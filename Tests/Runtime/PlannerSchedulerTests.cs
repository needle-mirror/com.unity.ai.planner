using NUnit.Framework;
using Unity.Jobs;
using Unity.PerformanceTesting;

namespace Unity.AI.Planner.Tests.Integration
{
    [Category("Integration")]
    [TestFixture]
    class PlannerSchedulerTests
    {

        [SetUp]
        public void SetupScheduler()
        {

        }

        [TearDown]
        public void TearDownScheduler()
        {
        }

        [Test]
        public void TestTenIterations()
        {
            var rootState = 0;
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, ActionScheduler, DefaultHeuristic, DefaultTerminalStateEvaluator>();
            scheduler.Initialize(rootState, new TestStateManager(), new ActionScheduler(), new DefaultHeuristic(), new DefaultTerminalStateEvaluator());
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
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, ActionScheduler, CountToHeuristic, CountToTerminationEvaluator>();
            scheduler.Initialize(k_RootState, new TestStateManager(), new ActionScheduler(), new CountToHeuristic { Goal = k_Goal }, new CountToTerminationEvaluator { Goal = k_Goal });

            scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootInfo);
            while (!rootInfo.Complete)
            {
                var currentJobHandle = scheduler.Schedule(default);
                currentJobHandle.Complete();

                scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out rootInfo);
            }

            Assert.IsTrue(rootInfo.Complete);
            Assert.AreEqual(103, scheduler.SearchContext.PolicyGraph.StateInfoLookup.Length);
            Assert.AreEqual(300, scheduler.SearchContext.PolicyGraph.ActionInfoLookup.Length);

            scheduler.Dispose();

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
        public void ProfileCountToGoalImmediate()
        {
            const int kRootState = 0;
            const int kGoal = 42;
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, ActionScheduler, CountToHeuristic, CountToTerminationEvaluator>();

            Measure.Method(() =>
            {
                scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(kRootState, out var rootInfo);
                while (!rootInfo.Complete)
                {
                    scheduler.Run();
                    scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(kRootState, out rootInfo);
                }
            }).SetUp(() =>
            {
                scheduler.Initialize(kRootState, new TestStateManager(), new ActionScheduler(), new CountToHeuristic { Goal = kGoal }, new CountToTerminationEvaluator { Goal = kGoal });
            }).CleanUp(() =>
            {
                scheduler.Dispose();
            }).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(27, 63);
        }

        [Performance, Test]
        public void ProfileCountToGoalScheduled()
        {
            const int kRootState = 0;
            const int kGoal = 42;
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, ActionScheduler, CountToHeuristic, CountToTerminationEvaluator>();

            Measure.Method(() =>
            {
                scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(kRootState, out var rootInfo);
                while (!rootInfo.Complete)
                {
                    scheduler.Schedule(default).Complete();
                    scheduler.SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(kRootState, out rootInfo);
                }
            }).SetUp(() =>
            {
                scheduler.Initialize(kRootState, new TestStateManager(), new ActionScheduler(), new CountToHeuristic { Goal = kGoal }, new CountToTerminationEvaluator { Goal = kGoal });
            }).CleanUp(() =>
            {
                scheduler.Dispose();
            }).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(22.8, 53.3);
        }
    }
}
#endif
