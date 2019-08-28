using System;
using Unity.Collections;

namespace Unity.AI.Planner
{
    class SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> : IPlan<TStateKey, TActionKey>
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
    {
        // Info for search
        public TStateKey RootStateKey { get; set; }
        public NativeHashMap<TStateKey, int> StateDepthLookup;
        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> PolicyGraph;

        TStateManager m_StateManager;

        public SearchContext() { }

        public SearchContext(TStateKey rootStateKey, TStateManager stateManager, int stateCapacity = 1, int actionCapacity = 1)
        {
            RootStateKey = rootStateKey;
            m_StateManager = stateManager;
            StateDepthLookup = new NativeHashMap<TStateKey, int>(stateCapacity, Allocator.Persistent);
            PolicyGraph = new PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult>(stateCapacity, actionCapacity);
            PolicyGraph.StateInfoLookup.TryAdd(rootStateKey, new StateInfo { VisitCount = 1 });
        }

        ~SearchContext()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (StateDepthLookup.IsCreated)
                StateDepthLookup.Dispose();

            if (m_StateManager != default)
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

            PolicyGraph.Dispose();
        }

        public bool GetOptimalAction(TStateKey stateKey, out TActionKey action)
        {
            bool actionsFound = PolicyGraph.StateActionLookup.TryGetFirstValue(stateKey, out var stateActionPair, out var iterator);
            if (!actionsFound)
            {
                action = default;
                return false;
            }

            var maxActionValuePair = (default(TActionKey), float.MinValue);

            do
            {
                PolicyGraph.ActionInfoLookup.TryGetValue(stateActionPair, out var actionInfo);
                if (actionInfo.ActionValue > maxActionValuePair.Item2)
                    maxActionValuePair = (stateActionPair.Item2, actionInfo.ActionValue);

            } while (PolicyGraph.StateActionLookup.TryGetNextValue(out stateActionPair, ref iterator));

            action = maxActionValuePair.Item1;
            return true;
        }

        public NativeArray<TStateKey> GetStateKeys(Allocator allocator)
        {
            return PolicyGraph.StateInfoLookup.GetKeyArray(allocator);
        }

        public void UpdatePlan(TStateKey stateKey)
        {
            var copyKey = m_StateManager.CopyState(stateKey);
            var stateData = m_StateManager.GetStateData(copyKey, false);
            var stateHashCode = copyKey.GetHashCode();

            using (var planStateKeys = GetStateKeys(Allocator.Temp))
            {
                foreach (var planStateKey in planStateKeys)
                {
                    if (stateHashCode == planStateKey.GetHashCode() &&
                       m_StateManager.Equals(stateData, m_StateManager.GetStateData(planStateKey, false)))
                    {
                        RootStateKey = planStateKey;
                        m_StateManager.DestroyState(copyKey);
                        DecrementSearchDepths();
                        Prune();
                        return;
                    }
                }
            }

            UpdateRoot(copyKey);
        }

        void UpdateRoot(TStateKey stateKey)
        {
            PolicyGraph.StateInfoLookup.TryAdd(stateKey, new StateInfo { VisitCount = 1 });
            RootStateKey = stateKey;
            DecrementSearchDepths();
            Prune();
        }

        void DecrementSearchDepths()
        {
            // Decrement search depths
            using (var stateKeyArray = StateDepthLookup.GetKeyArray(Allocator.Temp))
            {
                foreach (var stateKey in stateKeyArray)
                {
                    int depth = StateDepthLookup[stateKey] - 1;

                    if (depth >= 0)
                        StateDepthLookup[stateKey] = depth;
                    else
                        StateDepthLookup.Remove(stateKey);
                }
            }
        }

        void Prune()
        {
            var minimumReachableDepthMap = PolicyGraph.GetReachableDepthMap(RootStateKey);
            var stateKeyArray = PolicyGraph.StateInfoLookup.GetKeyArray(Allocator.Temp);

            foreach (var stateKey in stateKeyArray)
            {
                if (!minimumReachableDepthMap.TryGetValue(stateKey, out _))
                {
                    PolicyGraph.RemoveState(stateKey);
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
            PolicyGraph.Dispose();
            PolicyGraph = new PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult>(1, 1);
            PolicyGraph.StateInfoLookup.TryAdd(RootStateKey, new StateInfo { VisitCount = 1 });
        }

        public TStateKey GetNextState(TStateKey stateKey, TActionKey actionKey)
        {
            return PolicyGraph.ResultingStateLookup.TryGetFirstValue((stateKey, actionKey), out var resultKey, out _) ? resultKey : default;
        }
    }
}
