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
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, DefaultCumulativeRewardEstimator<int>, DefaultTerminalStateEvaluator<int>, DestroyIntsScheduler>();
            scheduler.Initialize(new TestStateManager(), new DefaultCumulativeRewardEstimator<int>(), new DefaultTerminalStateEvaluator<int>());
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
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, CountToCumulativeRewardEstimator, CountToTerminationEvaluator, DestroyIntsScheduler>();
            scheduler.Initialize(new TestStateManager(), new CountToCumulativeRewardEstimator { Goal = k_Goal }, new CountToTerminationEvaluator { Goal = k_Goal });

            bool complete = false;
            var request = scheduler.RequestPlan(k_RootState).PlanUntil(requestCompleteCallback: (plan) => {complete = true;});
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
            var scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, CountToCumulativeRewardEstimator, CountToTerminationEvaluator, DestroyIntsScheduler>();
            scheduler.Initialize(new TestStateManager(), new CountToCumulativeRewardEstimator { Goal = k_Goal }, new CountToTerminationEvaluator { Goal = k_Goal });
            var request = scheduler.RequestPlan(k_RootState);
            for (int i = 0; i < 10; i++)
            {
                scheduler.Schedule(default).Complete();
            }

            scheduler.UpdatePlanRequestRootState(1);
            scheduler.CurrentJobHandle.Complete();

            var planRootState = scheduler.planData.RootStateKey;
            var zeroInPlan = scheduler.planData.StateDepthLookup.ContainsKey(k_RootState);
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
    using PerformanceTesting;

    [Category("Performance")]
    class PlannerSchedulerPerformanceTests
    {
        [Performance, Test]
        public void ProfileCountToGoalScheduled()
        {
            const int kRootState = 0;
            const int kGoal = 42;
            PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, CountToCumulativeRewardEstimator, CountToTerminationEvaluator, CountToDestroyStatesScheduler> scheduler = default;

            Measure.Method(() =>
            {
                var planRequest = scheduler.RequestPlan(kRootState);
                while (planRequest.Status != PlanRequestStatus.Complete)
                {
                    scheduler.Schedule(default).Complete();
                }
            }).SetUp(() =>
            {
                scheduler = new PlannerScheduler<int, int, TestStateManager, int, TestStateDataContext, CountToActionScheduler, CountToCumulativeRewardEstimator, CountToTerminationEvaluator, CountToDestroyStatesScheduler>();
                scheduler.Initialize(new TestStateManager(), new CountToCumulativeRewardEstimator { Goal = kGoal }, new CountToTerminationEvaluator { Goal = kGoal }, 1.0f);
            }).CleanUp(() =>
            {
                scheduler.Dispose();
            }).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(9, 13);
        }
    }
}
#endif
