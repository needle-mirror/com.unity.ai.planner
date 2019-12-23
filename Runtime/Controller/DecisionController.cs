using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.Controller;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.Controller
{
    [HelpURL(Help.BaseURL + "/manual/ConfigureScene.html")]
    [AddComponentMenu("AI/Decision Controller")]
    class DecisionController : MonoBehaviour, IDecisionController
    {
#pragma warning disable 0649
        [SerializeField]
        List<TraitBasedObjectData> m_LocalObjectData;

        [SerializeField]
        PlanDefinition m_PlanDefinition;

        [Tooltip("Automatically initialize the planner on start; Toggle off if you need to delay planner initialization due to external factors")]
        [SerializeField]
        bool m_InitializeOnStart = true;

        [Tooltip("Automatically update the plan each frame and act if an action can be taken")]
        [SerializeField]
        bool m_AutoUpdate = true;

        [SerializeField]
        PlannerSearchSettings m_SearchSettings;

        [SerializeField]
        PlanExecutionSettings m_ExecutionSettings;

        [SerializeField]
        TraitBasedObjectQuery m_WorldObjectQuery;

        [SerializeField]
        ActionExecutionInfo[] m_ActionExecuteInfos;

#if UNITY_EDITOR
        [SerializeField]
        bool m_DisplayAdvancedSettings;
#endif
#pragma warning restore 0649

        IPlannerScheduler m_PlannerScheduler;
        Coroutine m_CurrentAction;

        internal IPlanExecutor PlanExecutor;

        string Name => $"{gameObject.name} {gameObject.GetInstanceID()}";

        public bool IsIdle => PlanExecutor.IsIdle;

        public IEnumerable<ITraitBasedObjectData> LocalObjectData => m_LocalObjectData;

        public event Action stateUpdated;

        public bool AutoUpdate
        {
            get => m_AutoUpdate;
            set => m_AutoUpdate = value;
        }

        public void Initialize()
        {
            if (PlanExecutor != null)
            {
                Debug.LogWarning("Plan executor instance already created");
                return;
            }

            if (m_PlanDefinition == null)
            {
                Debug.LogWarning("Plan Definition is not set on the DecisionController");
                return;
            }

            var planExecutorTypeName = $"{TypeResolver.ActionsNamespace}.{m_PlanDefinition.Name}.{m_PlanDefinition.Name}Executor,{TypeResolver.ActionsAssemblyName}";
            var executorType = TypeResolver.GetType(planExecutorTypeName);
            if (executorType == null)
            {
                Debug.LogError($"Cannot find type {planExecutorTypeName}");
                return;
            }

            PlanExecutor = (IPlanExecutor)Activator.CreateInstance(executorType);
            if (PlanExecutor == null)
            {
                Debug.LogError($"Unable to create an instance of {planExecutorTypeName}");
                enabled = false;
                return;
            }
            PlanExecutor.Initialize(Name, m_PlanDefinition, GetTraitBasedObjects(), m_ExecutionSettings);

            m_PlannerScheduler = PlanExecutor.PlannerScheduler;
            if (m_PlannerScheduler == null)
            {
                Debug.LogError($"No planning scheduler was found.");
                enabled = false;
                return;
            }
            m_PlannerScheduler.SearchSettings = m_SearchSettings;
        }

        public void UpdateExecutor()
        {
            if (m_CurrentAction == default && PlanExecutor.ReadyToAct())
                PlanExecutor.Act(this);
        }

        public void UpdateScheduler(bool forceComplete = false)
        {
            m_PlannerScheduler.Schedule(default, forceComplete);
        }

        void Awake()
        {
            foreach (var data in m_LocalObjectData)
            {
                data.Initialize(gameObject);
            }
        }

        void Start()
        {
            if (!m_InitializeOnStart)
                return;

            Initialize();
        }

        void Update()
        {
            if (m_AutoUpdate)
            {
                UpdateExecutor();
                UpdateScheduler();
            }
        }

        public void UpdateStateWithWorldQuery()
        {
            PlanExecutor.AdvancePlanWithNewState(GetTraitBasedObjects());
            stateUpdated?.Invoke();
        }

        void OnDestroy()
        {
            PlanExecutor?.Destroy();
        }

        internal IActionExecutionInfo GetExecutionInfo(string actionName)
        {
            return m_ActionExecuteInfos.FirstOrDefault(a => a.IsValidForAction(actionName));
        }

        IEnumerable<ITraitBasedObjectData> GetTraitBasedObjects()
        {
            return WorldDomainManager.Instance.GetTraitBasedObjects(gameObject, m_WorldObjectQuery).Concat(m_LocalObjectData);
        }

        internal void StartAction(IActionExecutionInfo executionInfo, object[] arguments)
        {
            var result = executionInfo.InvokeMethod(arguments);

            if (result is IEnumerator cr)
            {
                m_CurrentAction = StartCoroutine(cr);
                StartCoroutine(WaitForAction(executionInfo.OnActionComplete));
            }
            else
            {
                PlanNextAction(executionInfo.OnActionComplete);
            }
        }

        public IStateData GetPlannerState(bool readWrite = false)
        {
            return PlanExecutor?.GetCurrentStateData(readWrite);
        }

        IEnumerator WaitForAction(ActionComplete onActionComplete)
        {
            yield return m_CurrentAction;

            PlanNextAction(onActionComplete);
        }

        void PlanNextAction(ActionComplete onActionComplete)
        {
            CompleteAction(onActionComplete);
            m_CurrentAction = null;
        }

        void CompleteAction(ActionComplete onActionComplete)
        {
            switch (onActionComplete)
            {
                case ActionComplete.UseWorldState:
                    PlanExecutor.AdvancePlanWithNewState(GetTraitBasedObjects());
                    break;
                case ActionComplete.UseNextPlanState:
                    PlanExecutor.AdvancePlanWithPredictedState();
                    break;
            }

            stateUpdated?.Invoke();
        }

        public ITraitBasedObjectData GetLocalObjectData(string objectName)
        {
            return m_LocalObjectData.FirstOrDefault(o => o.Name == objectName);
        }
    }
}
