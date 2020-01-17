using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.AI.Planner.Controller;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    abstract class BasePlanExecutor<TObject, TStateKey, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TStateManager, TActionKey, TDestroyStatesScheduler> : IPlanExecutor
        where TObject : struct, ITraitBasedObject
        where TStateKey : struct, IEquatable<TStateKey>, IStateKey
        where TStateData : struct, ITraitBasedStateData<TObject, TStateData>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
        where TActionScheduler : struct, ITraitBasedActionScheduler<TObject, TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey>
        where THeuristic : struct, IHeuristic<TStateData>
        where TTerminationEvaluator : struct, ITerminationEvaluator<TStateData>
        where TStateManager : JobComponentSystem, ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext>
        where TActionKey : struct, IEquatable<TActionKey>, IActionKeyWithGuid
        where TDestroyStatesScheduler : struct, IDestroyStatesScheduler<TStateKey, TStateData, TStateDataContext, TStateManager>
    {
        class DecisionRuntimeInfo
        {
            public float StartTimestamp;
            public DecisionRuntimeInfo() => Reset();
            public void Reset() => StartTimestamp = Time.time;
        }

        class PlanWrapper : IPlan
        {
            PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> m_PolicyGraph;

            internal PlanWrapper(PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> policyGraph)
            {
                m_PolicyGraph = policyGraph;
            }

            public int Size
            {
                get
                {
                    var lookup = m_PolicyGraph.StateInfoLookup;
                    return lookup.IsCreated ? lookup.Length : 0;
                }
            }

            public bool TryGetOptimalAction(IStateKey stateKey, out IActionKey actionKey)
            {
                var found = m_PolicyGraph.GetOptimalAction((TStateKey)stateKey, out var actionKeyGeneric);
                actionKey = actionKeyGeneric;
                return found;
            }

            public bool TryGetActionInfo(IStateKey stateKey, IActionKey actionKey, out ActionInfo actionInfo)
            {
                return m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<TStateKey, TActionKey>((TStateKey)stateKey, (TActionKey)actionKey), out actionInfo);
            }

            public int GetActions(IStateKey stateKey, List<IActionKey> actionKeys)
            {
                actionKeys?.Clear();

                int count = 0;
                var stateActionLookup = m_PolicyGraph.ActionLookup;
                if (stateActionLookup.TryGetFirstValue((TStateKey)stateKey, out var actionKey, out var iterator))
                {
                    do
                    {
                        if (actionKeys != null)
                            actionKeys.Add(actionKey);

                        count++;
                    } while (stateActionLookup.TryGetNextValue(out actionKey, ref iterator));
                }

                return count;
            }

            public int GetResultingStates(IStateKey stateKey, IActionKey actionKey, List<IStateKey> stateTransitions)
            {
                stateTransitions?.Clear();

                var count = 0;
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>((TStateKey)stateKey, (TActionKey)actionKey);
                var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;

                if (!resultingStateLookup.IsCreated)
                    return 0;

                if (resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingState, out var iterator))
                {
                    do
                    {
                        stateTransitions?.Add(resultingState);
                        count++;
                    } while (resultingStateLookup.TryGetNextValue(out resultingState, ref iterator));
                }

                return count;
            }

            public bool TryGetStateTransitionInfo(IStateKey originatingStateKey, IActionKey actionKey, IStateKey resultingStateKey, out StateTransitionInfo stateTransitionInfo)
            {
                var stateTransition = new StateTransition<TStateKey, TActionKey>((TStateKey)originatingStateKey, (TActionKey)actionKey, (TStateKey)resultingStateKey);
                return m_PolicyGraph.StateTransitionInfoLookup.TryGetValue(stateTransition, out stateTransitionInfo);
            }

            public bool TryGetStateInfo(IStateKey stateKey, out StateInfo stateInfo)
            {
                return m_PolicyGraph.StateInfoLookup.TryGetValue((TStateKey)stateKey, out stateInfo);
            }
        }

        public bool IsIdle
        {
            get
            {
                if (!CurrentActionKey.Equals(default))
                    return false;

                if (CurrentStateKey.Equals(default)) // If we have no current state, then we're idle (need to update state)
                    return true;

                if (!m_PlannerScheduler.CurrentJobHandle.IsCompleted)
                    return false;

                m_PlannerScheduler.CurrentJobHandle.Complete();
                if (!Plan.TryGetStateInfo(CurrentStateKey, out var stateInfo))
                    return true;

                return stateInfo.SubgraphComplete && Plan.GetActions(CurrentStateKey, null) == 0;
            }
        }

        /// <summary>
        /// The plan the executor is following.
        /// </summary>
        public IPlan Plan => m_PlanWrapper;
        PlanWrapper m_PlanWrapper;

        /// <summary>
        /// The object managing the scheduling of planning jobs.
        /// </summary>
        IPlannerScheduler IPlanExecutor.PlannerScheduler => PlannerScheduler;
        protected PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TDestroyStatesScheduler> PlannerScheduler => m_PlannerScheduler;

        /// <summary>
        /// State key for the current state of the plan.
        /// </summary>
        IStateKey IPlanExecutor.CurrentStateKey => CurrentStateKey;
        protected TStateKey CurrentStateKey { get; private set; }

        /// <summary>
        /// Action key for the current action to be executed.
        /// </summary>
        IActionKey IPlanExecutor.CurrentActionKey => CurrentActionKey;
        protected TActionKey CurrentActionKey { get; set; }

        /// <summary>
        /// Module governing the conversion of game state data to planner state data.
        /// </summary>
        protected PlanningDomainData<TObject, TStateKey, TStateData, TStateDataContext, TStateManager> m_DomainData;

        /// <summary>
        /// Manager for plan state data.
        /// </summary>
        protected TStateManager m_StateManager;

        PlanExecutionSettings m_PlanExecutionSettings;
        DecisionRuntimeInfo m_DecisionRuntimeInfo;
        PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TDestroyStatesScheduler> m_PlannerScheduler;
        SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> SearchContext => m_PlannerScheduler?.SearchContext;

        /// <inheritdoc />
        public void Initialize(string name, PlanDefinition planDefinition, IEnumerable<ITraitBasedObjectData> initialTraitBasedObjects, PlanExecutionSettings settings = null)
        {
            m_DomainData = new PlanningDomainData<TObject, TStateKey, TStateData, TStateDataContext, TStateManager>(planDefinition);
            m_DecisionRuntimeInfo = new DecisionRuntimeInfo();
            m_PlanExecutionSettings = settings != null ? settings : new PlanExecutionSettings();

            InitializePlannerSystems(name, initialTraitBasedObjects, planDefinition);

            // todo fixme: Will move teardown to JobComponentSystem running scheduler in a future update.
            RegisterOnDestroyCallback();
        }

        void InitializePlannerSystems(string name, IEnumerable<ITraitBasedObjectData> traitBasedObjects, PlanDefinition planDefinition)
        {
            var world = new World(name);
            var entityManager = world.EntityManager;

            m_StateManager = world.GetOrCreateSystem<TStateManager>();

            var initialStateData = m_DomainData.CreateStateData(entityManager, traitBasedObjects);
            var initialStateKey = m_StateManager.GetStateDataKey(initialStateData);

            m_PlannerScheduler = new PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TDestroyStatesScheduler>();
            m_PlannerScheduler.Initialize(initialStateKey, m_StateManager, new THeuristic(), new TTerminationEvaluator(), discountFactor: planDefinition.DiscountFactor);

            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world, ScriptBehaviourUpdateOrder.CurrentPlayerLoop);

            var controllerState = m_StateManager.CopyStateData(initialStateData);
            CurrentStateKey = m_StateManager.GetStateDataKey(controllerState);

            m_PlanWrapper = new PlanWrapper(m_PlannerScheduler.SearchContext.PolicyGraph);
        }

        /// <inheritdoc />
        public virtual bool ReadyToAct()
        {
            var planningHandle = m_PlannerScheduler.CurrentJobHandle;
            if (!planningHandle.IsCompleted)
                return false;

            planningHandle.Complete();
            if (!NextActionAvailable())
                return false;

            var rootStateInfo = m_PlannerScheduler.SearchContext.PolicyGraph.StateInfoLookup[m_PlannerScheduler.SearchContext.RootStateKey];
            switch (m_PlanExecutionSettings.ExecutionMode)
            {
                case PlanExecutionSettings.PlanExecutionMode.ActImmediately:
                    return true;

                case PlanExecutionSettings.PlanExecutionMode.WaitForActMethodCall:
                    return false;

                case PlanExecutionSettings.PlanExecutionMode.WaitForPlanCompletion:
                    return rootStateInfo.SubgraphComplete;

                case PlanExecutionSettings.PlanExecutionMode.WaitForMaximumDecisionTolerance:
                    return rootStateInfo.PolicyValue.Range <= m_PlanExecutionSettings.MaximumDecisionTolerance;

                case PlanExecutionSettings.PlanExecutionMode.WaitForMinimumPlanSize:
                    return m_PlanWrapper.Size >= m_PlanExecutionSettings.MinimumPlanSize || rootStateInfo.SubgraphComplete;

                case PlanExecutionSettings.PlanExecutionMode.WaitForMinimumSearchTime:
                    return Time.time - m_DecisionRuntimeInfo.StartTimestamp >= m_PlanExecutionSettings.MinimumSearchTime;
            }

            return false;
        }

        /// <inheritdoc />
        public abstract void Act(DecisionController controller);

        protected abstract void RegisterOnDestroyCallback();

        /// <inheritdoc />
        public void AdvancePlanWithPredictedState()
        {
            m_PlannerScheduler.CurrentJobHandle.Complete();
            CurrentStateKey = m_PlannerScheduler.SearchContext.GetNextState(SearchContext.RootStateKey, CurrentActionKey);

            SearchContext.UpdateRootState(CurrentStateKey);
            SearchContext.DecrementSearchDepths();
            SearchContext.Prune();

            m_DecisionRuntimeInfo.Reset();
            CurrentActionKey = default;
        }

        /// <inheritdoc />
        public void AdvancePlanWithNewState(IEnumerable<ITraitBasedObjectData> traitBasedObjects)
        {
            m_PlannerScheduler.CurrentJobHandle.Complete();
            var stateData = m_DomainData.CreateStateData(m_StateManager.EntityManager, traitBasedObjects); // Ids are maintained
            var stateKey = m_StateManager.GetStateDataKey(stateData);

            if (FindMatchingStateInPlan(stateKey, out var planStateKey))
            {
                m_StateManager.DestroyState(stateKey);
                CurrentStateKey = planStateKey;
                SearchContext.DecrementSearchDepths();
            }
            else
            {
                CurrentStateKey = stateKey;
            }

            SearchContext.UpdateRootState(CurrentStateKey);
            SearchContext.Prune();

            m_DecisionRuntimeInfo.Reset();
            CurrentActionKey = default;
        }

        protected ObjectCorrespondence m_PlanStateToGameStateIdLookup = new ObjectCorrespondence(1, Allocator.Persistent);

        bool FindMatchingStateInPlan(TStateKey stateKey, out TStateKey planStateKey)
        {
            var stateData = m_StateManager.GetStateData(stateKey, false);
            var stateHashCode = stateKey.GetHashCode();

            if (SearchContext.BinnedStateKeyLookup.TryGetFirstValue(stateHashCode, out planStateKey, out var iterator))
            {
                do
                {
                    var planStateData = m_StateManager.GetStateData(planStateKey, false);
                    if (planStateData.TryGetObjectMapping(stateData, m_PlanStateToGameStateIdLookup))
                        return true;
                } while (SearchContext.BinnedStateKeyLookup.TryGetNextValue(out planStateKey, ref iterator));
            }

            // Clear data from last correspondence attempt.
            m_PlanStateToGameStateIdLookup.Clear();
            return false;
        }

        /// <summary>
        /// Returns the optimal action for the current root of the plan.
        /// </summary>
        /// <returns>Returns the optimal action for the current root of the plan.</returns>
        public TActionKey GetBestAction()
        {
            return SearchContext.PolicyGraph.GetOptimalAction(SearchContext.RootStateKey, out var actionKey) ? actionKey : default;
        }

        /// <inheritdoc />
        public IStateData GetCurrentStateData(bool readWrite = false)
        {
            return m_StateManager.GetStateData(CurrentStateKey, readWrite);
        }

        /// <inheritdoc />
        public abstract string GetActionName(IActionKey actionKey);

        public string GetStateString(IStateKey stateKey)
        {
            var stateData = m_StateManager.GetStateData((TStateKey)stateKey, false);
            return stateData.ToString();
        }

        /// <summary>
        /// Returns true if the plan has an action available for the current root state. Returns false otherwise.
        /// </summary>
        /// <returns>Returns true if the plan has an action available for the current root state. Returns false otherwise.</returns>
        public bool NextActionAvailable()
        {
            return SearchContext != null &&
                SearchContext.PolicyGraph.GetOptimalAction(SearchContext.RootStateKey, out _);
        }

        /// <inheritdoct />
        public int MaxPlanDepthFromCurrentState()
        {
            m_PlannerScheduler.CurrentJobHandle.Complete();

            var depth = 0;
            var depths = SearchContext.StateDepthLookup.GetValueArray(Allocator.Temp);
            for (int i = 0; i < depths.Length; i++)
            {
                depth = math.max(depth, depths[i]);
            }

            depths.Dispose();

            return depth;
        }

        /// <inheritdoc />
        public void Destroy()
        {
            m_PlannerScheduler?.CurrentJobHandle.Complete();
            if (m_DomainData is IDisposable disposable)
                disposable.Dispose();

            m_PlannerScheduler?.Dispose();
            m_PlanStateToGameStateIdLookup.Dispose();
        }
    }
}
