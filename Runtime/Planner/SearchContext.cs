using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.AI.Planner
{
    class SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> : ISearchContext<TStateKey, TActionKey>
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
    {
        // Info for search
        public TStateKey RootStateKey { get; set; }
        public NativeHashMap<TStateKey, int> StateDepthLookup;
        public NativeMultiHashMap<int, TStateKey> BinnedStateKeyLookup;
        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> PolicyGraph;

        TStateManager m_StateManager;

        public SearchContext(TStateKey rootStateKey, TStateManager stateManager, int stateCapacity = 1, int actionCapacity = 1)
        {
            RootStateKey = rootStateKey;
            m_StateManager = stateManager;

            StateDepthLookup = new NativeHashMap<TStateKey, int>(stateCapacity, Allocator.Persistent);
            StateDepthLookup.TryAdd(rootStateKey, 0);

            BinnedStateKeyLookup = new NativeMultiHashMap<int, TStateKey>(1, Allocator.Persistent);
            BinnedStateKeyLookup.Add(rootStateKey.GetHashCode(), rootStateKey);

            PolicyGraph = new PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo>(stateCapacity, actionCapacity);
            PolicyGraph.StateInfoLookup.TryAdd(rootStateKey, default);
        }

        public void UpdateRootState(TStateKey stateKey)
        {
            RootStateKey = stateKey;

            // If it's not in the plan, add it to the binned keys, state info lookup, and depth map
            if (!PolicyGraph.StateInfoLookup.TryAdd(stateKey, default))
            {
                BinnedStateKeyLookup.Add(stateKey.GetHashCode(), stateKey);
                StateDepthLookup.TryAdd(stateKey, 0);
            }
        }

        public void RegisterRoot(TStateKey stateKey)
        {
            if (!RootStateKey.Equals(default))
            {
                Debug.LogError("Root state has already been registered.");
                return;
            }

            RootStateKey = stateKey;
        }

        public bool RootsConverged(float tolerance)
        {
            if (!PolicyGraph.StateInfoLookup.TryGetValue(RootStateKey, out var rootInfo))
            {
                Debug.LogError("Root state does not exist in policy graph.");
                return false;
            }

            return rootInfo.PolicyValue.Range <= tolerance;
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

        public void Prune()
        {
            var minimumReachableDepthMap = PolicyGraph.GetReachableDepthMap(RootStateKey, Allocator.Temp);
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

        public void Reset(TStateKey newRootKey)
        {
            RootStateKey = newRootKey;
            StateDepthLookup.Clear();
            BinnedStateKeyLookup.Clear();
            PolicyGraph.Dispose();
            PolicyGraph = new PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo>(1, 1);
            PolicyGraph.StateInfoLookup.TryAdd(RootStateKey, default);
        }

        public TStateKey GetNextState(TStateKey stateKey, TActionKey actionKey)
        {
            return PolicyGraph.ResultingStateLookup.TryGetFirstValue(new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey), out var resultKey, out _) ? resultKey : default;
        }

        ~SearchContext()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (m_StateManager != null)
            {
                using (var stateKeys = PolicyGraph.StateInfoLookup.GetKeyArray(Allocator.TempJob))
                {
                    foreach (var stateKey in stateKeys)
                    {
                        m_StateManager.DestroyState(stateKey);
                    }
                }

                m_StateManager = default;
            }

            if (StateDepthLookup.IsCreated)
                StateDepthLookup.Dispose();

            if (BinnedStateKeyLookup.IsCreated)
                BinnedStateKeyLookup.Dispose();

            PolicyGraph.Dispose();
        }
    }
}
