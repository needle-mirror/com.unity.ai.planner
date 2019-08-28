#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.Agent;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A sample (abstract) agent script for GameObjects; Allows specifying a plan/agent definition and for initializing an instance
    /// of a planner and controller; Provides default implementations that can be overridden
    /// </summary>
    /// <typeparam name="TAgent">Agent type (i.e. inheriting class)</typeparam>
    /// <typeparam name="TObject">Object type (usually domain generated code)</typeparam>
    /// <typeparam name="TStateKey">StateKey type (usually domain generated code)</typeparam>
    /// <typeparam name="TStateData">StateData type (usually domain generated code)</typeparam>
    /// <typeparam name="TStateDataContext">StateDataContext type (usually domain generated code)</typeparam>
    /// <typeparam name="TActionScheduler">ActionScheduler type (usually domain generated code)</typeparam>
    /// <typeparam name="THeuristic">Heuristic type (usually domain generated code, but can be custom)</typeparam>
    /// <typeparam name="TTerminationEvaluator">Termination evaluator type (usually domain generated code, but can be custom)</typeparam>
    /// <typeparam name="TStateManager">StateManager type (usually domain generated code)</typeparam>
    /// <typeparam name="TActionKey">ActionKey type (usually just <see cref="ActionKey"/>)</typeparam>
    public abstract class BaseAgent<TAgent, TObject, TStateKey, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TStateManager, TActionKey> : MonoBehaviour
        where TAgent : class
        where TObject : struct, IDomainObject
        where TStateKey : struct, IEquatable<TStateKey>, IStateKey
        where TStateData : struct, ITraitBasedStateData<TObject>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
        where TActionScheduler : ITraitBasedActionScheduler<TObject, TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey, ActionResult>, IGetActionName, new()
        where THeuristic : struct, IHeuristic<TStateData>
        where TTerminationEvaluator : struct, ITerminationEvaluator<TStateData>
        where TStateManager : JobComponentSystem, ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext>
        where TActionKey : struct, IEquatable<TActionKey>, IActionKeyWithGuid
    {
        class PlanInternal : IPlanInternal
        {
            SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> m_SearchContext;
            Controller<TAgent, TStateKey, TActionKey, TStateData, TStateDataContext, TStateManager, SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>> m_Controller;
            TActionScheduler m_ActionScheduler;

            internal PlanInternal(SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> searchContext,
                Controller<TAgent, TStateKey, TActionKey, TStateData, TStateDataContext, TStateManager, SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>> controller,
                TActionScheduler actionScheduler)
            {
                m_SearchContext = searchContext;
                m_Controller = controller;
                m_ActionScheduler = actionScheduler;
            }

            public IStateManagerInternal StateManager => m_Controller.StateManager as IStateManagerInternal;

            public IStateKey RootStateKey => m_SearchContext.RootStateKey;

            public int MaxHorizonFromRoot
            {
                get
                {
                    var depths = m_SearchContext.StateDepthLookup.GetValueArray(Allocator.TempJob);
                    var maxDepth = int.MinValue;
                    foreach (var depth in depths)
                    {
                        maxDepth = Mathf.Max(depth, maxDepth);
                    }
                    depths.Dispose();

                    return maxDepth;
                }
            }

            public IActionKey CurrentAction
            {
                get
                {
                    var currentAction = m_Controller.CurrentAction;
                    return currentAction.Equals(default) ? (IActionKey)null : currentAction;
                }
            }

            public bool GetOptimalAction(IStateKey stateKey, out IActionKey actionKey)
            {
                var found = m_SearchContext.GetOptimalAction((TStateKey)stateKey, out var actionKeyGeneric);
                actionKey = actionKeyGeneric;
                return found;
            }

            public string GetActionName(IActionKey actionKey)
            {
                return m_ActionScheduler.GetActionName(actionKey);
            }

            public ActionInfo GetActionInfo((IStateKey, IActionKey) stateActionKey)
            {
                m_SearchContext.PolicyGraph.ActionInfoLookup.TryGetValue(((TStateKey, TActionKey))stateActionKey, out var actionInfo);
                return actionInfo;
            }

            public int GetActions(IStateKey stateKey, List<IActionKey> actionKeys)
            {
                actionKeys?.Clear();

                int count = 0;
                var stateActionLookup = m_SearchContext.PolicyGraph.StateActionLookup;
                if (stateActionLookup.TryGetFirstValue((TStateKey)stateKey, out var stateActionKey, out var iterator))
                {
                    do
                    {
                        if (actionKeys != null)
                        {
                            actionKeys.Add(stateActionKey.Item2);
                        }
                        count++;
                    } while (stateActionLookup.TryGetNextValue(out stateActionKey, ref iterator));
                }

                return count;
            }

            public int GetActionResults((IStateKey, IActionKey) stateActionKey, List<(ActionResult, IStateKey)> actionResults)
            {
                actionResults?.Clear();

                var count = 0;
                var genericStateActionKey = ((TStateKey, TActionKey))stateActionKey;
                var policyGraph = m_SearchContext.PolicyGraph;
                var resultingStateLookup = policyGraph.ResultingStateLookup;
                var actionResultLookup = policyGraph.ActionResultLookup;
                if (resultingStateLookup.TryGetFirstValue(genericStateActionKey, out var resultingState, out var iterator))
                {
                    do
                    {
                        if (actionResults != null)
                        {
                            var genericStateActionResultKey = ((TStateKey, TActionKey, TStateKey))(stateActionKey.Item1, stateActionKey.Item2, resultingState);
                            if (actionResultLookup.TryGetValue(genericStateActionResultKey, out var actionResult))
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
        /// Current operational action that the controller is executing
        /// </summary>
        public IOperationalAction<TAgent, TStateData, TActionKey> CurrentOperationalAction => m_Controller?.CurrentOperationalAction;

        /// <summary>
        /// Instance of the state manager
        /// </summary>
        protected TStateManager m_StateManager;

        // These fields are assigned in the editor, so ignore the warning that they are never assigned to
#pragma warning disable 0649
        [SerializeField]
        List<TraitObjectData> m_InitialStateTraitData = new List<TraitObjectData>();

        [SerializeField]
        PlanningDomainDefinition m_PlanningDefinition;

        [SerializeField]
        DomainObjectQuery m_InitialObjectQuery;
#pragma warning restore 0649

        PlanInternal m_PlanInternal;
        Controller<TAgent, TStateKey, TActionKey, TStateData, TStateDataContext, TStateManager, SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>> m_Controller;
        TActionScheduler m_ActionScheduler;
        PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator> m_PlannerScheduler;
        JobHandle m_JobHandle;
        PlanningDomainData<TObject, TStateKey, TStateData, TStateDataContext, TStateManager> m_DomainData;

        /// <summary>
        /// Get the controller's current state (useful in operational actions)
        /// </summary>
        /// <param name="readWrite">Whether the state needs to be capable of writing back to</param>
        /// <returns>Current state data</returns>
        public TStateData GetCurrentState(bool readWrite = false)
        {
            return m_Controller.GetCurrentState(readWrite);
        }

        /// <summary>
        /// Signal completion of the current operational action
        /// </summary>
        public void CompleteAction()
        {
            m_Controller.CompleteAction();
        }

        /// <summary>
        /// Start is called on the frame when a script is enabled just before any Update methods are called the first time (See Unity docs)
        /// </summary>
        protected virtual void Start()
        {
            m_DomainData = new PlanningDomainData<TObject, TStateKey, TStateData, TStateDataContext, TStateManager>();
            m_DomainData.Initialize($"{name}Domain", m_PlanningDefinition, WorldDomainManager.Instance.GetDomainObjects(gameObject, m_InitialObjectQuery));
            InitializeController();
        }

        (TStateKey, TStateData, SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>) InitializePlannerSystems()
        {
            var world = new World(m_DomainData.Name);
            var entityManager = world.EntityManager;

            m_StateManager = world.GetOrCreateSystem<TStateManager>();
            m_ActionScheduler = new TActionScheduler();

            var initialStateData = m_DomainData.GetInitialState(entityManager, m_InitialStateTraitData);
            var initialStateKey = m_StateManager.GetStateDataKey(initialStateData);

            m_PlannerScheduler = new PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator>();
            m_PlannerScheduler.Initialize(initialStateKey, m_StateManager, m_ActionScheduler, new THeuristic(), new TTerminationEvaluator());

            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);
            return (initialStateKey, initialStateData, m_PlannerScheduler.SearchContext);
        }

        /// <summary>
        /// Initialize the controller
        /// </summary>
        protected virtual void InitializeController()
        {
            (var initialStateKey, var initialStateData, var searchContext) = InitializePlannerSystems();

            var actionMapping = m_DomainData.ActionMapping.ToDictionary(kvp => kvp.Key,
                kvp => kvp.Value == null ?
                    NOOPAction<TAgent, TStateData, TActionKey>.Instance :
                    kvp.Value as IOperationalAction<TAgent, TStateData, TActionKey>);

            var controllerState = m_StateManager.CopyStateData(initialStateData);
            var controllerKey = m_StateManager.GetStateDataKey(controllerState);
            m_Controller = new Controller<TAgent, TStateKey, TActionKey, TStateData, TStateDataContext, TStateManager, SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>>(
                searchContext, controllerKey, this as TAgent, m_StateManager, actionKey => actionMapping[m_ActionScheduler.GetActionName(actionKey)], ReadyToAct);

            m_PlanInternal = new PlanInternal(searchContext, m_Controller, m_ActionScheduler);
        }

        void OnApplicationQuit()
        {
            m_JobHandle.Complete();
        }

        void OnDestroy()
        {
            if (m_DomainData is IDisposable disposable)
                disposable.Dispose();

            if (m_PlannerScheduler != null)
                m_PlannerScheduler.Dispose();
        }

        /// <summary>
        /// Update is called every frame, if the MonoBehaviour is enabled (see Unity docs)
        /// </summary>
        protected virtual void Update()
        {
            m_Controller.Update();

            m_JobHandle = m_PlannerScheduler.Schedule(default);
            // FIXME: Ideally, we don't complete immediately, but without doing this the entity debugger throws errors
            m_JobHandle.Complete();
        }

        /// <summary>
        /// Override to provide to the planner when a plan can be executed by the controller
        /// </summary>
        /// <returns>Whether the plan is ready to be acted upon</returns>
        protected virtual bool ReadyToAct()
        {
            return true;
        }
    }
}
#endif
