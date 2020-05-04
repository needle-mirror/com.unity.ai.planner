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
        StateDataContext m_StateDataContext;

        internal UnlockRoomAction(NativeList<StateEntityKey> statesToExpand, StateDataContext stateDataContext)
        {
            m_StatesToExpand = statesToExpand.AsDeferredJobArray();
            m_StateDataContext = stateDataContext;
        }

        void GenerateArgumentPermutations(StateData stateData, NativeList<ActionKey> argumentPermutations)
        {
            var traitBasedObjects = stateData.TraitBasedObjects;
            var agentObjects = new NativeList<int>(4, Allocator.Temp);
            stateData.GetTraitBasedObjectIndices(agentObjects, s_AgentFilter);
            var roomObjects = new NativeList<int>(4, Allocator.Temp);
            stateData.GetTraitBasedObjectIndices(roomObjects, s_RoomFilter);
            var keyObjects = new NativeList<int>(4, Allocator.Temp);
            stateData.GetTraitBasedObjectIndices(keyObjects, s_KeyFilter);

            var objectIds = stateData.TraitBasedObjectIds;
            var lockableBuffer = stateData.LockableBuffer;
            var localizedBuffer = stateData.LocalizedBuffer;
            var carrierBuffer = stateData.CarrierBuffer;
            var coloredBuffer = stateData.ColoredBuffer;

            // Get argument permutation and check preconditions
            for (var i = 0; i < roomObjects.Length; i++)
            {
                var roomIndex = roomObjects[i];
                var roomObject = traitBasedObjects[roomIndex];

                if (!lockableBuffer[roomObject.LockableIndex].Locked)
                    continue;

                for (var j = 0; j < agentObjects.Length; j++)
                {
                    var agentIndex = agentObjects[j];
                    var agentObject = traitBasedObjects[agentIndex];

                    if (localizedBuffer[agentObject.LocalizedIndex].Location != objectIds[roomIndex].Id)
                        continue;

                    for (var k = 0; k < keyObjects.Length; k++)
                    {
                        var keyIndex = keyObjects[k];
                        var keyObject = traitBasedObjects[keyIndex];

                        if (carrierBuffer[agentObject.CarrierIndex].CarriedObject != objectIds[keyIndex].Id)
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

        NativeArray<StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo>> ApplyEffects(ActionKey action, StateEntityKey originalStateEntityKey)
        {
            var results = new NativeArray<StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo>>(3, Allocator.Temp);

            results[0] = CreateResultingState(originalStateEntityKey, action, ColorValue.Black, 0.4f, 1f, false);
            results[1] = CreateResultingState(originalStateEntityKey, action, ColorValue.White, 0.4f, 1f, false);
            results[2] = CreateResultingState(originalStateEntityKey, action, ColorValue.Black, 0.2f, 10f, true);

            return results;
        }

        StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo> CreateResultingState(StateEntityKey originalStateEntityKey, ActionKey action,
            ColorValue roomColor, float probability, float reward, bool endRoom)
        {
            var originalState = m_StateDataContext.GetStateData(originalStateEntityKey);
            var originalStateObjectBuffer = originalState.TraitBasedObjects;

            var newState = m_StateDataContext.CopyStateData(originalState);

            var newObjectBuffer = newState.TraitBasedObjects;
            var newDomainIdBuffer = newState.TraitBasedObjectIds;
            var newLockableBuffer = newState.LockableBuffer;
            var newColoredBuffer = newState.ColoredBuffer;
            var newLocalizedBuffer = newState.LocalizedBuffer;
            var newEndBuffer = newState.EndBuffer;

            // Action effects
            newState.AddTraitBasedObject(s_RoomTypes, out var newRoom, out _);
            var newRoomIndex = newState.GetTraitBasedObjectIndex(newRoom);

            var newRoomLockable = newState.GetTraitOnObject<Lockable>(newRoom);
            newRoomLockable.Locked = true;
            newState.SetTraitOnObjectAtIndex(newRoomLockable, newRoomIndex);

            var newRoomColor = newState.GetTraitOnObject<Colored>(newRoom);
            newRoomColor.Color = roomColor;
            newState.SetTraitOnObjectAtIndex(newRoomColor, newRoomIndex);

            {
                newLockableBuffer[newObjectBuffer[action[k_RoomIndex]].LockableIndex] = new Lockable { Locked = false };
                newLocalizedBuffer[newObjectBuffer[action[k_AgentIndex]].LocalizedIndex] = new Localized { Location = newDomainIdBuffer[newRoomIndex].Id };
            }

            if (endRoom)
            {
                newEndBuffer.Add(new End());
                newRoom.EndIndex = (byte) (newEndBuffer.Length - 1);
                newObjectBuffer[newObjectBuffer.Length - 1] = newRoom;
            }

            var StateTransitionInfo = new StateTransitionInfo { Probability = probability, TransitionUtilityValue = reward };
            var resultingStateKey = m_StateDataContext.GetStateDataKey(newState);

            return new StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo>(originalStateEntityKey, action, resultingStateKey, StateTransitionInfo);
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
            public StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo> TransitionInfo;
        }
    }
}
