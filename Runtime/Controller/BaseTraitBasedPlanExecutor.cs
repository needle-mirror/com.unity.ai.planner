using System;
using System.Collections;
using Unity.AI.Planner;
using Unity.AI.Planner.Controller;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Assertions;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    abstract class BaseTraitBasedPlanExecutor<TObject, TStateKey, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TStateManager, TActionKey, TDestroyStatesScheduler> : ITraitBasedPlanExecutor
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

        internal struct ActionParameterInfo : IActionParameterInfo
        {
            public string ParameterName { get; set; }
            public string TraitObjectName { get; set; }

            public ObjectId TraitObjectId { get; set; }
        }

        /// <summary>
        /// Status of the plan executor.
        /// </summary>
        public PlanExecutionStatus Status { get; private set; } = PlanExecutionStatus.AwaitingPlan;

        /// <summary>
        /// The plan the executor is following.
        /// </summary>
        IPlan IPlanExecutor.Plan => m_PlanWrapper;
        protected PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> m_PlanWrapper;

        /// <summary>
        /// State key for the current state of the plan executor.
        /// </summary>
        IStateKey IPlanExecutor.CurrentExecutorStateKey => CurrentExecutorState;
        protected TStateKey CurrentExecutorState { get; private set; }

        IStateKey IPlanExecutor.CurrentPlanStateKey => CurrentPlanState;
        protected TStateKey CurrentPlanState { get; private set;}

        /// <summary>
        /// State data for the current state of the plan executor.
        /// </summary>
        IStateData IPlanExecutor.CurrentStateData => CurrentStateData;
        protected TStateData CurrentStateData => m_StateManager.GetStateData(CurrentExecutorState, false);

        /// <summary>
        /// Action key for the current action being executed.
        /// </summary>
        IActionKey IPlanExecutor.CurrentActionKey => CurrentActionKey;
        protected TActionKey CurrentActionKey { get; set; }

        /// <summary>
        /// The object managing the scheduling of planning jobs.
        /// </summary>
        IPlannerScheduler IPlanExecutor.PlannerScheduler => PlannerScheduler;
        protected PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TDestroyStatesScheduler> PlannerScheduler;

        /// <summary>
        /// The domain data.
        /// </summary>
        ITraitBasedStateConverter ITraitBasedPlanExecutor.StateConverter => m_StateConverter;
        protected PlannerStateConverter<TObject, TStateKey, TStateData, TStateDataContext, TStateManager> m_StateConverter;



        protected MonoBehaviour m_Actor;
        protected TStateManager m_StateManager;
        protected ObjectCorrespondence m_PlanStateToGameStateIdLookup = new ObjectCorrespondence(1, Allocator.Persistent);

        PlanExecutionSettings m_ExecutionSettings;

        DecisionRuntimeInfo m_DecisionRuntimeInfo;
        Coroutine m_CurrentActionCoroutine;
        IActionExecutionInfo[] m_ActionExecuteInfos;

        Action<IActionKey> m_OnActionComplete;
        Action<IStateKey> m_OnTerminalStateReached;
        Action<IStateKey> m_OnUnexpectedState;

        protected abstract void Act(TActionKey act);
        public abstract string GetActionName(IActionKey actionKey); //todo move to planWrapper
        public abstract IActionParameterInfo[] GetActionParametersInfo(IStateKey stateKey, IActionKey actionKey);


        public virtual void Initialize(MonoBehaviour actor, PlanDefinition planDefinition, IActionExecutionInfo[] actionExecutionInfos)
        {
            m_Actor = actor;
            m_ActionExecuteInfos = actionExecutionInfos;

            // Setup world
            var world = new World($"{actor.name} {actor.GetInstanceID()}");
            m_StateManager = world.GetOrCreateSystem<TStateManager>();
            world.GetOrCreateSystem<SimulationSystemGroup>().AddSystemToUpdateList(m_StateManager);
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world, ScriptBehaviourUpdateOrder.CurrentPlayerLoop);

            // Setup scheduler - todo move this elsewhere
            PlannerScheduler = new PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TDestroyStatesScheduler>();
            PlannerScheduler.Initialize(m_StateManager, new THeuristic(), new TTerminationEvaluator(), planDefinition.DiscountFactor);

            // Setup domain data
            m_StateConverter = new PlannerStateConverter<TObject, TStateKey, TStateData, TStateDataContext, TStateManager>(planDefinition, m_StateManager);

            m_DecisionRuntimeInfo = new DecisionRuntimeInfo();
        }

        public void SetExecutionSettings(PlanExecutionSettings executionSettings, Action<IActionKey> onActionComplete = null, Action<IStateKey> onTerminalStateReached = null, Action<IStateKey> onUnexpectedState = null)
        {
            m_ExecutionSettings = executionSettings;
            m_OnActionComplete = onActionComplete;
            m_OnTerminalStateReached = onTerminalStateReached;
            m_OnUnexpectedState = onUnexpectedState;
        }

        public void SetPlan(IPlan plan)
        {
            if (!(plan is PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> planWrapper))
                throw new ArgumentException($"Plan must be of type {typeof(PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>)}.");

            if (Status == PlanExecutionStatus.AwaitingPlan)
                Status = PlanExecutionStatus.AwaitingExecution;

            m_PlanWrapper = planWrapper;
        }

        public void UpdateCurrentState(IStateKey stateKey)
        {
            if (!(stateKey is TStateKey newExecutorState))
                throw new ArgumentException($"Expected state key of type {typeof(TStateKey)}. Received state key of type {stateKey?.GetType()}.");

            // Don't destroy the current state if the same key/data are used
            if (!newExecutorState.Equals(CurrentExecutorState))
                m_StateManager.DestroyState(CurrentExecutorState);

            // If the user has passed in a plan state, use a copy instead (so we don't mutate plan states).
            // Fixme: when executor and plan states use separate worlds.
            var matchingPlanState = default(TStateKey);
            if (m_PlanWrapper != null && FindMatchingStateInPlan(newExecutorState, out matchingPlanState) && stateKey.Equals(matchingPlanState))
                newExecutorState = m_StateManager.CopyState(newExecutorState); // Don't use plan states as executor states.

            // Assign new state
            CurrentExecutorState = newExecutorState;
            CurrentPlanState = matchingPlanState;

            // Check for terminal or unexpected state
            if (m_PlanWrapper != null)
                CheckNewState();
        }

        public void UpdateCurrentState(IStateData stateData)
        {
            if (!(stateData is TStateData castStateData))
                throw new ArgumentException($"Expected state data of type {typeof(TStateData)}. Received state data of type {stateData?.GetType()}.");

            var updatedKey = m_StateManager.GetStateDataKey(castStateData);
            UpdateCurrentState(updatedKey);
        }

        public bool ReadyToAct()
        {
            // Check if there isn't an assigned plan or if currently executing an action
            if (m_PlanWrapper == null || !CurrentActionKey.Equals(default))
                return false;

            // Check for a corresponding plan state, if not already assigned
            if (CurrentPlanState.Equals(default) && m_PlanWrapper.TryGetEquivalentPlanState(CurrentExecutorState, out var planStateKey))
                CurrentPlanState = planStateKey;

            // Check for immediate decision info
            if (!m_PlanWrapper.TryGetStateInfo(CurrentPlanState, out var stateInfo)
                || !m_PlanWrapper.TryGetOptimalAction(CurrentPlanState, out _))
                return false;

            // Check user-specified condition
            switch (m_ExecutionSettings.ExecutionMode)
            {
                case PlanExecutionSettings.PlanExecutionMode.ActImmediately:
                    return true;

                case PlanExecutionSettings.PlanExecutionMode.WaitForManualExecutionCall:
                    return false;

                case PlanExecutionSettings.PlanExecutionMode.WaitForPlanCompletion:
                    return stateInfo.SubgraphComplete;

                case PlanExecutionSettings.PlanExecutionMode.WaitForMaximumDecisionTolerance:
                    return stateInfo.PolicyValue.Range <= m_ExecutionSettings.MaximumDecisionTolerance;

                case PlanExecutionSettings.PlanExecutionMode.WaitForMinimumPlanSize:
                    return m_PlanWrapper.Size >= m_ExecutionSettings.MinimumPlanSize || stateInfo.SubgraphComplete;

                case PlanExecutionSettings.PlanExecutionMode.WaitForMinimumSearchTime:
                    return Time.time - m_DecisionRuntimeInfo.StartTimestamp >= m_ExecutionSettings.MinimumSearchTime || stateInfo.SubgraphComplete;

                default:
                    return true;
            }
        }

        public void ExecuteNextAction(IActionKey overrideAction = null)
        {
            if (m_PlanWrapper == null)
            {
                Debug.LogError("No plan assigned on the plan executor.");
                return;
            }

            // Reset decision time tracker
            m_DecisionRuntimeInfo.Reset();

            // Check for a corresponding plan state, if not already assigned
            if (CurrentPlanState.Equals(default) && m_PlanWrapper.TryGetEquivalentPlanState(CurrentExecutorState, out var planStateKey))
                CurrentPlanState = planStateKey;

            // Use specified action
            if (overrideAction != null)
            {
                if (!(overrideAction is TActionKey typedAction))
                    throw new ArgumentException($"Expected override action key of type {typeof(TActionKey)}. Received key of type {overrideAction.GetType()}.");

                if (!m_PlanWrapper.TryGetActionInfo(CurrentPlanState, typedAction, out _))
                    throw new ArgumentException($"Action {typedAction} for state {CurrentPlanState} was not found in the plan.");

                Status = PlanExecutionStatus.ExecutingAction;
                CurrentActionKey = typedAction;
                Act(typedAction);
                return;
            }

            // No manual override; use current best action
            if (!m_PlanWrapper.TryGetOptimalAction(CurrentPlanState, out var actionKey))
            {
                Debug.LogError($"No actions available for plan state {CurrentPlanState}.");
                Status = PlanExecutionStatus.AwaitingExecution;
                CurrentActionKey = default;
            }
            else
            {
                Status = PlanExecutionStatus.ExecutingAction;
                CurrentActionKey = actionKey;
                Act(CurrentActionKey);
            }
        }

        public void StopExecution()
        {
            Status = PlanExecutionStatus.AwaitingExecution;
            CurrentActionKey = default;

            if (m_CurrentActionCoroutine != null)
            {
                m_Actor.StopCoroutine(m_CurrentActionCoroutine);
                m_CurrentActionCoroutine = null;
            }
        }

        protected IActionExecutionInfo GetExecutionInfo(string actionName)
        {
            for (int i = 0; i < m_ActionExecuteInfos.Length; i++)
            {
                var info = m_ActionExecuteInfos[i];
                if (info.IsValidForAction(actionName))
                    return info;
            }

            return null;
        }

        protected void StartAction(IActionExecutionInfo executionInfo, object[] arguments)
        {
            Assert.IsNull(m_CurrentActionCoroutine);

            if (executionInfo.InvokeMethod(arguments) is IEnumerator actionCoroutine)
            {
                Assert.IsNotNull(m_Actor, "No actor assigned on the plan executor. Cannot start coroutine.");

                // Begin action coroutine
                m_CurrentActionCoroutine = m_Actor.StartCoroutine(actionCoroutine);
                m_Actor.StartCoroutine(WaitForAction());
            }
            else
            {
                // Immediately complete action
                CompleteAction();
            }
        }

        void CheckNewState()
        {
            if (CurrentPlanState.Equals(default))
            {
                // Don't change the plan here. Let users decide what to do.
                Status = PlanExecutionStatus.AwaitingExecution;
                m_OnUnexpectedState?.Invoke(CurrentExecutorState);
            }
            else if (IsTerminal(CurrentPlanState))
            {
                // Reached terminal state -> no more plan to execute.
                Status = PlanExecutionStatus.AwaitingPlan;
                m_OnTerminalStateReached?.Invoke(CurrentPlanState);
            }
            else
            {
                Status = PlanExecutionStatus.AwaitingExecution;
            }
        }

        bool FindMatchingStateInPlan(TStateKey stateKey, out TStateKey planStateKey)
        {
            planStateKey = default;
            m_PlanStateToGameStateIdLookup.Clear();

            if (!m_PlanWrapper.TryGetEquivalentPlanState(stateKey, out var matchingKey))
                return false;

            planStateKey = matchingKey;
            var planStateData = m_StateManager.GetStateData(planStateKey, false);
            var inputStateData = m_StateManager.GetStateData(stateKey, false);

            // Map the plan state to the input state
            return planStateData.TryGetObjectMapping(inputStateData, m_PlanStateToGameStateIdLookup);
        }

        bool IsTerminal(TStateKey stateKey)
        {
            return m_PlanWrapper.TryGetStateInfo(stateKey, out var stateInfo)
                    && stateInfo.SubgraphComplete
                    && !m_PlanWrapper.TryGetOptimalAction(stateKey, out _);
        }

        IEnumerator WaitForAction()
        {
            yield return m_CurrentActionCoroutine;

            CompleteAction();
        }

        void CompleteAction()
        {
            m_OnActionComplete?.Invoke(CurrentActionKey);
            m_CurrentActionCoroutine = null;
            CurrentActionKey = default;
        }

        public void Dispose()
        {
            PlannerScheduler?.CurrentJobHandle.Complete();
            if (m_StateConverter is IDisposable disposable)
                disposable.Dispose();

            PlannerScheduler?.Dispose();
            m_PlanStateToGameStateIdLookup.Dispose();
        }
    }
}
