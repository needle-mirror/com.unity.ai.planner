using System;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace KeyDomain
{
    struct MoveAction : IJobParallelForDefer
    {
        public static Guid ActionGuid = Guid.NewGuid();

        public const int k_AgentIndex = 0;
        public const int k_RoomIndex = 1;
        public const int k_MaxArguments = 2;

        static readonly ComponentType[] s_AgentFilter = {  ComponentType.ReadWrite<Localized>() };
        static readonly ComponentType[] s_RoomFilter = {  ComponentType.ReadWrite<Lockable>()  };

        [ReadOnly] NativeArray<StateEntityKey> m_StatesToExpand;
        StateDataContext m_StateDataContext;

        internal MoveAction(NativeList<StateEntityKey> statesToExpand, StateDataContext stateDataContext)
        {
            m_StatesToExpand = statesToExpand.AsDeferredJobArray();
            m_StateDataContext = stateDataContext;
        }

        void GenerateArgumentPermutations(StateData stateData, NativeList<ActionKey> argumentPermutations)
        {
            var agentObjects = new NativeList<int>(4, Allocator.Temp);
            stateData.GetTraitBasedObjectIndices(agentObjects, s_AgentFilter);
            var roomObjects = new NativeList<int>(4, Allocator.Temp);
            stateData.GetTraitBasedObjectIndices(roomObjects, s_RoomFilter);

            var localizedBuffer = stateData.LocalizedBuffer;
            var objectIds = stateData.TraitBasedObjectIds;

            // Get argument permutation and check preconditions
            for (var i = 0; i < roomObjects.Length; i++)
            {
                var roomIndex = roomObjects[i];

                for (var j = 0; j < agentObjects.Length; j++)
                {
                    var agentIndex = agentObjects[j];
                    var agentObject = stateData.TraitBasedObjects[agentIndex];

                    if (localizedBuffer[agentObject.LocalizedIndex].Location == objectIds[roomIndex].Id)
                        continue;

                    argumentPermutations.Add(new ActionKey(k_MaxArguments)
                    {
                        ActionGuid = ActionGuid,
                        [k_AgentIndex] = agentIndex,
                        [k_RoomIndex] = roomIndex,
                    });
                }
            }

            agentObjects.Dispose();
            roomObjects.Dispose();
        }

        StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo> ApplyEffects(ActionKey action, StateEntityKey originalStateEntityKey)
        {
            var originalState = m_StateDataContext.GetStateData(originalStateEntityKey);
            var originalStateObjectBuffer = originalState.TraitBasedObjects;

            // effect params
            var originalAgentObject = originalStateObjectBuffer[action[k_AgentIndex]];
            var originalRoomObject = originalStateObjectBuffer[action[k_RoomIndex]];

            var newState = m_StateDataContext.CopyStateData(originalState);

            var newLocalizedBuffer = newState.LocalizedBuffer;
            var newTraitBasedObjectIdsBuffer = newState.TraitBasedObjectIds;

            // Action effects
            {
                var @Localized = newLocalizedBuffer[originalAgentObject.LocalizedIndex];
                var @TraitBasedObjectId = newTraitBasedObjectIdsBuffer[action[k_RoomIndex]];
                @Localized.Location = @TraitBasedObjectId.Id;
                newLocalizedBuffer[originalAgentObject.LocalizedIndex] = @Localized;
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
