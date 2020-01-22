using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Traits;
using Unity.AI.Planner.Utility;
using Unity.Semantic.Traits;
using Unity.Semantic.Traits.Queries;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AI.Planner.Controller
{
    /// <summary>
    /// The primary component from the AI Planner package, governing planning and plan execution.
    /// </summary>
    [HelpURL(Help.BaseURL + "/manual/ConfigureScene.html")]
    [AddComponentMenu("AI/Decision Controller")]
    public sealed class DecisionController : MonoBehaviour
    {
#pragma warning disable 0649
        [FormerlySerializedAs("m_PlanDefinition")]
        [SerializeField]
        ProblemDefinition m_ProblemDefinition;

        [Tooltip("Automatically initialize the planner on start; Toggle off if you need to delay planner initialization due to external factors")]
        [SerializeField]
        bool m_InitializeOnStart = true;

        [FormerlySerializedAs("m_SearchSettings")]
        [SerializeField]
        PlannerSettings m_PlannerSettings;

        [SerializeField]
        PlanExecutionSettings m_ExecutionSettings;

        [SerializeField]
        SemanticQuery m_WorldObjectQuery;

        [SerializeField]
        ActionExecutionInfo[] m_ActionExecuteInfos;

        [SerializeField]
        [Tooltip("Automatically plan and execute according to the specified planning and execution settings.")]
        bool m_AutoUpdate = true;
#pragma warning restore 0649

        internal ITraitBasedPlanExecutor m_PlanExecutor { get; private set; }
        IPlannerScheduler m_PlannerScheduler;
        ITraitBasedStateConverter m_StateConverter;
        IPlanRequest m_CurrentPlanRequest => m_PlannerScheduler?.CurrentPlanRequest;

        /// <summary>
        /// Define if the controller should automatically schedule planning and execute the current plan. If false,
        /// the scheduler can be updated via <see cref="UpdateScheduler"/> and, similarly, the execution can be
        /// updated via <see cref="UpdateExecutor"/>.
        /// </summary>
        public bool AutoUpdate
        {
            get => m_AutoUpdate;
            set => m_AutoUpdate = value;
        }

        /// <summary>
        /// Returns whether the controller is currently idle (i.e. not planning and not executing actions)
        /// </summary>
        public bool IsIdle
        {
            get
            {
                if (!Initialized)
                    return false;

                if (!CurrentActionKey.Equals(default))
                    return false;

                if (CurrentStateKey.Equals(default)) // If we have no current state, then we're idle (need to update state)
                    return true;

                if (!m_PlannerScheduler.CurrentJobHandle.IsCompleted)
                    return false;

                if (!CurrentPlan.TryGetStateInfo(CurrentStateKey, out var stateInfo))
                    return true;

                return stateInfo.SubplanIsComplete && CurrentPlan.GetActions(CurrentStateKey, null) == 0;
            }
        }

        /// <summary>
        /// Settings for the control of the planning algorithm iterating on the current plan.
        /// </summary>
        public PlannerSettings PlannerSettings
        {
            get => m_PlannerSettings;
            set
            {
                m_PlannerSettings = value;
                m_CurrentPlanRequest?.WithSettings(value);
            }
        }

        /// <summary>
        /// Settings for control of the execution of plans.
        /// </summary>
        public PlanExecutionSettings PlanExecutionSettings
        {
            get => m_ExecutionSettings;
            set
            {
                m_ExecutionSettings = value;
                m_PlanExecutor?.SetExecutionSettings(this, m_ActionExecuteInfos, value, OnActionComplete, OnTerminalStateReached, OnUnexpectedState);
            }
        }

        /// <summary>
        /// Indicates if the decision controller has been initialized.
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// The current plan to be executed.
        /// </summary>
        public IPlan CurrentPlan => m_PlanExecutor.Plan;

        /// <summary>
        /// The state key of the current state, as used by the controller to track the execution of the plan.
        /// </summary>
        public IStateKey CurrentStateKey => m_PlanExecutor.CurrentExecutorStateKey;

        /// <summary>
        /// State data for the controller's current state.
        /// </summary>
        public IStateData CurrentStateData => m_PlanExecutor.CurrentStateData;

        /// <summary>
        /// The action key for the current action being executed.
        /// </summary>
        public IActionKey CurrentActionKey => m_PlanExecutor.CurrentActionKey;

        /// <summary>
        /// An event triggered after the current state has been updated
        /// </summary>
        public event Action stateUpdated;

        /// <summary>
        /// Status of the execution fo the current plan.
        /// </summary>
        public PlanExecutionStatus PlanExecutionStatus => m_PlanExecutor.Status;

        SemanticObject m_PlanningAgentObject;

        string Name => $"{gameObject.name} {gameObject.GetInstanceID()}";

        /// <summary>
        /// Initialize and create the executor instance
        /// </summary>
        public void Initialize()
        {
            if (Initialized)
            {
                Debug.LogWarning("DecisionController already initialized.");
                return;
            }

            if (m_ProblemDefinition == null)
            {
                Debug.LogWarning("Problem Definition is not set on the DecisionController");
                enabled = false;
                return;
            }

            var systemsProviderType = m_ProblemDefinition.SystemsProviderType;
            if (systemsProviderType == null)
            {
                Debug.LogError($"Planner systems provider not found. Have you generated the code (AI->Planner->Build)?");
                enabled = false;
                return;
            }
            var systemsProvider = (IPlanningSystemsProvider) Activator.CreateInstance(systemsProviderType);
            systemsProvider.Initialize(m_ProblemDefinition, Name);

            m_PlanningAgentObject = GetComponent<SemanticObject>();
            m_PlannerScheduler = systemsProvider.PlannerScheduler;
            m_StateConverter = systemsProvider.StateConverter;
            m_PlanExecutor = systemsProvider.PlanExecutor;
            m_PlanExecutor.SetExecutionSettings(this, m_ActionExecuteInfos, m_ExecutionSettings, OnActionComplete, OnTerminalStateReached, OnUnexpectedState);

            Initialized = true;

            // Query for initial state and plan
            var initialState = CreateNewStateFromWorldQuery();
            m_PlannerScheduler.RequestPlan(initialState, null, m_PlannerSettings);
            m_PlanExecutor.SetPlan(m_CurrentPlanRequest.Plan);
            m_PlanExecutor.UpdateCurrentState(initialState);
        }

        /// <summary>
        /// Uses a world query to update the current state used for planning and acting.
        /// </summary>
        public void UpdateStateWithWorldQuery()
        {
            if (m_PlanExecutor == null || m_StateConverter == null)
            {
                Debug.LogError("DecisionController components not initialized.");
                return;
            }
            var newState = CreateNewStateFromWorldQuery();
            m_PlanExecutor.UpdateCurrentState(newState);

            if (CurrentPlan.TryGetEquivalentPlanState(newState, out var planState))
                m_PlannerScheduler.UpdatePlanRequestRootState(planState);
            else
                m_PlannerScheduler.RequestPlan(newState, null, m_PlannerSettings);
        }

        /// <summary>
        /// Updates the planner scheduler. If the previous planning job has not finished, the scheduler will not
        /// scheduler new planning jobs unless forceComplete is true.
        /// </summary>
        /// <param name="forceComplete">Force the scheduler to complete previous planning jobs before scheduling new
        /// iterations.</param>
        public void UpdateScheduler(bool forceComplete = false)
        {
            m_PlannerScheduler?.Schedule(default, forceComplete);
        }

        /// <summary>
        /// Updates execution of the plan. If the plan execution criteria are satisfied, the executor will enact the
        /// next action of the plan.
        /// </summary>
        /// <param name="forceAct">Force the execution of the next action in the plan.</param>
        public void UpdateExecutor(bool forceAct = false)
        {
            // Update executor
            if (forceAct || m_PlanExecutor.ReadyToAct())
                m_PlanExecutor.ExecuteNextAction();
        }

        /// <summary>
        /// This is the default callback for completing an action. The default behavior is to update the executor
        /// state, according to the PlanExecutorStateUpdateMode specified on the DecisionController, then to advance
        /// the current plan request root state to the current executor state.
        /// </summary>
        /// <param name="actionKey">The action key for the completed action.</param>
        void OnActionComplete(IActionKey actionKey)
        {
            var actionName = m_PlanExecutor.GetActionName(actionKey);
            var executionInfo = GetExecutionInfo(actionName);

            UpdateExecutorState(executionInfo.PlanExecutorStateUpdateMode, actionKey);

            if (m_CurrentPlanRequest.Plan.TryGetStateInfo(m_PlanExecutor.CurrentPlanStateKey, out _))
                m_PlannerScheduler.UpdatePlanRequestRootState(m_PlanExecutor.CurrentPlanStateKey);
        }

        /// <summary>
        /// This is the default callback for reaching a terminal state in a plan. The default behavior is to query the
        /// game state for a new state. If the new state is non-terminal, a new plan request will be initiated from the
        /// new state, and the executor will be assigned the corresponding plan.
        /// </summary>
        /// <param name="stateKey">The state key for the terminal state reached.</param>
        void OnTerminalStateReached(IStateKey stateKey)
        {
        }

        /// <summary>
        /// This is the default callback for reaching an state not contained within the current plan. The default
        /// behavior is to begin a new plan request from the new state and assign the corresponding plan for execution.
        /// </summary>
        /// <param name="stateKey"></param>
        void OnUnexpectedState(IStateKey stateKey)
        {
            m_PlannerScheduler.RequestPlan(stateKey, null, m_PlannerSettings);
            m_PlanExecutor.SetPlan(m_CurrentPlanRequest.Plan);
        }

        IEnumerator Start()
        {
            if (!m_InitializeOnStart)
                yield break;

            yield return null; // Wait for world state to settle
            Initialize();

            if (!Initialized && m_InitializeOnStart)
                Debug.LogWarning($"Decision Controller for object {name} has not been initialized.");
		}

        void OnDestroy()
        {
            m_PlannerScheduler?.Dispose();
            m_StateConverter?.Dispose();
            m_PlanExecutor?.Dispose();
        }

        void Update()
        {
            if (!Initialized)
                return;

            // Execution
            if (AutoUpdate && m_PlanExecutor.Status == PlanExecutionStatus.AwaitingExecution)
                UpdateExecutor();
        }

        void LateUpdate()
        {
            if (AutoUpdate)
                UpdateScheduler();
        }

        ActionExecutionInfo GetExecutionInfo(string actionName)
        {
            for (int i = 0; i < m_ActionExecuteInfos.Length; i++)
            {
                var info = m_ActionExecuteInfos[i];
                if (info.IsValidForAction(actionName))
                    return info;
            }

            return null;
        }

        IEnumerable<SemanticObject> GetTraitBasedObjects()
        {
            return m_WorldObjectQuery == null ? Enumerable.Empty<SemanticObject>() : m_WorldObjectQuery.GetSemanticObjects();
        }

        IStateKey GetNextPlanState(IActionKey actionKey)
        {
            var plan = m_PlanExecutor.Plan;
            var lastState = m_PlanExecutor.CurrentPlanStateKey;

            var resultingStates = new List<IStateKey>();
            plan.GetResultingStates(lastState, actionKey, resultingStates);

            switch (resultingStates.Count)
            {
                case 0:
                    Debug.LogError($"No resulting states found for state {lastState} and action {m_PlanExecutor.GetActionName(actionKey)}{actionKey}.");
                    return null;
                case 1:
                    return resultingStates[0];

                default:
                    Debug.LogError($"Multiple possible states result from state {lastState} and action {m_PlanExecutor.GetActionName(actionKey)}{actionKey}. Cannot determine next state from the plan.");
                    return null;
            }
        }

        void UpdateExecutorState(PlanExecutorStateUpdateMode planExecutorStateUpdateMode, IActionKey actionKey)
        {
            IStateKey nextState = null;
            switch (planExecutorStateUpdateMode)
            {
                case PlanExecutorStateUpdateMode.UseWorldState:
                    nextState = CreateNewStateFromWorldQuery();
                    break;
                case PlanExecutorStateUpdateMode.UseNextPlanState:
                    nextState = GetNextPlanState(actionKey);
                    break;
            }
            m_PlanExecutor.UpdateCurrentState(nextState);
            stateUpdated?.Invoke();
        }

        IStateKey CreateNewStateFromWorldQuery()
        {
            m_PlannerScheduler.CurrentJobHandle.Complete();

            var traitBasedObjects = GetTraitBasedObjects();
            var stateKey = m_StateConverter.CreateState(
                m_PlanningAgentObject ? m_PlanningAgentObject.Entity : default,
                traitBasedObjects.Select(o => o.Entity)); // Ids are maintained

            return stateKey;
        }
    }
}
