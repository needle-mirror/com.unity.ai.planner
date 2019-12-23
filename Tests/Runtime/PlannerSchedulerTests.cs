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
            var rootState = 0;
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, DefaultHeuristic, DefaultTerminalStateEvaluator, DestroyIntsScheduler>();
            scheduler.Initialize(rootState, new TestStateManager(), new DefaultHeuristic(), new DefaultTerminalStateEvaluator());
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
            scheduler.Initialize(k_RootState, new TestStateManager(), new CountToHeuristic { Goal = k_Goal }, new CountToTerminationEvaluator { Goal = k_Goal });

            scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootInfo);
            while (!rootInfo.SubgraphComplete)
            {
                var currentJobHandle = scheduler.Schedule(default);
                currentJobHandle.Complete();

                scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out rootInfo);
            }

            var numStates = scheduler.SearchContext.PolicyGraph.StateInfoLookup.Length;
            var numActions = scheduler.SearchContext.PolicyGraph.ActionInfoLookup.Length;
            scheduler.Dispose();

            Assert.IsTrue(rootInfo.SubgraphComplete);
            Assert.AreEqual(103, numStates);
            Assert.AreEqual(288, numActions);
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
