using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.AI.Planner.Controller;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    abstract class BasePlanExecutor<TObject, TStateKey, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TStateManager, TActionKey> : IPlanExecutor
        where TObject : struct, ITraitBasedObject
        where TStateKey : struct, IEquatable<TStateKey>, IStateKey
        where TStateData : struct, ITraitBasedStateData<TObject, TStateData>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
        where TActionScheduler : ITraitBasedActionScheduler<TObject, TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey, StateTransitionInfo>, IGetActionName, new()
        where THeuristic : struct, IHeuristic<TStateData>
        where TTerminationEvaluator : struct, ITerminationEvaluator<TStateData>
        where TStateManager : JobComponentSystem, ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext>
        where TActionKey : struct, IEquatable<TActionKey>, IActionKeyWithGuid
    {
        class DecisionRuntimeInfo
        {
            public float StartTimestamp;
            public DecisionRuntimeInfo() => Reset();
            public void Reset() => StartTimestamp = Time.time;
        }

        class PlanExecutionInfo : IPlanExecutionInfo
        {
            SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> m_SearchContext;
            TStateManager m_StateManager;
            TActionScheduler m_ActionScheduler;
            IPlanExecutor m_PlanExecutor;

            internal PlanExecutionInfo(SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> searchContext,
                TStateManager stateManager,
                TActionScheduler actionScheduler,
                IPlanExecutor planExecutor)
            {
                m_SearchContext = searchContext;
                m_StateManager = stateManager;
                m_ActionScheduler = actionScheduler;
                m_PlanExecutor = planExecutor;
            }

            public int Size
            {
                get
                {
                    var lookup = m_SearchContext.PolicyGraph.StateInfoLookup;
                    return lookup.IsCreated ? lookup.Length : 0;
                }
            }

            public bool PlanExists => m_SearchContext.PolicyGraph.StateInfoLookup.IsCreated;

            public IStateManagerInternal StateManager => m_StateManager as IStateManagerInternal;

            public IStateKey RootStateKey => m_SearchContext.RootStateKey;

            public int MaxHorizonFromRoot
            {
                get
                {
                    var maxDepth = 0;
                    if (!m_SearchContext.StateDepthLookup.IsCreated)
                        return maxDepth;

                    using (var depths = m_SearchContext.StateDepthLookup.GetValueArray(Allocator.TempJob))
                    {
                        foreach (var depth in depths)
                        {
                            maxDepth = Math.Max(depth, maxDepth);
                        }
                    }

                    return maxDepth;
                }
            }

            public IActionKey CurrentAction
            {
                get
                {
                    var currentAction = m_PlanExecutor.CurrentActionKey;
                    return currentAction.Equals(default) ? null : currentAction;
                }
            }

            public bool GetOptimalAction(IStateKey stateKey, out IActionKey actionKey)
            {
                var found = m_SearchContext.Plan.GetOptimalAction((TStateKey)stateKey, out var actionKeyGeneric);
                actionKey = actionKeyGeneric;
                return found;
            }

            public string GetActionName(IActionKey actionKey)
            {
                return m_ActionScheduler.GetActionName(actionKey);
            }

            public ActionInfo GetActionInfo((IStateKey, IActionKey) stateActionKey)
            {
                m_SearchContext.PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<TStateKey, TActionKey>((TStateKey)stateActionKey.Item1, (TActionKey)stateActionKey.Item2),  out var actionInfo);
                return actionInfo;
            }

            public int GetActions(IStateKey stateKey, List<IActionKey> actionKeys)
            {
                actionKeys?.Clear();

                int count = 0;
                var stateActionLookup = m_SearchContext.PolicyGraph.ActionLookup;
                if (stateActionLookup.TryGetFirstValue((TStateKey)stateKey, out var actionKey, out var iterator))
                {
                    do
                    {
                        if (actionKeys != null)
                        {
                            actionKeys.Add(actionKey);
                        }
                        count++;
                    } while (stateActionLookup.TryGetNextValue(out actionKey, ref iterator));
                }

                return count;
            }

            public int GetActionResults((IStateKey, IActionKey) stateActionKey, List<(StateTransitionInfo, IStateKey)> actionResults)
            {
                actionResults?.Clear();

                var count = 0;
                var genericStateActionKey = ((TStateKey, TActionKey))stateActionKey;
                var stateActionPair = new StateActionPair<TStateKey, TActionKey> { StateKey = genericStateActionKey.Item1, ActionKey = genericStateActionKey.Item2 };
                var policyGraph = m_SearchContext.PolicyGraph;
                var resultingStateLookup = policyGraph.ResultingStateLookup;

                if (!resultingStateLookup.IsCreated)
                    return 0;

                if (resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingState, out var iterator))
                {
                    do
                    {
                        if (actionResults != null)
                        {
                            var stateTransition = new StateTransition<TStateKey, TActionKey>(stateActionPair, resultingState);
                            if (policyGraph.StateTransitionInfoLookup.TryGetValue(stateTransition, out var actionResult))
                            {
                                actionResults.Add((actionResult, resultingState));
                            }
                        }

                        count++;
                    } while (resultingStateLookup.TryGetNextValue(out resultingState, ref iterator));
                }

                return count;
            }

            public StateInfo GetStateInfo(IStateKey stateKey)
            {
                m_SearchContext.PolicyGraph.StateInfoLookup.TryGetValue((TStateKey)stateKey, out var stateInfo);
                return stateInfo;
            }
        }

        /// <summary>
        /// The object managing the scheduling of planning jobs.
        /// </summary>
        public PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator> PlannerScheduler => m_PlannerScheduler;
        IPlannerScheduler IPlanExecutor.PlannerScheduler => m_PlannerScheduler;

        /// <summary>
        /// Module governing the conversion of game state data to planner state data.
        /// </summary>
        protected PlanningDomainData<TObject, TStateKey, TStateData, TStateDataContext, TStateManager> m_DomainData;

        /// <summary>
        /// Manager for plan state data.
        /// </summary>
        protected TStateManager m_StateManager;

        /// <summary>
        /// State of the search process operating on the plan.
        /// </summary>
        protected SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> SearchContext => m_PlannerScheduler?.m_SearchContext;

        /// <summary>
        /// State key for the current state of the plan.
        /// </summary>
        protected TStateKey CurrentStateKey { get; private set; }

        /// <summary>
        /// Action key for the current action to be executed.
        /// </summary>
        protected TActionKey CurrentActionKey { get; set; }
        IActionKey IPlanExecutor.CurrentActionKey => CurrentActionKey;

        PlanExecutionSettings m_PlanExecutionSettings;
        DecisionRuntimeInfo m_DecisionRuntimeInfo;
        PlanExecutionInfo m_PlanExecutionInfo; // Used Implicitly

        PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator> m_PlannerScheduler;
        TActionScheduler m_ActionScheduler;

        /// <inheritdoc />
        public void Initialize(string name, PlanDefinition planDefinition, IEnumerable<ITraitBasedObjectData> initialTraitBasedObjects, PlanExecutionSettings settings = null)
        {
            m_DomainData = new PlanningDomainData<TObject, TStateKey, TStateData, TStateDataContext, TStateManager>(planDefinition);
            m_DecisionRuntimeInfo = new DecisionRuntimeInfo();
            m_PlanExecutionSettings = settings != null ? settings : new PlanExecutionSettings();

            InitializePlannerSystems(name, initialTraitBasedObjects, planDefinition);
        }

        void InitializePlannerSystems(string name, IEnumerable<ITraitBasedObjectData> traitBasedObjects, PlanDefinition planDefinition)
        {
            var world = new World(name);
            var entityManager = world.EntityManager;

            m_StateManager = world.GetOrCreateSystem<TStateManager>();
            m_ActionScheduler = new TActionScheduler();

            var initialStateData = m_DomainData.CreateStateData(entityManager, traitBasedObjects);
            var initialStateKey = m_StateManager.GetStateDataKey(initialStateData);

            m_PlannerScheduler = new PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator>();
            m_PlannerScheduler.Initialize(initialStateKey, m_StateManager, m_ActionScheduler, new THeuristic(), new TTerminationEvaluator(), discountFactor: planDefinition.DiscountFactor);

            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);

            var controllerState = m_StateManager.CopyStateData(initialStateData);
            CurrentStateKey = m_StateManager.GetStateDataKey(controllerState);

            m_PlanExecutionInfo = new PlanExecutionInfo(m_PlannerScheduler.m_SearchContext, m_StateManager, m_ActionScheduler, this);
        }

        /// <inheritdoc />
        public virtual bool ReadyToAct()
        {
            if (!NextActionAvailable())
                return false;

            var rootStateInfo = m_PlannerScheduler.m_SearchContext.PolicyGraph.StateInfoLookup[m_PlannerScheduler.m_SearchContext.RootStateKey];
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
                    return SearchContext.Plan.Size >= m_PlanExecutionSettings.MinimumPlanSize || rootStateInfo.SubgraphComplete;

                case PlanExecutionSettings.PlanExecutionMode.WaitForMinimumSearchTime:
                    return Time.time - m_DecisionRuntimeInfo.StartTimestamp >= m_PlanExecutionSettings.MinimumSearchTime;
            }

            return false;
        }

        /// <inheritdoc />
        public abstract void Act(DecisionController controller);

        public void UpdateState(IEnumerable<ITraitBasedObjectData> traitBasedObjects)
        {
            var newStateData = m_DomainData.CreateStateData(m_StateManager.EntityManager, traitBasedObjects); // Ids are maintained
            var newStateKey = m_StateManager.GetStateDataKey(newStateData);

            var currentStateData = m_StateManager.GetStateData(CurrentStateKey, false);
            if (currentStateData.Equals(newStateData))
            {
                m_StateManager.DestroyState(newStateKey);
                return;
            }

            if (FindMatchingStateInPlan(newStateKey, out var planStateKey))
            {
                m_StateManager.DestroyState(newStateKey);
                CurrentStateKey = planStateKey;
            }
            else
            {
                CurrentStateKey = newStateKey;
            }

            SearchContext.UpdateRootState(CurrentStateKey);
            SearchContext.Prune();
        }

        /// <inheritdoc />
        public void AdvancePlanWithPredictedState()
        {
            CurrentStateKey = m_PlannerScheduler.m_SearchContext.GetNextState(SearchContext.RootStateKey, CurrentActionKey);

            SearchContext.UpdateRootState(CurrentStateKey);
            SearchContext.DecrementSearchDepths();
            SearchContext.Prune();

            m_DecisionRuntimeInfo.Reset();
            CurrentActionKey = default;
        }

        /// <inheritdoc />
        public void AdvancePlanWithNewState(IEnumerable<ITraitBasedObjectData> traitBasedObjects)
        {
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
            return !SearchContext.Plan.GetOptimalAction(SearchContext.RootStateKey, out var actionKey) ? default : actionKey;
        }

        /// <inheritdoc />
        public IStateData GetCurrentState(bool readWrite = false)
        {
            return m_StateManager.GetStateData(CurrentStateKey, readWrite);
        }

        /// <summary>
        /// Returns true if the plan has an action available for the current root state. Returns false otherwise.
        /// </summary>
        /// <returns>Returns true if the plan has an action available for the current root state. Returns false otherwise.</returns>
        public bool NextActionAvailable()
        {
            return SearchContext.Plan.GetOptimalAction(SearchContext.RootStateKey, out _);
        }

        /// <inheritdoc />
        public void Destroy()
        {
            if (m_DomainData is IDisposable disposable)
                disposable.Dispose();

            m_PlannerScheduler?.Dispose();
            m_PlanStateToGameStateIdLookup.Dispose();
        }
    }
}
