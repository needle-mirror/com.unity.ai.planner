using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.AI.Planner.Agent
{
    class Controller<TAgent, TStateKey, TActionKey, TStateData, TStateDataContext, TStateManager, TPlan>
        where TStateKey : struct, IEquatable<TStateKey>, IStateKey
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where TStateManager : JobComponentSystem, IStateManager<TStateKey, TStateData, TStateDataContext>
        where TActionKey : struct, IEquatable<TActionKey>, IActionKey
        where TPlan : IPlan<TStateKey, TActionKey>
    {
        public TStateKey CurrentStateKey { get; private set; }
        public TActionKey CurrentAction { get; private set; }
        public IOperationalAction<TAgent, TStateData, TActionKey> CurrentOperationalAction { get; private set; }
        public TStateManager StateManager { get; }

        TPlan m_Plan;
        TAgent m_Agent;
        TStateData m_CurrentStateData => StateManager.GetStateData(CurrentStateKey, true);
        Func<TActionKey, IOperationalAction<TAgent, TStateData, TActionKey>> m_GetOperationalAction;
        Func<bool> m_CheckPlan;

        public Controller(TPlan plan, TStateKey currentStateKey, TAgent agent, TStateManager stateManager,
            Func<TActionKey, IOperationalAction<TAgent, TStateData, TActionKey>> getOperationalAction, Func<bool> checkPlan = null)
        {
            m_Plan = plan;
            CurrentStateKey = currentStateKey;
            m_Agent = agent;
            StateManager = stateManager;

            m_GetOperationalAction = getOperationalAction;
            m_CheckPlan = checkPlan ?? ( () => true );

            CurrentOperationalAction = NOOPAction<TAgent, TStateData, TActionKey>.Instance;
        }

        ~Controller()
        {
            m_Plan.Dispose();
        }

        public void Update()
        {
            if (CurrentAction.Equals(default))
            {
                if (ReadyToAct())
                    AdvancePlan();

                return;
            }

            switch (CurrentOperationalAction.Status(m_CurrentStateData, CurrentAction, m_Agent))
            {
                case OperationalActionStatus.InProgress:
                    CurrentOperationalAction.ContinueExecution(m_CurrentStateData, CurrentAction, m_Agent);
                    return;

                case OperationalActionStatus.NoLongerValid:
                    UpdatePlanToCurrentState();
                    return;

                case OperationalActionStatus.Completed:
                    CurrentOperationalAction.EndExecution(m_CurrentStateData, CurrentAction, m_Agent);
                    UpdatePlanToCurrentState();
                    return;
            }
        }

        public TStateData GetCurrentState(bool readWrite = false)
        {
            return StateManager.GetStateData(CurrentStateKey, readWrite);
        }

        bool ReadyToAct()
        {
            // Can be called before planner has had a chance to set up an initial policy
            return m_Plan.GetOptimalAction(m_Plan.RootStateKey, out _) && m_CheckPlan();
        }

        void AdvancePlan()
        {
            // Grab the next action from the policy.
            m_Plan.GetOptimalAction(m_Plan.RootStateKey, out var currentAction);
            CurrentOperationalAction = m_GetOperationalAction(currentAction);
            CurrentAction = currentAction;

            CurrentOperationalAction.BeginExecution(m_CurrentStateData, currentAction, m_Agent);
        }

        public void CompleteAction()
        {
            // End current domain action
            CurrentOperationalAction.EndExecution(m_CurrentStateData, CurrentAction, m_Agent);
            UpdatePlanToCurrentState();
        }

        void UpdatePlanToCurrentState()
        {
            CurrentAction = default;
            CurrentOperationalAction = NOOPAction<TAgent, TStateData, TActionKey>.Instance;

            m_Plan.UpdatePlan(CurrentStateKey);
        }
    }
}
