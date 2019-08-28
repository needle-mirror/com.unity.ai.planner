using System;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.AI.Planner.Jobs
{
    struct PrepareForExpansionJob<TStateKey, TActionKey> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> PolicyGraph { get; set; }

        public NativeQueue<(TStateKey, TActionKey, ActionResult, TStateKey)> InputStateExpansionInfo { get; set; }
        public NativeList<(TStateKey, TActionKey, ActionResult, TStateKey)> OutputStateExpansionInfo { get; set; }

        public void Execute()
        {
            var maximumCapacityNeeded = InputStateExpansionInfo.Count;
            PolicyGraph.ExpandBy(maximumCapacityNeeded, maximumCapacityNeeded);

            while (InputStateExpansionInfo.TryDequeue(out var item))
            {
                OutputStateExpansionInfo.Add(item);
            }
        }
    }

    struct GraphExpansionJob<TStateKey, TStateData, TStateDataContext, TActionKey> : IJobParallelForDefer
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        [ReadOnly] public NativeArray<(TStateKey, TActionKey, ActionResult, TStateKey)> NewStateTransitionInfo;
        [ReadOnly] public NativeArray<TStateKey> ExistingStateKeys;

        public TStateDataContext StateDataContext;
        public NativeQueue<TStateKey>.ParallelWriter NewStates;
        public NativeMultiHashMap<TStateKey, (TStateKey, TActionKey)>.ParallelWriter StateActionLookup;
        public NativeHashMap<(TStateKey, TActionKey), ActionInfo>.ParallelWriter ActionInfoLookup;
        public NativeMultiHashMap<(TStateKey, TActionKey), TStateKey>.ParallelWriter ActionToStateLookup;
        public NativeMultiHashMap<TStateKey, TStateKey>.ParallelWriter PredecessorGraph;
        public NativeHashMap<(TStateKey, TActionKey, TStateKey), ActionResult>.ParallelWriter ActionResultLookup;
        public NativeQueue<TStateKey>.ParallelWriter StatesToDestroy;

        public void Execute(int index)
        {
            var (precedingStateKey, actionKey, actionResult, stateKey) = NewStateTransitionInfo[index];
            var stateData = StateDataContext.GetStateData(stateKey);

            // Iterate over all potential matches. If any match -> existing; otherwise -> new.
            foreach (var otherStateKey in ExistingStateKeys)
            {
                if (otherStateKey.GetHashCode() == stateKey.GetHashCode()
                    && StateDataContext.Equals(StateDataContext.GetStateData(otherStateKey), stateData))
                {
                    WriteEdgeToState(precedingStateKey, actionKey, actionResult, otherStateKey);
                    StatesToDestroy.Enqueue(stateKey);
                    return;
                }
            }

            for (var i = 0; i < NewStateTransitionInfo.Length; i++)
            {
                var resultingStateKey = NewStateTransitionInfo[i].Item4;

                if (resultingStateKey.GetHashCode() == stateKey.GetHashCode()
                    && StateDataContext.Equals(StateDataContext.GetStateData(resultingStateKey), stateData))
                {
                    WriteEdgeToState(precedingStateKey, actionKey, actionResult, resultingStateKey);

                    if (i == index) // Matched to self -> output for heuristic evaluation
                        NewStates.Enqueue(stateKey);
                    else
                        StatesToDestroy.Enqueue(stateKey);

                    return;
                }
            }

            throw new Exception("State not matched during lookup.");
        }

        void WriteEdgeToState(TStateKey precedingStateKey, TActionKey actionKey, ActionResult actionResult, TStateKey resultingStateKey)
        {
            StateActionLookup.Add(precedingStateKey, (precedingStateKey, actionKey));
            ActionInfoLookup.TryAdd((precedingStateKey, actionKey), new ActionInfo { VisitCount = 1 });
            ActionToStateLookup.Add((precedingStateKey, actionKey), resultingStateKey);
            ActionResultLookup.TryAdd((precedingStateKey, actionKey, resultingStateKey), actionResult);
            PredecessorGraph.Add(resultingStateKey, precedingStateKey);
        }
    }
}
