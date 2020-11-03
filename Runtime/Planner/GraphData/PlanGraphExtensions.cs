using System;
using Unity.Collections;
using UnityEngine;


namespace Unity.AI.Planner
{
    static class PlanGraphExtensions
    {
        public static void GetExpandedDepthMap<TStateKey, TStateInfo, TActionKey, TActionInfo, TStateTransitionInfo>(this PlanGraph<TStateKey, TStateInfo, TActionKey, TActionInfo, TStateTransitionInfo> planGraph, TStateKey rootKey, NativeHashMap<TStateKey, int> depthMap, NativeQueue<StateHorizonPair<TStateKey>> queue)
            where TStateKey : struct, IEquatable<TStateKey>
            where TStateInfo : struct, IStateInfo
            where TActionKey : struct, IEquatable<TActionKey>
            where TActionInfo : struct, IActionInfo
            where TStateTransitionInfo : struct
        {
            depthMap.Clear();
            queue.Clear();
            var actionLookup = planGraph.ActionLookup;
            var resultingStateLookup = planGraph.ResultingStateLookup;

            depthMap.TryAdd(rootKey, 0);
            queue.Enqueue(new StateHorizonPair<TStateKey> {StateKey = rootKey, Horizon = 0});

            while (queue.TryDequeue(out var stateHorizonPair))
            {
                var stateKey = stateHorizonPair.StateKey;
                var horizon = stateHorizonPair.Horizon;
                var nextHorizon = horizon + 1;

                if (actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var iterator))
                {
                    do
                    {
                        var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                        if (resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var resultIterator))
                        {
                            do
                            {
                                // Skip unexpanded states
                                if (!actionLookup.TryGetFirstValue(resultingStateKey, out _, out _))
                                    continue;

                                // first add will be most shallow due to BFS
                                if(depthMap.TryAdd(resultingStateKey, nextHorizon))
                                    queue.Enqueue(new StateHorizonPair<TStateKey>() { StateKey =  resultingStateKey, Horizon =  nextHorizon});

                            } while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref resultIterator));
                        }

                    } while (actionLookup.TryGetNextValue(out actionKey, ref iterator));
                }
            }
        }

        public static void GetReachableDepthMap<TStateKey, TStateInfo, TActionKey, TActionInfo, TStateTransitionInfo>(this PlanGraph<TStateKey, TStateInfo, TActionKey, TActionInfo, TStateTransitionInfo> planGraph, TStateKey rootKey, NativeHashMap<TStateKey, int> depthMap, NativeQueue<StateHorizonPair<TStateKey>> queue)
            where TStateKey : struct, IEquatable<TStateKey>
            where TStateInfo : struct, IStateInfo
            where TActionKey : struct, IEquatable<TActionKey>
            where TActionInfo : struct, IActionInfo
            where TStateTransitionInfo : struct
        {
            depthMap.Clear();
            queue.Clear();
            var actionLookup = planGraph.ActionLookup;
            var resultingStateLookup = planGraph.ResultingStateLookup;

            depthMap.TryAdd(rootKey, 0);
            queue.Enqueue(new StateHorizonPair<TStateKey>{ StateKey = rootKey, Horizon = 0 });

            while (queue.TryDequeue(out var stateHorizonPair))
            {
                var stateKey = stateHorizonPair.StateKey;
                var horizon = stateHorizonPair.Horizon;
                var nextHorizon = horizon + 1;

                if (actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var iterator))
                {
                    do
                    {
                        var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                        if (resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var resultIterator))
                        {
                            do
                            {
                                // first add will be most shallow due to BFS
                                if(depthMap.TryAdd(resultingStateKey, nextHorizon))
                                    queue.Enqueue(new StateHorizonPair<TStateKey>() { StateKey =  resultingStateKey, Horizon =  nextHorizon});

                            } while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref resultIterator));
                        }

                    } while (actionLookup.TryGetNextValue(out actionKey, ref iterator));
                }
            }
        }


#if !UNITY_DOTSPLAYER
        public static void LogStructuralInfo<TStateKey, TStateInfo, TActionKey, TActionInfo, TStateTransitionInfo>(this PlanGraph<TStateKey, TStateInfo, TActionKey, TActionInfo, TStateTransitionInfo> planGraph)
            where TStateKey : struct, IEquatable<TStateKey>, IComparable<TStateKey>
            where TStateInfo : struct, IStateInfo
            where TActionKey : struct, IEquatable<TActionKey>
            where TActionInfo : struct, IActionInfo
            where TStateTransitionInfo : struct
        {
            Debug.Log($"States: {planGraph.StateInfoLookup.Count()}");

            var (predecessorKeyArray, uniquePredecessorCount) = planGraph.PredecessorGraph.GetUniqueKeyArray(Allocator.TempJob);
            Debug.Log($"States with Predecessors: {uniquePredecessorCount}");
            predecessorKeyArray.Dispose();

            var (stateActionKeyArray, uniqueStateActionCount) = planGraph.ActionLookup.GetUniqueKeyArray(Allocator.TempJob);
            Debug.Log($"States with Successors: {uniqueStateActionCount}");
            stateActionKeyArray.Dispose();

            Debug.Log($"Actions: {planGraph.ActionInfoLookup.Count()}");
            Debug.Log($"Action Results: {planGraph.StateTransitionInfoLookup.Count()}");
        }
#endif
    }
}
