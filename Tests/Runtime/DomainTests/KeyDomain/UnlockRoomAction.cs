using System;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace KeyDomain
{
    struct UnlockRoomAction : IJobParallelForDefer
    {
        public static Guid ActionGuid = Guid.NewGuid();

        const int k_AgentIndex = 0;
        const int k_RoomIndex = 1;
        const int k_MaxArguments = 2;

        static readonly ComponentType[] s_AgentFilter = {  ComponentType.ReadWrite<Localized>(), ComponentType.ReadWrite<Carrier>(),  };
        static readonly ComponentType[] s_RoomFilter = {  ComponentType.ReadWrite<Lockable>(), ComponentType.ReadWrite<Colored>(),  };
        static readonly ComponentType[] s_KeyFilter = {  ComponentType.ReadWrite<Carriable>(), ComponentType.ReadWrite<Colored>(),  };

        static readonly ComponentType[] s_RoomTypes =  {  ComponentType.ReadWrite<Lockable>(), ComponentType.ReadWrite<Colored>(),  };

        [ReadOnly] NativeArray<StateEntityKey> m_StatesToExpand;
        [ReadOnly] StateDataContext m_StateDataContext;

        internal UnlockRoomAction(NativeList<StateEntityKey> statesToExpand, StateDataContext stateDataContext)
        {
            m_StatesToExpand = statesToExpand.AsDeferredJobArray();
            m_StateDataContext = stateDataContext;
        }

        void GenerateArgumentPermutations(StateData stateData, NativeList<ActionKey> argumentPermutations)
        {
            var agentObjects = new NativeList<(DomainObject, int)>(4, Allocator.Temp);
            stateData.GetDomainObjects(agentObjects, s_AgentFilter);
            var roomObjects = new NativeList<(DomainObject, int)>(4, Allocator.Temp);
            stateData.GetDomainObjects(roomObjects, s_RoomFilter);
            var keyObjects = new NativeList<(DomainObject, int)>(4, Allocator.Temp);
            stateData.GetDomainObjects(keyObjects, s_KeyFilter);

            var objectIDs = stateData.DomainObjectIDs;
            var lockableBuffer = stateData.LockableBuffer;
            var localizedBuffer = stateData.LocalizedBuffer;
            var carrierBuffer = stateData.CarrierBuffer;
            var coloredBuffer = stateData.ColoredBuffer;

            // Get argument permutation and check preconditions
            for (var i = 0; i < roomObjects.Length; i++)
            {
                var (roomObject, roomIndex) = roomObjects[i];

                if (!lockableBuffer[roomObject.LockableIndex].Locked)
                    continue;

                for (var j = 0; j < agentObjects.Length; j++)
                {
                    var (agentObject, agentIndex) = agentObjects[j];

                    if (localizedBuffer[agentObject.LocalizedIndex].Location != objectIDs[roomIndex].ID)
                        continue;

                    for (var k = 0; k < keyObjects.Length; k++)
                    {
                        var (keyObject, keyIndex) = keyObjects[k];

                        if (carrierBuffer[agentObject.CarrierIndex].CarriedObject != objectIDs[keyIndex].ID)
                            continue;

                        if (!coloredBuffer[roomObject.ColoredIndex].Color.Equals(coloredBuffer[keyObject.ColoredIndex].Color))
                            continue;

                        argumentPermutations.Add(new ActionKey(k_MaxArguments)
                        {
                            ActionGuid = ActionGuid,
                            [k_AgentIndex] = agentIndex,
                            [k_RoomIndex] = roomIndex,
                        });
                    }
                }
            }

            agentObjects.Dispose();
            roomObjects.Dispose();
            keyObjects.Dispose();
        }

        NativeArray<(StateEntityKey, ActionKey, ActionResult, StateEntityKey)> ApplyEffects(ActionKey action, StateEntityKey originalStateEntityKey)
        {
            var results = new NativeArray<(StateEntityKey, ActionKey, ActionResult, StateEntityKey)>(3, Allocator.Temp);

            results[0] = CreateResultingState(originalStateEntityKey, action, ColorValue.Black, 0.4f, 1f, false);
            results[1] = CreateResultingState(originalStateEntityKey, action, ColorValue.White, 0.4f, 1f, false);
            results[2] = CreateResultingState(originalStateEntityKey, action, ColorValue.Black, 0.2f, 10f, true);

            return results;
        }

        (StateEntityKey, ActionKey, ActionResult, StateEntityKey) CreateResultingState(StateEntityKey originalStateEntityKey, ActionKey action,
            ColorValue roomColor, float probability, float reward, bool endRoom)
        {
            var originalState = m_StateDataContext.GetStateData(originalStateEntityKey);
            var originalStateObjectBuffer = originalState.DomainObjects;

            var newState = m_StateDataContext.CopyStateData(originalState);

            var newObjectBuffer = newState.DomainObjects;
            var newDomainIDBuffer = newState.DomainObjectIDs;
            var newLockableBuffer = newState.LockableBuffer;
            var newColoredBuffer = newState.ColoredBuffer;
            var newLocalizedBuffer = newState.LocalizedBuffer;
            var newEndBuffer = newState.EndBuffer;

            // Action effects
            var (newRoom, newRoomID) = newState.AddDomainObject(s_RoomTypes);
            var newRoomIndex = newState.GetDomainObjectIndex(newRoom);

            var newRoomLockable = newState.GetTraitOnObject<Lockable>(newRoom);
            newRoomLockable.Locked = true;
            newState.SetTraitOnObjectAtIndex(newRoomLockable, newRoomIndex);

            var newRoomColor = newState.GetTraitOnObject<Colored>(newRoom);
            newRoomColor.Color = roomColor;
            newState.SetTraitOnObjectAtIndex(newRoomColor, newRoomIndex);

            {
                newLockableBuffer[newObjectBuffer[action[k_RoomIndex]].LockableIndex] = new Lockable { Locked = false };
                newLocalizedBuffer[newObjectBuffer[action[k_AgentIndex]].LocalizedIndex] = new Localized { Location = newDomainIDBuffer[newRoomIndex].ID };
            }

            if (endRoom)
            {
                newEndBuffer.Add(new End());
                newRoom.EndIndex = (byte) (newEndBuffer.Length - 1);
                newObjectBuffer[newObjectBuffer.Length - 1] = newRoom;
            }

            var actionResult = new ActionResult { Probability = probability, TransitionUtilityValue = reward };
            var resultingStateKey = m_StateDataContext.GetStateDataKey(newState);

            return (originalStateEntityKey, action, actionResult, resultingStateKey);
        }

        public void Execute(int jobIndex)
        {
            m_StateDataContext.JobIndex = jobIndex;

            var stateEntityKey = m_StatesToExpand[jobIndex];
            var stateData = m_StateDataContext.GetStateData(stateEntityKey);

            var argumentPermutations = new NativeList<ActionKey>(4, Allocator.Temp);
            GenerateArgumentPermutations(stateData, argumentPermutations);

            var transitionInfo = new NativeArray<FixupReference>(argumentPermutations.Length * 3, Allocator.Temp);
            for (var i = 0; i < argumentPermutations.Length; i++)
            {
                var results = ApplyEffects(argumentPermutations[i], stateEntityKey);
                for (int j = 0; j < 3; j++)
                {
                    transitionInfo[i + j] = new FixupReference { TransitionInfo = results[j] };
                }
                results.Dispose();
            }

            // fixups
            var stateEntity = stateEntityKey.Entity;
            var fixupBuffer = m_StateDataContext.EntityCommandBuffer.AddBuffer<FixupReference>(jobIndex, stateEntity);
            fixupBuffer.CopyFrom(transitionInfo);

            transitionInfo.Dispose();
            argumentPermutations.Dispose();
        }

        public struct FixupReference : IBufferElementData
        {
            public (StateEntityKey, ActionKey, ActionResult, StateEntityKey) TransitionInfo;
        }
    }
}
