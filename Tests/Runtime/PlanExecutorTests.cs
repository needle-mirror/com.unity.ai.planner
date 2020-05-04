using System;
using System.Collections.Generic;
using KeyDomain;
using NUnit.Framework;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.TestTools;

namespace Unity.AI.Planner.Tests.Integration
{
    using KeyDomainBaseExecutor = BaseTraitBasedPlanExecutor<TraitBasedObject,StateEntityKey,StateData,StateDataContext,ActionScheduler,DefaultHeuristic<StateData>,DefaultTerminalStateEvaluator<StateData>,StateManager,ActionKey,DestroyStatesJobScheduler>;
    using KeyDomainScheduler = PlannerScheduler<StateEntityKey, ActionKey, StateManager, StateData, StateDataContext, ActionScheduler, TestManualOverrideHeuristic<StateData>, TestManualOverrideTerminationEvaluator<StateData>, DestroyStatesJobScheduler>;

    class PlanExecutorTests
    {
        class MockKeyDomainPlanExecutor : KeyDomainBaseExecutor
        {
            public StateManager StateManager => m_StateManager;

            public PlanWrapper<StateEntityKey, ActionKey, StateManager, StateData, StateDataContext> Plan => m_PlanWrapper;

            public override string GetActionName(IActionKey actionKey) =>
                ActionScheduler.s_ActionGuidToNameLookup[((IActionKeyWithGuid)actionKey).ActionGuid];

            public override IActionParameterInfo[] GetActionParametersInfo(IStateKey stateKey, IActionKey actionKey) => null;

            protected override void Act(ActionKey actionKey) { }
        }

        class MockMonoBehaviour : MonoBehaviour { }

        MockKeyDomainPlanExecutor m_Executor;
        StateManager m_StateManager;
        KeyDomainScheduler m_Scheduler;

        [SetUp]
        public void SetUp()
        {
            m_Executor = new MockKeyDomainPlanExecutor();
            m_Executor.Initialize(new GameObject("TestGameObject").AddComponent<MockMonoBehaviour>(), ScriptableObject.CreateInstance<PlanDefinition>(), null);
            m_StateManager = m_Executor.StateManager;

            m_Scheduler = new KeyDomainScheduler();
            m_Scheduler.Initialize(m_StateManager, terminationEvaluator: default);

            KeyDomainUtility.Initialize(m_StateManager.World);
        }

        [TearDown]
        public void TearDown()
        {
            m_Scheduler.Dispose();
            m_Executor.Dispose();
        }

        [Test]
        public void ExecuteWithNoPlanLogsError()
        {
            LogAssert.Expect(LogType.Error, "No plan assigned on the plan executor.");
            m_Executor.ExecuteNextAction();
            Assert.Pass();
        }

        [Test]
        public void ExecuteCompletePlanTriggersCallbackAtTerminalState()
        {
            // Force terminal evaluation.
            m_Scheduler.Dispose();
            m_Scheduler = new KeyDomainScheduler();
            m_Scheduler.Initialize(m_StateManager, terminationEvaluator: new TestManualOverrideTerminationEvaluator<StateData> { TerminationReturnValue = true });

            // Initiate query
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey).SearchUntil(maximumUpdates: 1);

            // Schedule single iteration
            m_Scheduler.Schedule(default);
            m_Scheduler.CurrentJobHandle.Complete();

            // Set plan and grab next state
            var plan = query.Plan;
            m_Executor.SetPlan(plan);
            var nextState = GetNextPlanState(plan.RootStateKey, GetOptimalAction(plan, plan.RootStateKey));

            // Assert we reach the correct terminal state in callback
            m_Executor.SetExecutionSettings(default, onTerminalStateReached: (state) => Assert.AreEqual(nextState, state));

            // Execute
            m_Executor.UpdateCurrentState(KeyDomainUtility.InitialStateKey);
            m_Executor.ExecuteNextAction();
            m_Executor.UpdateCurrentState(nextState);

            Assert.AreEqual(PlanExecutionStatus.AwaitingPlan, m_Executor.Status);
        }

        [Test]
        public void UpdatePlanRootWithPredictedState()
        {
            // Initiate query
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey)
                .SearchUntil(maximumUpdates: 1)
                .WithBudget(searchIterationsPerUpdate: 2, stateExpansionsPerIteration: 4);

            // Schedule single iteration
            m_Scheduler.Schedule(default);
            m_Scheduler.CurrentJobHandle.Complete();

            // Set plan and grab next state
            var plan = query.Plan;
            m_Executor.SetPlan(plan);
            var nextStateFromOptimalAction = GetNextPlanState(plan.RootStateKey, GetOptimalAction(plan, plan.RootStateKey));

            // Execute
            m_Executor.UpdateCurrentState(KeyDomainUtility.InitialStateKey);
            m_Executor.ExecuteNextAction();
            m_Executor.UpdateCurrentState(nextStateFromOptimalAction);

            Assert.AreEqual(PlanExecutionStatus.AwaitingExecution, m_Executor.Status);
        }

        [Test]
        public void UpdateCurrentStateWithUnexpectedStateNotInPlan()
        {
            // Initiate query
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey)
                .SearchUntil(maximumUpdates: 1)
                .WithBudget(searchIterationsPerUpdate: 2, stateExpansionsPerIteration: 4);

            // Schedule single iteration
            m_Scheduler.Schedule(default);
            m_Scheduler.CurrentJobHandle.Complete();

            // Set plan and grab next state
            var plan = query.Plan;
            m_Executor.SetPlan(plan);
            var nextStateFromNonOptimalAction = GetNextPlanState(plan.RootStateKey, GetNonOptimalAction(plan, plan.RootStateKey));

            var stateCopy = m_StateManager.CopyState((StateEntityKey)nextStateFromNonOptimalAction);
            var stateCopyData = m_StateManager.GetStateData(stateCopy, readWrite: true);
            KeyDomainUtility.CreateKey(stateCopyData, ColorValue.White);

            // Set unexpected state callback
            var triggered = false;
            m_Executor.SetExecutionSettings(default, onUnexpectedState: (state) => triggered = true);

            // Execute
            m_Executor.UpdateCurrentState(KeyDomainUtility.InitialStateKey);
            m_Executor.ExecuteNextAction();
            m_Executor.UpdateCurrentState(stateCopy);

            Assert.AreEqual(PlanExecutionStatus.AwaitingExecution, m_Executor.Status);
            Assert.AreEqual(true, triggered);
        }

        [Test]
        public void UpdateCurrentStateWithUnexpectedStateCoveredInPlan()
        {
            // Initiate query
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey)
                .SearchUntil(maximumUpdates: 1)
                .WithBudget(searchIterationsPerUpdate: 2, stateExpansionsPerIteration: 4);

            // Schedule single iteration
            m_Scheduler.Schedule(default);
            m_Scheduler.CurrentJobHandle.Complete();

            // Set plan and grab next state
            var plan = query.Plan;
            m_Executor.SetPlan(plan);
            var nextStateFromNonOptimalAction = m_StateManager.CopyState((StateEntityKey)GetNextPlanState(plan.RootStateKey, GetNonOptimalAction(plan, plan.RootStateKey)));

            // Set unexpected state callback
            var triggered = false;
            m_Executor.SetExecutionSettings(default, onUnexpectedState: (state) => triggered = true);

            // Execute
            m_Executor.UpdateCurrentState(m_StateManager.CopyState(KeyDomainUtility.InitialStateKey));
            m_Executor.ExecuteNextAction();
            m_Executor.UpdateCurrentState(nextStateFromNonOptimalAction);

            Assert.AreEqual(PlanExecutionStatus.AwaitingExecution, m_Executor.Status);
            Assert.AreEqual(false, triggered);
        }

        IStateKey GetNextPlanState(IStateKey stateKey, IActionKey actionKey)
        {
            var plan = m_Executor.Plan;
            Assert.AreNotEqual(actionKey, default);

            var resultingStates = new List<IStateKey>();
            plan.GetResultingStates(stateKey, actionKey, resultingStates);

            Assert.IsTrue(resultingStates.Count > 0, "No resulting states from plan root.");
            return resultingStates[0];
        }

        IActionKey GetOptimalAction(IPlan plan, IStateKey stateKey)
        {
            plan.TryGetOptimalAction(stateKey, out var actionKey);
            return actionKey;
        }

        IActionKey GetNonOptimalAction(IPlan plan, IStateKey stateKey)
        {
            var optimalAction = GetOptimalAction(plan, stateKey);
            var actions = new List<IActionKey>();
            plan.GetActions(stateKey, actions);

            foreach (var action in actions)
            {
                if (!action.Equals(optimalAction))
                    return action;
            }

            return null;
        }
    }
}
