using System;
using NUnit.Framework;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.AI.Planner.Tests.Integration
{
    [Category("Integration")]
    [TestFixture]
    class PlannerSchedulerTests
    {
        struct DestroyIntsScheduler : IDestroyStatesScheduler<int, int, TestStateDataContext, TestStateManager>
        {
            public TestStateManager StateManager { get; set; }
            public NativeQueue<int> StatesToDestroy { get; set; }
            public JobHandle Schedule(JobHandle inputDeps)
            {
                return inputDeps;
            }
        }

        [Test]
        public void TestTenIterations()
        {
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, DefaultHeuristic<int>, DefaultTerminalStateEvaluator<int>, DestroyIntsScheduler>();
            scheduler.Initialize(new TestStateManager(), new DefaultHeuristic<int>(), new DefaultTerminalStateEvaluator<int>());
            JobHandle currentJobHandle = default;

            for (int i = 0; i < 10; i++)
            {
                currentJobHandle = scheduler.Schedule(default);
                currentJobHandle.Complete();
            }

            currentJobHandle.Complete();
            scheduler.Dispose();
        }

        [Test]
        public void TestUntilCompletion()
        {
            const int k_RootState = 0;
            const int k_Goal = 100;
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, CountToHeuristic, CountToTerminationEvaluator, DestroyIntsScheduler>();
            scheduler.Initialize(new TestStateManager(), new CountToHeuristic { Goal = k_Goal }, new CountToTerminationEvaluator { Goal = k_Goal });

            bool complete = false;
            var request = scheduler.RequestPlan(k_RootState).SearchUntil(requestCompleteCallback: (plan) => {complete = true;});
            while (!complete)
            {
                scheduler.Schedule(default).Complete();
            }

            var planSize = request.Plan.Size;
            request.Dispose();
            scheduler.Dispose();

            Assert.IsTrue(complete);
            Assert.AreEqual(103, planSize);
        }

        [Test]
        public void TestGraphPruning()
        {
            const int k_RootState = 0;
            const int k_Goal = 100;
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, CountToHeuristic, CountToTerminationEvaluator, DestroyIntsScheduler>();
            scheduler.Initialize(new TestStateManager(), new CountToHeuristic { Goal = k_Goal }, new CountToTerminationEvaluator { Goal = k_Goal });
            var request = scheduler.RequestPlan(k_RootState);
            for (int i = 0; i < 10; i++)
            {
                scheduler.Schedule(default).Complete();
            }

            scheduler.UpdatePlanRequestRootState(1);
            scheduler.CurrentJobHandle.Complete();

            var planRootState = scheduler.m_SearchContext.RootStateKey;
            var zeroInPlan = scheduler.m_SearchContext.StateDepthLookup.ContainsKey(k_RootState);
            request.Dispose();
            scheduler.Dispose();

            Assert.AreEqual(1, planRootState);
            Assert.IsFalse(zeroInPlan);
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    [Category("Performance")]
    class PlannerSchedulerPerformanceTests
    {
        [Performance, Test]
        public void ProfileCountToGoalScheduled()
        {
            const int kRootState = 0;
            const int kGoal = 42;
            PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, CountToHeuristic, CountToTerminationEvaluator> scheduler = null;

            Measure.Method(() =>
            {
                scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(kRootState, out var rootInfo);
                while (!rootInfo.SubgraphComplete)
                {
                    scheduler.Schedule(default).Complete();
                    scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(kRootState, out rootInfo);
                }
            }).SetUp(() =>
            {
                scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, CountToHeuristic, CountToTerminationEvaluator>
                    (kRootState, new TestStateManager(), new CountToActionScheduler(), new CountToHeuristic { Goal = kGoal }, new CountToTerminationEvaluator { Goal = kGoal });
            }).CleanUp(() =>
            {
                scheduler.Dispose();
            }).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(9, 13);
        }
    }
}
#endif
