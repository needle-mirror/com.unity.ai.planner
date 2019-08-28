using System;
using Unity.Collections;
using UnityEngine;


namespace Unity.AI.Planner
{
    static class PolicyGraphExtensions
    {
        public static NativeHashMap<TStateKey, int> GetExpandedDepthMap<TStateKey, TStateInfo, TActionKey, TActionInfo, TActionResult>(this PolicyGraph<TStateKey, TStateInfo, TActionKey, TActionInfo, TActionResult> policyGraph, TStateKey rootKey)
            where TStateKey : struct, IEquatable<TStateKey>
            where TStateInfo : struct
            where TActionKey : struct, IEquatable<TActionKey>
            where TActionInfo : struct
            where TActionResult : struct
        {
            var depthMap = new NativeHashMap<TStateKey, int>(policyGraph.StateInfoLookup.Length, Allocator.Persistent);

            var stateActionLookup = policyGraph.StateActionLookup;
            var resultingStateLookup = policyGraph.ResultingStateLookup;
            var queue = new NativeQueue<(TStateKey, int)>(Allocator.TempJob);

            depthMap.TryAdd(rootKey, 0);
            queue.Enqueue((rootKey, 0));

            while (queue.TryDequeue(out var stateHorizonPair))
            {
                (var stateKey, int horizon) = stateHorizonPair;
                var nextHorizon = horizon + 1;

                if (stateActionLookup.TryGetFirstValue(stateKey, out var stateActionPair, out var iterator))
                {
                    do
                    {
                        if (resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var resultIterator))
                        {
                            do
                            {
                                // Skip unexpanded states
                                if (!stateActionLookup.TryGetFirstValue(resultingStateKey, out _, out _))
                                    continue;

                                // first add will be most shallow due to BFS
                                if(depthMap.TryAdd(resultingStateKey, nextHorizon))
                                    queue.Enqueue((resultingStateKey, nextHorizon));

                            } while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref resultIterator));
                        }

                    } while (stateActionLookup.TryGetNextValue(out stateActionPair, ref iterator));
                }
            }

            queue.Dispose();

            return depthMap;
        }

        public static NativeHashMap<TStateKey, int> GetReachableDepthMap<TStateKey, TStateInfo, TActionKey, TActionInfo, TActionResult>(this PolicyGraph<TStateKey, TStateInfo, TActionKey, TActionInfo, TActionResult> policyGraph, TStateKey rootKey)
            where TStateKey : struct, IEquatable<TStateKey>
            where TStateInfo : struct
            where TActionKey : struct, IEquatable<TActionKey>
            where TActionInfo : struct
            where TActionResult : struct
        {
            var depthMap = new NativeHashMap<TStateKey, int>(policyGraph.StateInfoLookup.Length, Allocator.Persistent);

            var stateActionLookup = policyGraph.StateActionLookup;
            var resultingStateLookup = policyGraph.ResultingStateLookup;
            var queue = new NativeQueue<(TStateKey, int)>(Allocator.TempJob);

            depthMap.TryAdd(rootKey, 0);
            queue.Enqueue((rootKey, 0));

            while (queue.TryDequeue(out var stateHorizonPair))
            {
                (var stateKey, int horizon) = stateHorizonPair;
                var nextHorizon = horizon + 1;

                if (stateActionLookup.TryGetFirstValue(stateKey, out var stateActionPair, out var iterator))
                {
                    do
                    {
                        if (resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var resultIterator))
                        {
                            do
                            {
                                // first add will be most shallow due to BFS
                                if(depthMap.TryAdd(resultingStateKey, nextHorizon))
                                    queue.Enqueue((resultingStateKey, nextHorizon));

                            } while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref resultIterator));
                        }

                    } while (stateActionLookup.TryGetNextValue(out stateActionPair, ref iterator));
                }
            }

            queue.Dispose();

            return depthMap;
        }


#if !UNITY_DOTSPLAYER
        public static void LogStructuralInfo<TStateKey, TStateInfo, TActionKey, TActionInfo, TActionResult>(this PolicyGraph<TStateKey, TStateInfo, TActionKey, TActionInfo, TActionResult> policyGraph)
            where TStateKey : struct, IEquatable<TStateKey>, IComparable<TStateKey>
            where TStateInfo : struct
            where TActionKey : struct, IEquatable<TActionKey>
            where TActionInfo : struct
            where TActionResult : struct
        {
            Debug.Log($"States: {policyGraph.StateInfoLookup.Length}");

            var (predecessorKeyArray, uniquePredecessorCount) = policyGraph.PredecessorGraph.GetUniqueKeyArray(Allocator.TempJob);
            Debug.Log($"States with Predecessors: {uniquePredecessorCount}");
            predecessorKeyArray.Dispose();

            var (stateActionKeyArray, uniqueStateActionCount) = policyGraph.StateActionLookup.GetUniqueKeyArray(Allocator.TempJob);
            Debug.Log($"States with Successors: {uniqueStateActionCount}");
            stateActionKeyArray.Dispose();

            Debug.Log($"Actions: {policyGraph.ActionInfoLookup.Length}");
            Debug.Log($"Action Results: {policyGraph.ActionResultLookup.Length}");
        }
#endif
    }
}
