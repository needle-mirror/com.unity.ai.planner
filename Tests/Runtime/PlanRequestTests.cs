using System;
using KeyDomain;
using NUnit.Framework;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Entities;
using UnityEngine;


namespace Unity.AI.Planner.Tests
{
    using KeyDomainScheduler = PlannerScheduler<StateEntityKey, ActionKey, StateManager, StateData, StateDataContext, ActionScheduler, TestManualOverrideHeuristic<StateData>, TestManualOverrideTerminationEvaluator<StateData>, DestroyStatesJobScheduler>;

    class PlanRequestTests
    {
        protected StateManager m_StateManager;
        protected KeyDomainScheduler m_Scheduler;

        [SetUp]
        public void SetUp()
        {
            KeyDomainUtility.Initialize(new World("TestWorld"));

            m_StateManager = KeyDomainUtility.StateManager;
            m_Scheduler = new KeyDomainScheduler();
            m_Scheduler.Initialize(m_StateManager, terminationEvaluator: default);
        }

        [TearDown]
        public void TearDown()
        {
            m_Scheduler?.Dispose();
            m_Scheduler = null;
            m_StateManager = null;
        }
    }
}

namespace Unity.AI.Planner.Tests.Unit
{
    using KeyDomainScheduler = PlannerScheduler<StateEntityKey, ActionKey, StateManager, StateData, StateDataContext, ActionScheduler, TestManualOverrideHeuristic<StateData>, TestManualOverrideTerminationEvaluator<StateData>, DestroyStatesJobScheduler>;

    class PlanRequestUnitTests: PlanRequestTests
    {
        [Test]
        public void PlanQueryInitializesWithPlanAndRunningStatus()
        {
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey);

            Assert.IsNotNull(query.Plan, "Null plan on query initialization.");
            Assert.AreEqual(PlanRequestStatus.Running, query.Status, $"Incorrect query status on initialization. Expected {PlanRequestStatus.Running}; received {query.Status}.");
        }

        [Test]
        public void ActivePlanQueryCanBePausedAndResumed()
        {
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey);
            Assert.AreEqual(PlanRequestStatus.Running, query.Status, $"Incorrect query status on initialization. Expected {PlanRequestStatus.Running}; received {query.Status}.");

            query.Pause();
            Assert.AreEqual(PlanRequestStatus.Paused, query.Status, $"Expected {PlanRequestStatus.Paused}; received {query.Status}.");

            query.Resume();
            Assert.AreEqual(PlanRequestStatus.Running, query.Status, $"Expected {PlanRequestStatus.Running}; received {query.Status}.");
        }

        [Test]
        public void CancelledPlanQueryCannotBePausedOrResumed()
        {
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey);
            Assert.AreEqual(PlanRequestStatus.Running, query.Status, $"Incorrect query status on initialization. Expected {PlanRequestStatus.Running}; received {query.Status}.");

            query.Cancel();
            Assert.AreEqual(PlanRequestStatus.Complete, query.Status, $"Expected {PlanRequestStatus.Complete}; received {query.Status}.");

            query.Pause();
            Assert.AreEqual(PlanRequestStatus.Complete, query.Status, $"Expected {PlanRequestStatus.Complete}; received {query.Status}.");

            query.Resume();
            Assert.AreEqual(PlanRequestStatus.Complete, query.Status, $"Expected {PlanRequestStatus.Complete}; received {query.Status}.");

            query.Cancel();
            Assert.AreEqual(PlanRequestStatus.Complete, query.Status, $"Expected {PlanRequestStatus.Complete}; received {query.Status}.");
        }

        [Test]
        public void PlanQueryMethodsWithDisposedQueryThrowErrors()
        {
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey);
            query.Dispose();

            Assert.Throws<InvalidOperationException>(query.Pause);
            Assert.Throws<InvalidOperationException>(query.Resume);
            Assert.Throws<InvalidOperationException>(query.Cancel);
            Assert.Throws<InvalidOperationException>(() => query.SearchUntil());
            Assert.Throws<InvalidOperationException>(() => query.WithBudget());
            Assert.Throws<InvalidOperationException>(() => query.SchedulingMode());
        }
    }
}

namespace Unity.AI.Planner.Tests.Integration
{
    using KeyDomainScheduler = PlannerScheduler<StateEntityKey, ActionKey, StateManager, StateData, StateDataContext, ActionScheduler, TestManualOverrideHeuristic<StateData>, TestManualOverrideTerminationEvaluator<StateData>, DestroyStatesJobScheduler>;

    class PlanRequestIntegrationTests : PlanRequestTests
    {
        [Test]
        public void CallbackInvokedAndQueryStatusIsCompleteAfterPlanComplete()
        {
            // Force terminal evaluation.
            m_Scheduler.Dispose();
            m_Scheduler = new KeyDomainScheduler();
            m_Scheduler.Initialize(m_StateManager, terminationEvaluator: new TestManualOverrideTerminationEvaluator<StateData> { TerminationReturnValue = true });

            bool complete = false;
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey,  plan => { complete = true;});

            for (int i = 0; i < 3 && !complete; i++)
            {
                m_Scheduler.Schedule(default);
                m_Scheduler.CurrentJobHandle.Complete();
            }

            Assert.AreEqual(PlanRequestStatus.Complete, query.Status);
            Assert.IsTrue(complete, "Callback on query complete was not invoked.");
        }

        [Test]
        public void CallbackIsTriggeredUponAchievingMaximumUpdates()
        {
            bool complete = false;
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey,  plan => { complete = true;})
                .SearchUntil(maximumUpdates: 1);

            for (int i = 0; i < 3 && !complete; i++)
            {
                m_Scheduler.Schedule(default);
                m_Scheduler.CurrentJobHandle.Complete();
            }

            Assert.AreEqual(PlanRequestStatus.Complete, query.Status);
            Assert.IsTrue(complete, "Callback on query complete was not invoked.");
        }

        [Test]
        public void CallbackIsTriggeredUponAchievingMaximumPlanSize()
        {
            bool complete = false;
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey,  plan => { complete = true;})
                .SearchUntil(planSize: 2);

            for (int i = 0; i < 3 && !complete; i++)
            {
                m_Scheduler.Schedule(default);
                m_Scheduler.CurrentJobHandle.Complete();
            }

            Assert.AreEqual(PlanRequestStatus.Complete, query.Status);
            Assert.IsTrue(complete, "Callback on query complete was not invoked.");
        }

        [Test]
        public void CallbackIsTriggeredUponAchievingRootStateTolerance()
        {
            bool complete = false;
            var query = m_Scheduler.RequestPlan(KeyDomainUtility.InitialStateKey,  plan => { complete = true;})
                .SearchUntil(rootStateTolerance: float.MaxValue);

            m_Scheduler.Schedule(default);
            m_Scheduler.CurrentJobHandle.Complete();
            Assert.IsFalse(complete);

            m_Scheduler.Schedule(default);
            m_Scheduler.CurrentJobHandle.Complete();

            Assert.AreEqual(PlanRequestStatus.Complete, query.Status);
            Assert.IsTrue(complete, "Callback on query complete was not invoked.");
        }
    }
}
