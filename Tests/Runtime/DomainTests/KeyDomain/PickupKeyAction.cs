using System;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace KeyDomain
{
    struct PickupKeyAction : IJobParallelForDefer
    {
        public static Guid ActionGuid = Guid.NewGuid();

        const int k_KeyIndex = 0;
        const int k_AgentIndex = 1;
        const int k_AgentKeyIndex = 2;
        const int k_MaxArguments = 3;

        static readonly ComponentType[] s_AgentFilter = {  ComponentType.ReadWrite<Localized>(), ComponentType.ReadWrite<Carrier>(),  };
        static readonly ComponentType[] s_KeyFilter = {  ComponentType.ReadWrite<Carriable>(), ComponentType.ReadWrite<Colored>(),  };
        static readonly ComponentType[] s_RoomFilter = {  ComponentType.ReadWrite<Lockable>(), ComponentType.ReadWrite<Colored>(),  };


        [ReadOnly] NativeArray<StateEntityKey> m_StatesToExpand;
        StateDataContext m_StateDataContext;

        internal PickupKeyAction(NativeList<StateEntityKey> statesToExpand, StateDataContext stateDataContext)
        {
            m_StatesToExpand = statesToExpand.AsDeferredJobArray();
            m_StateDataContext = stateDataContext;
        }

        void GenerateArgumentPermutations(StateData stateData, NativeList<ActionKey> argumentPermutations)
        {
            var agentObjects = new NativeList<int>(4, Allocator.Temp);
            stateData.GetTraitBasedObjectIndices(agentObjects, s_AgentFilter);
            var keyObjects = new NativeList<int>(4, Allocator.Temp);
            stateData.GetTraitBasedObjectIndices(keyObjects, s_KeyFilter);
            var roomObjects = new NativeList<int>(4, Allocator.Temp);
            stateData.GetTraitBasedObjectIndices(roomObjects, s_RoomFilter);

            if (roomObjects.Length <= 0)
                return;

            var traitBasedObjectIds = stateData.TraitBasedObjectIds;
            var carriableBuffer = stateData.CarriableBuffer;
            var carrierBuffer = stateData.CarrierBuffer;
            var localizedBuffer = stateData.LocalizedBuffer;

            var firstRoom = traitBasedObjectIds[roomObjects[0]].Id;
            var agentKeyIndex = -1;

            for (var i = 0; i < keyObjects.Length; i++)
            {
                var keyIndex = keyObjects[i];
                var keyObject = stateData.TraitBasedObjects[keyIndex];

                if (carriableBuffer[keyObject.CarriableIndex].Carrier != ObjectId.None)
                    continue;

                for (var j = 0; j < agentObjects.Length; j++)
                {
                    var agentIndex = agentObjects[j];
                    var agentObject = stateData.TraitBasedObjects[agentIndex];

                    if (carrierBuffer[agentObject.CarrierIndex].CarriedObject == traitBasedObjectIds[keyIndex].Id)
                    {
                        agentKeyIndex = keyIndex;
                        break;
                    }
                }

                if (keyIndex >= 0)
                    break;
            }

            // Get argument permutation and check preconditions
            for (var i = 0; i < keyObjects.Length; i++)
            {
                var keyIndex = keyObjects[i];
                var keyObject = stateData.TraitBasedObjects[keyIndex];

                if (carriableBuffer[keyObject.CarriableIndex].Carrier != ObjectId.None)
                    continue;

                for (var j = 0; j < agentObjects.Length; j++)
                {
                    var agentIndex = agentObjects[j];
                    var agentObject = stateData.TraitBasedObjects[agentIndex];

                    if (carrierBuffer[agentObject.CarrierIndex].CarriedObject == traitBasedObjectIds[keyIndex].Id)
                        continue;

                    if (localizedBuffer[agentObject.LocalizedIndex].Location != firstRoom)
                        continue;

                    argumentPermutations.Add(new ActionKey(k_MaxArguments)
                    {
                        ActionGuid = ActionGuid,
                        [k_KeyIndex] = keyIndex,
                        [k_AgentIndex] = agentIndex,
                        [k_AgentKeyIndex] = agentKeyIndex
                    });
                }
            }

            agentObjects.Dispose();
            keyObjects.Dispose();
            roomObjects.Dispose();
        }

        StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo> ApplyEffects(ActionKey action, StateEntityKey originalStateEntityKey)
        {
            var originalState = m_StateDataContext.GetStateData(originalStateEntityKey);
            var originalStateObjectBuffer = originalState.TraitBasedObjects;
            var newState = m_StateDataContext.CopyStateData(originalState);

            var originalObjectIds = originalState.TraitBasedObjectIds;

            // Action effects
            var oldKeyIndex = action[k_KeyIndex];

            var newCarriableBuffer = newState.CarriableBuffer;
            var newCarrierBuffer = newState.CarrierBuffer;

            {
                if (oldKeyIndex >= 0)
                    newCarriableBuffer[oldKeyIndex] = new Carriable() {Carrier = ObjectId.None};
            }

            {
                newCarriableBuffer[originalStateObjectBuffer[action[k_KeyIndex]].CarriableIndex] =
                    new Carriable() {Carrier = originalObjectIds[action[k_AgentIndex]].Id};
            }

            {
                newCarrierBuffer[originalStateObjectBuffer[action[k_AgentIndex]].CarrierIndex] =
                    new Carrier() {CarriedObject = originalObjectIds[action[k_KeyIndex]].Id};
            }

            var reward = Reward(originalState, action, newState);
            var StateTransitionInfo = new StateTransitionInfo { Probability = 1f, TransitionUtilityValue = reward };
            var resultingStateKey = m_StateDataContext.GetStateDataKey(newState);

            return new StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo>(originalStateEntityKey, action, resultingStateKey, StateTransitionInfo);
        }

        float Reward(StateData originalState, ActionKey action, StateData newState)
        {
            return -1f;
        }

        public void Execute(int jobIndex)
        {
            m_StateDataContext.JobIndex = jobIndex;

            var stateEntityKey = m_StatesToExpand[jobIndex];
            var stateData = m_StateDataContext.GetStateData(stateEntityKey);

            var argumentPermutations = new NativeList<ActionKey>(4, Allocator.Temp);
            GenerateArgumentPermutations(stateData, argumentPermutations);

            var transitionInfo = new NativeArray<FixupReference>(argumentPermutations.Length, Allocator.Temp);
            for (var i = 0; i < argumentPermutations.Length; i++)
            {
                transitionInfo[i] = new FixupReference { TransitionInfo = ApplyEffects(argumentPermutations[i], stateEntityKey) };
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
