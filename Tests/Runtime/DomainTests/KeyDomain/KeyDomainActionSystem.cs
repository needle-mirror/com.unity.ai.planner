using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace KeyDomain
{
    using StateTransitionInfo = ValueTuple<StateEntityKey, ActionKey, ActionResult, StateEntityKey>;

    internal class ActionScheduler :
        ITraitBasedActionScheduler<DomainObject, StateEntityKey, StateData, StateDataContext, StateManager, ActionKey, ActionResult>,
        IGetActionName
    {
        // Input
        public NativeList<StateEntityKey> UnexpandedStates { get; set; }
        public StateManager StateManager { get; set; }

        // Output
        public NativeQueue<StateTransitionInfo> CreatedStateInfo { get; set; }

        public Guid[] ActionGuids => s_ActionGuids;

        static Guid[] s_ActionGuids = {
            MoveAction.ActionGuid,
            PickupKeyAction.ActionGuid,
            UnlockRoomAction.ActionGuid
        };

        static Dictionary<Guid, string> s_ActionGuidToNameLookup = new Dictionary<Guid,string>()
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

        public JobHandle Schedule(JobHandle inputDeps)
        {
            var MoveActionDataContext = StateManager.GetStateDataContext();
            var MoveActionECB = new EntityCommandBuffer(Allocator.TempJob);
            MoveActionDataContext.EntityCommandBuffer = MoveActionECB.ToConcurrent();
            var PickupKeyActionDataContext = StateManager.GetStateDataContext();
            var PickupKeyActionECB = new EntityCommandBuffer(Allocator.TempJob);
            PickupKeyActionDataContext.EntityCommandBuffer = PickupKeyActionECB.ToConcurrent();
            var UnlockRoomActionDataContext = StateManager.GetStateDataContext();
            var UnlockRoomActionECB = new EntityCommandBuffer(Allocator.TempJob);
            UnlockRoomActionDataContext.EntityCommandBuffer = UnlockRoomActionECB.ToConcurrent();


            var allActionJobs = new NativeArray<JobHandle>(3, Allocator.TempJob)
            {
                [0] = new MoveAction(UnexpandedStates, MoveActionDataContext).Schedule(UnexpandedStates, 0, inputDeps),
                [1] = new PickupKeyAction(UnexpandedStates, PickupKeyActionDataContext).Schedule(UnexpandedStates, 0, inputDeps),
                [2] = new UnlockRoomAction(UnexpandedStates, UnlockRoomActionDataContext).Schedule(UnexpandedStates, 0, inputDeps),
            };

            JobHandle.CompleteAll(allActionJobs);

            // Playback entity changes and output state transition info
            var entityManager = StateManager.EntityManager;

            MoveActionECB.Playback(entityManager);
            for (int i = 0; i < UnexpandedStates.Length; i++)
            {
                var MoveActionRefs = entityManager.GetBuffer<MoveAction.FixupReference>(UnexpandedStates[i].Entity);
                for (int j = 0; j < MoveActionRefs.Length; j++)
                    CreatedStateInfo.Enqueue(MoveActionRefs[j].TransitionInfo);
            }

            PickupKeyActionECB.Playback(entityManager);
            for (int i = 0; i < UnexpandedStates.Length; i++)
            {
                var PickupKeyActionRefs = entityManager.GetBuffer<MoveAction.FixupReference>(UnexpandedStates[i].Entity);
                for (int j = 0; j < PickupKeyActionRefs.Length; j++)
                    CreatedStateInfo.Enqueue(PickupKeyActionRefs[j].TransitionInfo);
            }

            UnlockRoomActionECB.Playback(entityManager);
            for (int i = 0; i < UnexpandedStates.Length; i++)
            {
                var UnlockRoomActionRefs = entityManager.GetBuffer<MoveAction.FixupReference>(UnexpandedStates[i].Entity);
                for (int j = 0; j < UnlockRoomActionRefs.Length; j++)
                    CreatedStateInfo.Enqueue(UnlockRoomActionRefs[j].TransitionInfo);
            }

            allActionJobs.Dispose();
            MoveActionECB.Dispose();
            PickupKeyActionECB.Dispose();
            UnlockRoomActionECB.Dispose();

            return default;
        }
    }
}
