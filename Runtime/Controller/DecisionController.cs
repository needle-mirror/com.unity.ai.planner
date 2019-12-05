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
#pragma warning restore 0649

        IPlanExecutor m_PlanExecutor;
        IPlannerScheduler m_PlannerScheduler;
        Coroutine m_CurrentAction;

        internal IPlanExecutor planExecutor => m_PlanExecutor;

        string Name => gameObject.name;

        public IEnumerable<ITraitBasedObjectData> LocalObjectData => m_LocalObjectData;

        public event Action stateUpdated;

        public bool AutoUpdate
        {
            get => m_AutoUpdate;
            set => m_AutoUpdate = value;
        }

        IDecisionController m_DecisionControllerImplementation;

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
                return;
            }

            var planExecutorTypeName = $"{TypeResolver.ActionsNamespace}.{m_PlanDefinition.Name}.{m_PlanDefinition.Name}Executor,{TypeResolver.ActionsAssemblyName}";
            var executorType = TypeResolver.GetType(planExecutorTypeName);
            if (executorType == null)
            {
                Debug.LogError($"Cannot find type {planExecutorTypeName}");
                return;
            }

            m_PlanExecutor = (IPlanExecutor)Activator.CreateInstance(executorType);
            if (m_PlanExecutor == null)
            {
                Debug.LogError($"Unable to create an instance of {planExecutorTypeName}");
                return;
            }
            m_PlanExecutor.Initialize(Name, m_PlanDefinition, GetTraitBasedObjects(), m_ExecutionSettings);

            m_PlannerScheduler = m_PlanExecutor.PlannerScheduler;
            m_PlannerScheduler.SearchSettings = m_SearchSettings;
        }

        public void UpdateExecutor()
        {
            if (planExecutor == null)
            {
                Debug.LogWarning("No Executor to update");
                return;
            }

            if (m_CurrentAction != default)
                return;

            if (m_PlanExecutor.ReadyToAct())
                m_PlanExecutor.Act(this);
        }

        public void UpdateScheduler()
        {
            if (m_PlannerScheduler == null)
            {
                Debug.LogWarning("No planner scheduler to update.");
                return;
            }

            m_PlannerScheduler.Schedule(default).Complete();
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

        void OnDestroy()
        {
            if (planExecutor != null)
                m_PlanExecutor.Destroy();
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
            return m_PlanExecutor?.GetCurrentState(readWrite);
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
                    m_PlanExecutor.AdvancePlanWithNewState(GetTraitBasedObjects());
                    break;
                case ActionComplete.UseNextPlanState:
                    m_PlanExecutor.AdvancePlanWithPredictedState();
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
