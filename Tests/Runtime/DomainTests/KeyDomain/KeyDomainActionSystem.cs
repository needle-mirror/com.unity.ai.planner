using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace KeyDomain
{
    struct ActionScheduler :
        ITraitBasedActionScheduler<TraitBasedObject, StateEntityKey, StateData, StateDataContext, StateManager, ActionKey>
    {
        // Input
        public NativeList<StateEntityKey> UnexpandedStates { get; set; }
        public StateManager StateManager { get; set; }

        // Output
        public NativeQueue<StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo>> CreatedStateInfo { private get; set; }

        internal static Dictionary<Guid, string> s_ActionGuidToNameLookup = new Dictionary<Guid,string>()
        {
            { MoveAction.ActionGuid, nameof(MoveAction) },
            { PickupKeyAction.ActionGuid, nameof(PickupKeyAction) },
            { UnlockRoomAction.ActionGuid, nameof(UnlockRoomAction) },
        };

        public string GetActionName(IActionKey actionKey)
        {
            s_ActionGuidToNameLookup.TryGetValue(((IActionKeyWithGuid)actionKey).ActionGuid, out var name);
            return name;
        }

        struct PlaybackECB : IJob
        {
            public ExclusiveEntityTransaction ExclusiveEntityTransaction;

            [ReadOnly]
            public NativeList<StateEntityKey> UnexpandedStates;
            public NativeQueue<StateTransitionInfoPair<StateEntityKey, ActionKey, StateTransitionInfo>> CreatedStateInfo;

            public EntityCommandBuffer MoveActionECB;
            public EntityCommandBuffer PickupKeyActionECB;
            public EntityCommandBuffer UnlockRoomActionECB;

            public void Execute()
            {
                // Playback entity changes and output state transition info
                var entityManager = ExclusiveEntityTransaction;

                MoveActionECB.Playback(entityManager);
                for (int i = 0; i < UnexpandedStates.Length; i++)
                {
                    var stateEntity = UnexpandedStates[i].Entity;
                    var MoveActionRefs = entityManager.GetBuffer<MoveAction.FixupReference>(stateEntity);
                    for (int j = 0; j < MoveActionRefs.Length; j++)
                        CreatedStateInfo.Enqueue(MoveActionRefs[j].TransitionInfo);
                    entityManager.RemoveComponent(stateEntity, typeof(MoveAction.FixupReference));
                }

                PickupKeyActionECB.Playback(entityManager);
                for (int i = 0; i < UnexpandedStates.Length; i++)
                {
                    var stateEntity = UnexpandedStates[i].Entity;
                    var PickupKeyActionRefs = entityManager.GetBuffer<PickupKeyAction.FixupReference>(stateEntity);
                    for (int j = 0; j < PickupKeyActionRefs.Length; j++)
                        CreatedStateInfo.Enqueue(PickupKeyActionRefs[j].TransitionInfo);
                    entityManager.RemoveComponent(stateEntity, typeof(PickupKeyAction.FixupReference));
                }

                UnlockRoomActionECB.Playback(entityManager);
                for (int i = 0; i < UnexpandedStates.Length; i++)
                {
                    var stateEntity = UnexpandedStates[i].Entity;
                    var UnlockRoomActionRefs = entityManager.GetBuffer<UnlockRoomAction.FixupReference>(stateEntity);
                    for (int j = 0; j < UnlockRoomActionRefs.Length; j++)
                        CreatedStateInfo.Enqueue(UnlockRoomActionRefs[j].TransitionInfo);
                    entityManager.RemoveComponent(stateEntity, typeof(UnlockRoomAction.FixupReference));
                }
            }
        }

        public JobHandle Schedule(JobHandle inputDeps)
        {
            var entityManager = StateManager.EntityManager;

            var MoveActionDataContext = StateManager.GetStateDataContext();
            var MoveActionECB = StateManager.GetEntityCommandBuffer();
            MoveActionDataContext.EntityCommandBuffer = MoveActionECB.ToConcurrent();
            var PickupKeyActionDataContext = StateManager.GetStateDataContext();
            var PickupKeyActionECB = StateManager.GetEntityCommandBuffer();
            PickupKeyActionDataContext.EntityCommandBuffer = PickupKeyActionECB.ToConcurrent();
            var UnlockRoomActionDataContext = StateManager.GetStateDataContext();
            var UnlockRoomActionECB = StateManager.GetEntityCommandBuffer();
            UnlockRoomActionDataContext.EntityCommandBuffer = UnlockRoomActionECB.ToConcurrent();

            var allActionJobs = new NativeArray<JobHandle>(4, Allocator.TempJob)
            {
                [0] = new MoveAction(UnexpandedStates, MoveActionDataContext).Schedule(UnexpandedStates, 0, inputDeps),
                [1] = new PickupKeyAction(UnexpandedStates, PickupKeyActionDataContext).Schedule(UnexpandedStates, 0, inputDeps),
                [2] = new UnlockRoomAction(UnexpandedStates, UnlockRoomActionDataContext).Schedule(UnexpandedStates, 0, inputDeps),
                [3] = entityManager.ExclusiveEntityTransactionDependency,
            };

            var allActionJobsHandle = JobHandle.CombineDependencies(allActionJobs);
            allActionJobs.Dispose();

            // Playback entity changes and output state transition info
            var playbackJob = new PlaybackECB()
            {
                ExclusiveEntityTransaction = StateManager.ExclusiveEntityTransaction,
                UnexpandedStates = UnexpandedStates,
                CreatedStateInfo = CreatedStateInfo,
                MoveActionECB = MoveActionECB,
                PickupKeyActionECB = PickupKeyActionECB,
                UnlockRoomActionECB = UnlockRoomActionECB,
            };

            var playbackJobHandle = playbackJob.Schedule(allActionJobsHandle);
            entityManager.ExclusiveEntityTransactionDependency = playbackJobHandle;

            return playbackJobHandle;

        }
    }
}
