using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Unity.AI.Planner.Jobs
{
    struct EvaluateNewStatesJob<TStateKey, TStateData, TStateDataContext, THeuristic, TTermination> : IJobParallelForDefer
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where THeuristic : struct, IHeuristic<TStateData>
        where TTermination : struct, ITerminationEvaluator<TStateData>
    {
        // inputs
        [field:ReadOnly] public THeuristic Heuristic { get; set; }
        [field:ReadOnly] public TTermination TerminationEvaluator { get; set; }
        [ReadOnly] public TStateDataContext StateDataContext;
        [ReadOnly] public NativeArray<TStateKey> States;

        //output
        [field:WriteOnly] public NativeHashMap<TStateKey, StateInfo>.ParallelWriter StateInfoLookup { get; set; }

        public void Execute(int index)
        {
            var stateKey = States[index];
            var stateData = StateDataContext.GetStateData(stateKey);

            StateInfoLookup.TryAdd(stateKey, new StateInfo()
            {
                Complete = TerminationEvaluator.IsTerminal(stateData),
                PolicyValue = Heuristic.Evaluate(stateData),
                VisitCount = 1,
            });
        }
    }
}
