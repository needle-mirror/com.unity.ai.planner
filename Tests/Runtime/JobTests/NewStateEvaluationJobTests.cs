using System;
using NUnit.Framework;
using Unity.AI.Planner.Jobs;
using Unity.AI.Planner.Tests.Unit;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.AI.Planner.Tests.Unit
{
    [Category("Unit")]
    class NewStateEvaluationJobTests
    {
        internal struct StateValueAsHeuristicValue : IHeuristic<int>
        {
            public BoundedValue Evaluate(int stateData) => new float3(stateData, stateData, stateData);
        }

        struct EvensTerminalStateEvaluator : ITerminationEvaluator<int>
        {
            public bool IsTerminal(int stateData, out float terminalReward)
            {
                terminalReward = stateData;
                return stateData % 2 == 0;
            }
        }

        struct ExceptionHeuristic : IHeuristic<int>
        {
            public BoundedValue Evaluate(int stateData)
            {
                throw new Exception("Should not be thrown.");
            }
        }

        struct ExceptionTerminationEvaluator : ITerminationEvaluator<int>
        {
            public bool IsTerminal(int stateData, out float terminalReward)
            {
                terminalReward = 0f;
                throw new Exception("Should not be thrown.");
            }
        }

        [Test]
        public void DoesNotExecuteWithoutStates()
        {
            var states = new NativeList<int>(0, Allocator.TempJob);
            var stateInfoLookup = new NativeHashMap<int, StateInfo>(0, Allocator.TempJob);
            var binnedStateKeys = new NativeMultiHashMap<int, int>(1, Allocator.TempJob);

            var heuristicJob = new EvaluateNewStatesJob<int, int, TestStateDataContext, ExceptionHeuristic, ExceptionTerminationEvaluator>()
            {
                StateDataContext = new TestStateDataContext(),
                StateInfoLookup = stateInfoLookup.AsParallelWriter(),
                States = states.AsDeferredJobArray(),
                BinnedStateKeys = binnedStateKeys.AsParallelWriter(),
            };

            Assert.DoesNotThrow(() => heuristicJob.Schedule(states, default).Complete());

            states.Dispose();
            stateInfoLookup.Dispose();
            binnedStateKeys.Dispose();
        }

        [Test]
        public void EvaluateHeuristicMultipleStates()
        {
            const int kStateCount = 10;
            var states = new NativeList<int>(kStateCount, Allocator.TempJob);
            var stateInfoLookup = new NativeHashMap<int, StateInfo>(kStateCount, Allocator.TempJob);
            var binnedStateKeys = new NativeMultiHashMap<int, int>(kStateCount, Allocator.TempJob);

            for (int i = 0; i < kStateCount; i++)
            {
                states.Add(i);
            }

            var heuristicJob = new EvaluateNewStatesJob<int, int, TestStateDataContext, StateValueAsHeuristicValue, DefaultTerminalStateEvaluator<int>>
            {
                StateDataContext = new TestStateDataContext(),
                StateInfoLookup = stateInfoLookup.AsParallelWriter(),
                States = states.AsDeferredJobArray(),
                BinnedStateKeys = binnedStateKeys.AsParallelWriter(),
            };
            heuristicJob.Schedule(states, default).Complete();

            for (int i = 0; i < states.Length; i++)
            {
                stateInfoLookup.TryGetValue(i, out var stateInfo);

                Assert.AreEqual(new BoundedValue(i, i, i), stateInfo.PolicyValue);
            }

            states.Dispose();
            stateInfoLookup.Dispose();
            binnedStateKeys.Dispose();
        }

        [Test]
        public void EvaluateTerminationMultipleStates()
        {
            const int kStateCount = 10;
            var states = new NativeList<int>(kStateCount, Allocator.TempJob);
            var stateInfoLookup = new NativeHashMap<int, StateInfo>(kStateCount, Allocator.TempJob);
            var binnedStateKeys = new NativeMultiHashMap<int, int>(kStateCount, Allocator.TempJob);

            for (int i = 0; i < kStateCount; i++)
            {
                states.Add(i);
            }

            var heuristicJob = new EvaluateNewStatesJob<int, int, TestStateDataContext, DefaultHeuristic<int>, EvensTerminalStateEvaluator>
            {
                StateDataContext = new TestStateDataContext(),
                StateInfoLookup = stateInfoLookup.AsParallelWriter(),
                States = states.AsDeferredJobArray(),
                BinnedStateKeys = binnedStateKeys.AsParallelWriter(),
            };
            heuristicJob.Schedule(states, default).Complete();

            for (int i = 0; i < states.Length; i++)
            {
                stateInfoLookup.TryGetValue(i, out var stateInfo);

                Assert.AreEqual(i % 2 == 0, stateInfo.SubgraphComplete);
            }

            states.Dispose();
            stateInfoLookup.Dispose();
            binnedStateKeys.Dispose();
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    [Category("Performance")]
    public class NewStateEvaluationJobPerformanceTests
    {
        [Performance, Test]
        public void TestEvaluateMultipleStates()
        {
            const int kStateCount = 1000;

            NativeList<int> states = default;
            NativeHashMap<int, StateInfo> stateInfoLookup = default;
            NativeMultiHashMap<int, int> binnedStateKeys = default;

            Measure.Method(() =>
            {
                var heuristicJob = new EvaluateNewStatesJob<int, int, TestStateDataContext,
                    NewStateEvaluationJobTests.StateValueAsHeuristicValue, DefaultTerminalStateEvaluator>
                {
                    StateDataContext = new TestStateDataContext(),
                    States = states.AsDeferredJobArray(),

                    StateInfoLookup = stateInfoLookup.AsParallelWriter(),
                    BinnedStateKeys = binnedStateKeys.AsParallelWriter(),
                };
                heuristicJob.Schedule(states, default).Complete();
            }).SetUp(() =>
            {
                states = new NativeList<int>(kStateCount, Allocator.TempJob);
                stateInfoLookup = new NativeHashMap<int, StateInfo>(kStateCount, Allocator.TempJob);
                binnedStateKeys = new NativeMultiHashMap<int, int>(kStateCount, Allocator.TempJob);

                for (int i = 0; i < kStateCount; i++)
                {
                    states.Add(i);
                }
            }).CleanUp(() =>
            {
                states.Dispose();
                stateInfoLookup.Dispose();
                binnedStateKeys.Dispose();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();
        }
    }
}
#endif
