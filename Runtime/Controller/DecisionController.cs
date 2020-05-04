using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.Controller;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.Controller
{
    /// <summary>
    /// The primary component from the AI Planner package, governing planning and plan execution.
    /// </summary>
    [HelpURL(Help.BaseURL + "/manual/ConfigureScene.html")]
    [AddComponentMenu("AI/Decision Controller")]
    public sealed class DecisionController : MonoBehaviour
    {
#pragma warning disable 0649
        [SerializeField]
        PlanDefinition m_PlanDefinition;

        [Tooltip("Automatically initialize the planner on start; Toggle off if you need to delay planner initialization due to external factors")]
        [SerializeField]
        bool m_InitializeOnStart = true;

        [SerializeField]
        PlannerSearchSettings m_SearchSettings;

        [SerializeField]
        PlanExecutionSettings m_ExecutionSettings;

        [SerializeField]
        TraitBasedObjectQuery m_WorldObjectQuery;

        [SerializeField]
        ActionExecutionInfo[] m_ActionExecuteInfos;

        [SerializeField]
        [Tooltip("Automatically plan and execute according to the specified search and execution settings.")]
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
                if (!CurrentActionKey.Equals(default))
                    return false;

                if (CurrentStateKey.Equals(default)) // If we have no current state, then we're idle (need to update state)
                    return true;

                if (!m_PlannerScheduler.CurrentJobHandle.IsCompleted)
                    return false;

                if (!CurrentPlan.TryGetStateInfo(CurrentStateKey, out var stateInfo))
                    return true;

                return stateInfo.SubgraphComplete && CurrentPlan.GetActions(CurrentStateKey, null) == 0;
            }
        }

        /// <summary>
        /// Settings for the control of the search algorithm iterating on the current plan.
        /// </summary>
        public PlannerSearchSettings PlannerSearchSettings
        {
            get => m_SearchSettings;
            set
            {
                m_SearchSettings = value;
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
                m_PlanExecutor?.SetExecutionSettings(value, OnActionComplete, OnTerminalStateReached, OnUnexpectedState);
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

        /// <summary>
        /// Initialize and create the executor instance
        /// </summary>
        public void Initialize()
        {
            if (m_PlanExecutor != null)
            {
                Debug.LogWarning("Plan executor instance already created");
                return;
            }

            if (m_PlanDefinition == null)
            {
                Debug.LogWarning("Plan Definition is not set on the DecisionController");
                enabled = false;
                return;
            }

            var planExecutorTypeName = $"{TypeResolver.PlansQualifier}.{m_PlanDefinition.Name}.{m_PlanDefinition.Name}Executor,{TypeResolver.PlansQualifier}";
            if (!TypeResolver.TryGetType(planExecutorTypeName, out var executorType))
            {
                Debug.LogError($"Cannot find type {planExecutorTypeName}");
                enabled = false;
                return;
            }

            m_PlanExecutor = (ITraitBasedPlanExecutor)Activator.CreateInstance(executorType);
            if (m_PlanExecutor == null)
            {
                Debug.LogError($"Unable to create an instance of {planExecutorTypeName}");
                enabled = false;
                return;
            }
            m_PlanExecutor.Initialize(this, m_PlanDefinition, m_ActionExecuteInfos);
            m_PlanExecutor.SetExecutionSettings(m_ExecutionSettings, OnActionComplete, OnTerminalStateReached, OnUnexpectedState);

            m_PlannerScheduler = m_PlanExecutor.PlannerScheduler;
            if (m_PlannerScheduler == null)
            {
                Debug.LogError($"No planning scheduler was found.");
                enabled = false;
                return;
            }

            m_StateConverter = m_PlanExecutor.StateConverter;
            if (m_StateConverter == null)
            {
                Debug.LogError($"No domain data was found.");
                enabled = false;
                return;
            }

            // Query for initial state and plan
            var initialState = CreateNewStateFromWorldQuery();
            m_PlannerScheduler.RequestPlan(initialState, null, m_SearchSettings);
            m_PlanExecutor.SetPlan(m_CurrentPlanRequest.Plan);
            m_PlanExecutor.UpdateCurrentState(initialState);

            Initialized = true;
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
            var newState = m_StateConverter.CreateStateFromObjectData(GetTraitBasedObjects());
            m_PlanExecutor.UpdateCurrentState(newState);
            m_PlannerScheduler.UpdatePlanRequestRootState(newState);
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
        public void UpdateExecutor()
        {
            // Complete previous jobs and set new auto-complete frame
            m_PlannerScheduler.CurrentJobHandle.Complete();

            // Update executor
            if (m_PlanExecutor.ReadyToAct())
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
            m_PlannerScheduler.RequestPlan(stateKey, null, m_SearchSettings);
            m_PlanExecutor.SetPlan(m_CurrentPlanRequest.Plan);
        }

        void Start()
        {
            if (m_InitializeOnStart)
                Initialize();
        }

        void OnDestroy()
        {
            m_PlanExecutor?.Dispose();
        }

        void Update()
        {
            if (!Initialized)
            {
                if (m_InitializeOnStart)
                    Debug.LogWarning($"Decision Controller for object {name} has not been initialized.");
                return;
            }

            if (!m_PlannerScheduler.CurrentJobHandle.IsCompleted)
                return;



            // Execution
            if (AutoUpdate)
                UpdateExecutor();
        }

        void LateUpdate()
        {
            if (AutoUpdate)
                UpdateScheduler();
        }

        List<ITraitBasedObjectData> GetTraitBasedObjects()
        {
            return WorldDomainManager.Instance.GetTraitBasedObjects(gameObject, m_WorldObjectQuery);
        }

        IActionExecutionInfo GetExecutionInfo(string actionName)
        {
            for (int i = 0; i < m_ActionExecuteInfos.Length; i++)
            {
                var info = m_ActionExecuteInfos[i];
                if (info.IsValidForAction(actionName))
                    return info;
            }

            return null;
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
            return m_StateConverter.CreateStateFromObjectData(GetTraitBasedObjects());
        }
    }
}
