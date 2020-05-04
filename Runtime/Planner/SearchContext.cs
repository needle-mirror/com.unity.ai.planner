using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.AI.Planner
{
    class SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
    {
        // Info for search
        public TStateKey RootStateKey { get; internal set; }
        public NativeHashMap<TStateKey, int> StateDepthLookup;
        public NativeMultiHashMap<int, TStateKey> BinnedStateKeyLookup;
        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> PolicyGraph;

        internal TStateManager m_StateManager;
        internal JobHandle m_PlanningJobHandle;
        internal int m_FramesSinceComplete;

        public SearchContext(TStateManager stateManager, int stateCapacity = 1, int actionCapacity = 1, int transitionCapacity = 1)
        {
            m_StateManager = stateManager;

            StateDepthLookup = new NativeHashMap<TStateKey, int>(stateCapacity, Allocator.Persistent);
            BinnedStateKeyLookup = new NativeMultiHashMap<int, TStateKey>(stateCapacity, Allocator.Persistent);
            PolicyGraph = new PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo>(stateCapacity, actionCapacity, transitionCapacity);
        }

        public void UpdateRootState(TStateKey stateKey)
        {
            var newRoot = FindMatchingStateInPlan(stateKey, out var planKey) ? planKey : m_StateManager.CopyState(stateKey);

            // If it's not in the plan, add it to the binned keys, state info lookup, and depth map
            if (PolicyGraph.StateInfoLookup.TryAdd(newRoot, new StateInfo { PolicyValue = new BoundedValue(float.MinValue, 0, float.MaxValue), SubgraphComplete = false }))
            {
                BinnedStateKeyLookup.Add(newRoot.GetHashCode(), newRoot);
                StateDepthLookup.TryAdd(newRoot, 0);
            }

            RootStateKey = newRoot;
        }

        public void DecrementSearchDepths()
        {
            // Decrement search depths
            using (var stateKeyArray = StateDepthLookup.GetKeyArray(Allocator.Temp))
            {
                foreach (var stateKey in stateKeyArray)
                {
                    StateDepthLookup[stateKey] -= 1;
                }
            }
        }

        internal void Prune()
        {
            var minimumReachableDepthMap = new NativeHashMap<TStateKey, int>(PolicyGraph.Size, Allocator.Temp);
            using (var queue = new NativeQueue<StateHorizonPair<TStateKey>>(Allocator.Temp))
            {
                PolicyGraph.GetReachableDepthMap(RootStateKey, minimumReachableDepthMap, queue);
            }
            var stateKeyArray = PolicyGraph.StateInfoLookup.GetKeyArray(Allocator.Temp);

            foreach (var stateKey in stateKeyArray)
            {
                if (!minimumReachableDepthMap.TryGetValue(stateKey, out _))
                {
                    // Graph containers
                    PolicyGraph.RemoveState(stateKey);
                    StateDepthLookup.Remove(stateKey);
                    BinnedStateKeyLookup.Remove(stateKey.GetHashCode(), stateKey);

                    // State data for the key
                    m_StateManager.DestroyState(stateKey);
                }
            }

            stateKeyArray.Dispose();
            minimumReachableDepthMap.Dispose();
        }

        public bool RootsConverged(float tolerance)
        {
            if (!PolicyGraph.StateInfoLookup.TryGetValue(RootStateKey, out var rootInfo))
                throw new KeyNotFoundException($"Root state {RootStateKey} is not contained within the policy graph.");

            return rootInfo.PolicyValue.Range <= tolerance;
        }

        public bool IsTerminal(TStateKey stateKey)
        {
            return PolicyGraph.StateInfoLookup.TryGetValue(stateKey, out var stateInfo)
                && stateInfo.SubgraphComplete
                && !PolicyGraph.ActionLookup.TryGetFirstValue(stateKey, out _, out _);
        }

        public bool FindMatchingStateInPlan(TStateKey stateKey, out TStateKey planStateKey)
        {
            if (PolicyGraph.StateInfoLookup.ContainsKey(stateKey))
            {
                planStateKey = stateKey;
                return true;
            }

            var stateData = m_StateManager.GetStateData(stateKey, false); // todo might pass in state data from a different state manager
            var stateHashCode = stateKey.GetHashCode();

            if (BinnedStateKeyLookup.TryGetFirstValue(stateHashCode, out planStateKey, out var iterator))
            {
                do
                {
                    var planStateData = m_StateManager.GetStateData(planStateKey, false);
                    if (m_StateManager.Equals(planStateData, stateData))
                        return true;
                } while (BinnedStateKeyLookup.TryGetNextValue(out planStateKey, ref iterator));
            }

            return false;
        }

        public void Dispose(JobHandle jobHandle = default)
        {
            if (StateDepthLookup.IsCreated)
                StateDepthLookup.Dispose(jobHandle);

            if (BinnedStateKeyLookup.IsCreated)
                BinnedStateKeyLookup.Dispose(jobHandle);

            PolicyGraph.Dispose(jobHandle);
        }

        public void CompletePlanningJobs()
        {
            m_PlanningJobHandle.Complete();
            m_FramesSinceComplete = 0;
        }
    }
}
