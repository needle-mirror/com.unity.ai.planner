using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.AI.Planner
{
    class PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> : IPlan
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
    {
        /// <inheritdoc cref="IPlan"/>
        public int Size {
            get
            {
                m_SearchContext.CompletePlanningJobs();
                return m_SearchContext.PolicyGraph.StateInfoLookup.IsCreated ?
                    m_SearchContext.PolicyGraph.StateInfoLookup.Count() : 0;
            }
        }

        /// <inheritdoc cref="IPlan"/>
        public int MaxPlanDepth
        {
            get
            {
                m_SearchContext.CompletePlanningJobs();
                var depth = 0;
                using (var depths = m_SearchContext.StateDepthLookup.GetValueArray(Allocator.Temp))
                {
                    for (int i = 0; i < depths.Length; i++)
                    {
                        depth = math.max(depth, depths[i]);
                    }
                }
                return depth + 1; // add 1 to account for unexpanded states, which are not in the depth lookup yet
            }
        }

        /// <inheritdoc cref="IPlan"/>
        public IStateKey RootStateKey => m_SearchContext.RootStateKey as IStateKey;

        internal SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> m_SearchContext;


        internal PlanWrapper(SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> searchContext)
        {
            m_SearchContext = searchContext;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetEquivalentPlanState(IStateKey stateKey, out IStateKey matchingPlanStateKey)
        {
            m_SearchContext.CompletePlanningJobs();
            bool found = m_SearchContext.FindMatchingStateInPlan(Convert(stateKey), out var matchingKey);
            matchingPlanStateKey = matchingKey as IStateKey;
            return found;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool IsTerminal(IStateKey planStateKey)
        {
            return IsTerminal(Convert(planStateKey));
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetStateInfo(IStateKey planStateKey, out StateInfo stateInfo)
        {
            return TryGetStateInfo(Convert(planStateKey), out stateInfo);
        }

        /// <inheritdoc cref="IPlan"/>
        public IStateData GetStateData(IStateKey stateKey)
        {
            return GetStateData(Convert(stateKey));
        }

        /// <inheritdoc cref="IPlan"/>
        public int GetActions(IStateKey planStateKey, IList<IActionKey> actionKeys)
        {
            m_SearchContext.CompletePlanningJobs();
            actionKeys?.Clear();

            int count = 0;
            var stateActionLookup = m_SearchContext.PolicyGraph.ActionLookup;
            if (stateActionLookup.TryGetFirstValue(Convert(planStateKey), out var actionKey, out var iterator))
            {
                do
                {
                    actionKeys?.Add(actionKey as IActionKey);
                    count++;
                } while (stateActionLookup.TryGetNextValue(out actionKey, ref iterator));
            }

            return count;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetOptimalAction(IStateKey planStateKey, out IActionKey actionKey)
        {
            m_SearchContext.CompletePlanningJobs();
            var found = m_SearchContext.PolicyGraph.TryGetOptimalAction(Convert(planStateKey), out var actionKeyTyped);
            actionKey = actionKeyTyped as IActionKey;
            return found;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetActionInfo(IStateKey planStateKey, IActionKey actionKey, out ActionInfo actionInfo)
        {
            return TryGetActionInfo(Convert(planStateKey), Convert(actionKey), out actionInfo);
        }

        /// <inheritdoc cref="IPlan"/>
        public int GetResultingStates(IStateKey planStateKey, IActionKey actionKey, IList<IStateKey> resultingPlanStateKeys)
        {
            m_SearchContext.CompletePlanningJobs();
            resultingPlanStateKeys?.Clear();

            var count = 0;
            var stateActionPair = new StateActionPair<TStateKey, TActionKey>(Convert(planStateKey), Convert(actionKey));
            var resultingStateLookup = m_SearchContext.PolicyGraph.ResultingStateLookup;
            if (resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingState, out var iterator))
            {
                do
                {
                    resultingPlanStateKeys?.Add(resultingState as IStateKey);
                    count++;
                } while (resultingStateLookup.TryGetNextValue(out resultingState, ref iterator));
            }

            return count;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetStateTransitionInfo(IStateKey originatingPlanStateKey, IActionKey actionKey, IStateKey resultingPlanStateKey, out StateTransitionInfo stateTransitionInfo)
        {
            return TryGetStateTransitionInfo(Convert(originatingPlanStateKey), Convert(actionKey), Convert(resultingPlanStateKey), out stateTransitionInfo);
        }


        /// <inheritdoc cref="IPlan"/>
        public bool TryGetEquivalentPlanState(TStateKey stateKey, out TStateKey matchingPlanStateKey)
        {
            m_SearchContext.CompletePlanningJobs();
            bool found = m_SearchContext.FindMatchingStateInPlan(stateKey, out var matchingKey);
            matchingPlanStateKey = matchingKey;
            return found;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool IsTerminal(TStateKey planStateKey)
        {
            m_SearchContext.CompletePlanningJobs();
            if (!m_SearchContext.PolicyGraph.StateInfoLookup.ContainsKey(planStateKey))
                throw new ArgumentException($"State key {planStateKey} does not exist in the plan.");

            return m_SearchContext.IsTerminal(planStateKey);
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetStateInfo(TStateKey planStateKey, out StateInfo stateInfo)
        {
            m_SearchContext.CompletePlanningJobs();
            return m_SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(planStateKey, out stateInfo);
        }

        /// <inheritdoc cref="IPlan"/>
        public IStateData GetStateData(TStateKey planStateKey)
        {
            m_SearchContext.CompletePlanningJobs();
            return m_SearchContext.m_StateManager?.GetStateData(planStateKey, readWrite: false) as IStateData;
        }

        /// <inheritdoc cref="IPlan"/>
        public int GetActions(TStateKey planStateKey, IList<TActionKey> actionKeys)
        {
            m_SearchContext.CompletePlanningJobs();
            actionKeys?.Clear();

            int count = 0;
            var stateActionLookup = m_SearchContext.PolicyGraph.ActionLookup;
            if (stateActionLookup.TryGetFirstValue(planStateKey, out var actionKey, out var iterator))
            {
                do
                {
                    actionKeys?.Add(actionKey);
                    count++;
                } while (stateActionLookup.TryGetNextValue(out actionKey, ref iterator));
            }

            return count;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetOptimalAction(TStateKey planStateKey, out TActionKey actionKey)
        {
            m_SearchContext.CompletePlanningJobs();
            var found = m_SearchContext.PolicyGraph.TryGetOptimalAction(planStateKey, out var actionKeyTyped);
            actionKey = actionKeyTyped;
            return found;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetActionInfo(TStateKey planStateKey, TActionKey actionKey, out ActionInfo actionInfo)
        {
            m_SearchContext.CompletePlanningJobs();
            return m_SearchContext.PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<TStateKey, TActionKey>(planStateKey, actionKey), out actionInfo);
        }

        /// <inheritdoc cref="IPlan"/>
        public int GetResultingStates(TStateKey planStateKey, TActionKey actionKey, IList<TStateKey> resultingPlanStateKeys)
        {
            m_SearchContext.CompletePlanningJobs();
            resultingPlanStateKeys?.Clear();

            var count = 0;
            var stateActionPair = new StateActionPair<TStateKey, TActionKey>(planStateKey, actionKey);
            var resultingStateLookup = m_SearchContext.PolicyGraph.ResultingStateLookup;
            if (resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingState, out var iterator))
            {
                do
                {
                    resultingPlanStateKeys?.Add(resultingState);
                    count++;
                } while (resultingStateLookup.TryGetNextValue(out resultingState, ref iterator));
            }

            return count;
        }

        /// <inheritdoc cref="IPlan"/>
        public bool TryGetStateTransitionInfo(TStateKey originatingPlanStateKey, TActionKey actionKey, TStateKey resultingPlanStateKey, out StateTransitionInfo stateTransitionInfo)
        {
            m_SearchContext.CompletePlanningJobs();
            var stateTransition = new StateTransition<TStateKey, TActionKey>(originatingPlanStateKey, actionKey,resultingPlanStateKey);
            return m_SearchContext.PolicyGraph.StateTransitionInfoLookup.TryGetValue(stateTransition, out stateTransitionInfo);
        }

        TStateKey Convert(IStateKey stateKey)
        {
            if (stateKey is TStateKey converted)
                return converted;

            throw new ArgumentException($"Expected state key of type {typeof(TStateKey)}. Received key of type {stateKey?.GetType()}.");
        }

        TActionKey Convert(IActionKey actionKey)
        {
            if (actionKey is TActionKey converted)
                return converted;

            throw new ArgumentException($"Expected action key of type {typeof(TActionKey)}. Received key of type {actionKey?.GetType()}.");
        }
    }
}
