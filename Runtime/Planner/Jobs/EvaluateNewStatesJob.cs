using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.AI.Planner.Jobs
{
    //[BurstCompile]
    struct EvaluateNewStatesJob<TStateKey, TStateData, TStateDataContext, THeuristic, TTerminationEvaluator> : IJobParallelForDefer
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where THeuristic : struct, IHeuristic<TStateData>
        where TTerminationEvaluator : struct, ITerminationEvaluator<TStateData>
    {
        // Input
        [ReadOnly] public THeuristic Heuristic;
        [ReadOnly] public TTerminationEvaluator TerminationEvaluator;
        [ReadOnly] public TStateDataContext StateDataContext;
        [ReadOnly] public NativeArray<TStateKey> States;

        // Output
        [WriteOnly] public NativeHashMap<TStateKey, StateInfo>.ParallelWriter StateInfoLookup;
        [WriteOnly] public NativeMultiHashMap<int, TStateKey>.ParallelWriter BinnedStateKeys;

        public void Execute(int index)
        {
            var stateKey = States[index];
            var stateData = StateDataContext.GetStateData(stateKey);

            var terminal = TerminationEvaluator.IsTerminal(stateData, out var terminalReward);
            var value = terminal ?
                new BoundedValue(terminalReward, terminalReward, terminalReward) :
                Heuristic.Evaluate(stateData);

            StateInfoLookup.TryAdd(stateKey, new StateInfo
            {
                SubgraphComplete = terminal,
                PolicyValue = value,
            });

            BinnedStateKeys.Add(stateKey.GetHashCode(), stateKey);
        }
    }
}
