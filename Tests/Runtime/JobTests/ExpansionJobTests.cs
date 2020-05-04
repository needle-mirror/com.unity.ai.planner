using NUnit.Framework;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.AI.Planner.Tests.Unit
{
    [Category("Unit")]
    class ExpansionJobTests
    {
        const int k_RootState = -1;
        const int k_StateOne = 1;
        const int k_StateTwo = 2;
        const int k_ActionOne = 1;
        const int k_ActionTwo = 2;
        const int k_StateThree = 3;
        const int k_StateFour = 4;

        PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> m_PolicyGraph;

        [SetUp]
        public void SetupPartialPolicyGraph()
        {
            m_PolicyGraph = new PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo>(10, 10, 10);

            var builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
            var stateContext = builder.AddState(k_RootState);
            stateContext.AddAction(k_ActionOne).AddResultingState(k_StateOne);
            stateContext.AddAction(k_ActionTwo).AddResultingState(k_StateTwo);

            // Add first half of actions to simulate action system
            builder.WithState(k_StateOne).AddAction(k_ActionOne);
            builder.WithState(k_StateTwo).AddAction(k_ActionTwo);
        }

        [TearDown]
        public void TearDown()
        {
            m_PolicyGraph.Dispose();
        }

        NativeMultiHashMap<int, int> GetBinnedStateKeys()
        {
            var binned = new NativeMultiHashMap<int, int>(m_PolicyGraph.StateInfoLookup.Count(), Allocator.Persistent);
            using (var stateKeys = m_PolicyGraph.StateInfoLookup.GetKeyArray(Allocator.Temp))
            {
                foreach (var stateKey in stateKeys)
                {
                    binned.Add(stateKey.GetHashCode(), stateKey);
                }
            }
            return binned;
        }

        [Test]
        public void AddLinksToMultipleExistingStates()
        {
            var statesToProcess = new NativeList<StateTransitionInfoPair<int, int, StateTransitionInfo>>(2, Allocator.TempJob);
            statesToProcess.Add(new StateTransitionInfoPair<int, int, StateTransitionInfo>(k_StateOne, k_ActionOne, k_StateOne, new StateTransitionInfo { Probability = 1, TransitionUtilityValue = 1}));
            statesToProcess.Add(new StateTransitionInfoPair<int, int, StateTransitionInfo>(k_StateTwo, k_ActionTwo, k_StateTwo, new StateTransitionInfo { Probability = 1, TransitionUtilityValue = 1}));

            var binnedStateKeys = GetBinnedStateKeys();
            var stateTransitionInfoLookup = m_PolicyGraph.StateTransitionInfoLookup;
            var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;
            var newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
            var newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);
            var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
            {
                BinnedStateKeys = binnedStateKeys,
                NewStateTransitionInfoPairs = statesToProcess.AsDeferredJobArray(),

                ActionLookup = m_PolicyGraph.ActionLookup.AsParallelWriter(),
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup.AsParallelWriter(),
                StateTransitionInfoLookup = stateTransitionInfoLookup.AsParallelWriter(),
                ResultingStateLookup = resultingStateLookup.AsParallelWriter(),
                NewStates = newStatesQueue.AsParallelWriter(),
                PredecessorGraph = m_PolicyGraph.PredecessorGraph.AsParallelWriter(),
                StateDataContext = new TestStateDataContext(),
                StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
            };

            // Check to ensure edges do not exist
            Assert.IsFalse(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateOne, k_ActionOne, k_StateOne), out _));
            Assert.IsFalse(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateTwo, k_ActionTwo, k_StateTwo), out _));

            expansionJob.Schedule(statesToProcess, default).Complete();

            // No new states
            Assert.AreEqual(0, newStatesQueue.Count);
            Assert.AreEqual(2, newStatesToDestroy.Count);

            // Action results for new edges
            Assert.IsTrue(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateOne, k_ActionOne, k_StateOne), out _));
            Assert.IsTrue(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateTwo, k_ActionTwo, k_StateTwo), out _));

            // Check for added edges (forward and reverse)
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue(new StateActionPair<int, int>(k_StateOne, k_ActionOne), out _, out _));
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue(new StateActionPair<int, int>(k_StateTwo, k_ActionTwo), out _, out _));

            statesToProcess.Dispose();
            binnedStateKeys.Dispose();
            newStatesQueue.Dispose();
            newStatesToDestroy.Dispose();
        }

        [Test]
        public void AddMultipleNewStates()
        {
            var statesToProcess = new NativeList<StateTransitionInfoPair<int, int, StateTransitionInfo>>(2, Allocator.TempJob);
            statesToProcess.Add(new StateTransitionInfoPair<int, int, StateTransitionInfo>(k_StateOne, k_ActionOne, k_StateThree, new StateTransitionInfo(){ Probability = 1, TransitionUtilityValue = 1}));
            statesToProcess.Add(new StateTransitionInfoPair<int, int, StateTransitionInfo>(k_StateTwo, k_ActionTwo, k_StateFour, new StateTransitionInfo(){ Probability = 1, TransitionUtilityValue = 1}));

            var binnedStateKeys =  GetBinnedStateKeys();
            var stateTransitionInfoLookup = m_PolicyGraph.StateTransitionInfoLookup;
            var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;
            var newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
            var newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);
            var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
            {
                BinnedStateKeys = binnedStateKeys,
                NewStateTransitionInfoPairs = statesToProcess.AsDeferredJobArray(),

                ActionLookup = m_PolicyGraph.ActionLookup.AsParallelWriter(),
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup.AsParallelWriter(),
                StateTransitionInfoLookup = stateTransitionInfoLookup.AsParallelWriter(),
                ResultingStateLookup = resultingStateLookup.AsParallelWriter(),
                NewStates = newStatesQueue.AsParallelWriter(),
                PredecessorGraph = m_PolicyGraph.PredecessorGraph.AsParallelWriter(),
                StateDataContext = new TestStateDataContext(),
                StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
            };

            // Check to ensure edges do not exist
            Assert.IsFalse(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateOne, k_ActionOne, k_StateThree), out _));
            Assert.IsFalse(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateTwo, k_ActionTwo, k_StateFour), out _));

            expansionJob.Schedule(statesToProcess, default).Complete();

            // No new states
            Assert.AreEqual(2, newStatesQueue.Count);
            Assert.AreEqual(0, newStatesToDestroy.Count);

            // Action results for new edges
            Assert.IsTrue(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateOne, k_ActionOne, k_StateThree), out _));
            Assert.IsTrue(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateTwo, k_ActionTwo, k_StateFour), out _));

            // Check for added edges (forward and reverse)
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue(new StateActionPair<int, int>(k_StateOne, k_ActionOne), out _, out _));
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue(new StateActionPair<int, int>(k_StateTwo, k_ActionTwo), out _, out _));

            statesToProcess.Dispose();
            binnedStateKeys.Dispose();
            newStatesQueue.Dispose();
            newStatesToDestroy.Dispose();
        }

        [Test]
        public void AddMixedNewAndExistingStates()
        {
            var statesToProcess = new NativeList<StateTransitionInfoPair<int, int, StateTransitionInfo>>(2, Allocator.TempJob);
            statesToProcess.Add(new StateTransitionInfoPair<int, int, StateTransitionInfo>(k_StateOne, k_ActionOne, k_StateOne, new StateTransitionInfo(){ Probability = 1, TransitionUtilityValue = 1}));
            statesToProcess.Add(new StateTransitionInfoPair<int, int, StateTransitionInfo>(k_StateTwo, k_ActionTwo, k_StateFour, new StateTransitionInfo(){ Probability = 1, TransitionUtilityValue = 1}));

            var binnedStateKeys =  GetBinnedStateKeys();
            var stateTransitionInfoLookup = m_PolicyGraph.StateTransitionInfoLookup;
            var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;
            var newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
            var newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);

            var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
            {
                BinnedStateKeys = binnedStateKeys,
                NewStateTransitionInfoPairs = statesToProcess.AsDeferredJobArray(),

                ActionLookup = m_PolicyGraph.ActionLookup.AsParallelWriter(),
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup.AsParallelWriter(),
                StateTransitionInfoLookup = stateTransitionInfoLookup.AsParallelWriter(),
                ResultingStateLookup = resultingStateLookup.AsParallelWriter(),
                NewStates = newStatesQueue.AsParallelWriter(),
                PredecessorGraph = m_PolicyGraph.PredecessorGraph.AsParallelWriter(),
                StateDataContext = new TestStateDataContext(),
                StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
            };

            // Check to ensure edges do not exist
            Assert.IsFalse(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateOne, k_ActionOne, k_StateOne), out _));
            Assert.IsFalse(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateTwo, k_ActionTwo, k_StateFour), out _));

            expansionJob.Schedule(statesToProcess, default).Complete();

            // Only one new state; One was existing
            Assert.AreEqual(1, newStatesQueue.Count);
            Assert.AreEqual(1, newStatesToDestroy.Count);

            // Action results for new edges
            Assert.IsTrue(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateOne, k_ActionOne, k_StateOne), out _));
            Assert.IsTrue(stateTransitionInfoLookup.TryGetValue(new StateTransition<int, int>(k_StateTwo, k_ActionTwo, k_StateFour), out _));

            // Check for added edges (forward and reverse)
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue(new StateActionPair<int, int>(k_StateOne, k_ActionOne), out _, out _));
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue(new StateActionPair<int, int>(k_StateTwo, k_ActionTwo), out _, out _));

            statesToProcess.Dispose();
            binnedStateKeys.Dispose();
            newStatesQueue.Dispose();
            newStatesToDestroy.Dispose();
        }

        [Test]
        public void AddNoStates()
        {
            var statesToProcess = new NativeList<StateTransitionInfoPair<int, int, StateTransitionInfo>>(0, Allocator.TempJob);

            var binnedStateKeys = GetBinnedStateKeys();
            var newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
            var stateTransitionInfoLookup = m_PolicyGraph.StateTransitionInfoLookup;
            var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;
            var predecessorGraph = m_PolicyGraph.PredecessorGraph;
            var newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);

            var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
            {
                BinnedStateKeys = binnedStateKeys,
                NewStateTransitionInfoPairs = statesToProcess.AsDeferredJobArray(),

                ActionLookup = m_PolicyGraph.ActionLookup.AsParallelWriter(),
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup.AsParallelWriter(),
                StateTransitionInfoLookup = stateTransitionInfoLookup.AsParallelWriter(),
                ResultingStateLookup = resultingStateLookup.AsParallelWriter(),
                NewStates = newStatesQueue.AsParallelWriter(),
                PredecessorGraph = predecessorGraph.AsParallelWriter(),
                StateDataContext = new TestStateDataContext(),
                StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
            };

            var stateTransitionInfosBefore = stateTransitionInfoLookup.Count();
            var resultingStateLookupBefore = resultingStateLookup.Count();
            var predecessorGraphBefore = predecessorGraph.Count();

            expansionJob.Schedule(statesToProcess, default).Complete();

            // No new action results, states, predecessor links, etc.
            Assert.AreEqual(0, newStatesQueue.Count);
            Assert.AreEqual(0, newStatesToDestroy.Count);
            Assert.AreEqual(stateTransitionInfosBefore, stateTransitionInfoLookup.Count());
            Assert.AreEqual(resultingStateLookupBefore, resultingStateLookup.Count());
            Assert.AreEqual(predecessorGraphBefore, predecessorGraph.Count());

            statesToProcess.Dispose();
            binnedStateKeys.Dispose();
            newStatesQueue.Dispose();
            newStatesToDestroy.Dispose();
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    using Unity.PerformanceTesting;

    // Test performance going wide; probably doesn't need to be deep
    [Category("Performance")]
    public class ExpansionJobPerformanceTests
    {
        NativeMultiHashMap<int, int> GetBinnedStateKeys(PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> policyGraph)
        {
            var binned = new NativeMultiHashMap<int, int>(policyGraph.StateInfoLookup.Length, Allocator.Persistent);
            using (var stateKeys = policyGraph.StateInfoLookup.GetKeyArray(Allocator.Temp))
            {
                foreach (var stateKey in stateKeys)
                {
                    binned.Add(stateKey.GetHashCode(), stateKey);
                }
            }
            return binned;
        }

        [Performance, Test]
        public void ExpandByManyUniqueStates()
        {
            const int kRootState = 0;
            const int kActionCount = 1000;

            PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> policyGraph = default;
            NativeMultiHashMap<int, int> binnedStateKeys = default;
            NativeQueue<int> newStatesQueue = default;
            NativeList<StateTransitionInfoPair<int, int, StateTransitionInfo>> statesToProcess = default;
            NativeQueue<int> newStatesToDestroy = default;

            Measure.Method(() =>
            {
                var stateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup;
                var resultingStateLookup = policyGraph.ResultingStateLookup;

                var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
                {
                    BinnedStateKeys = binnedStateKeys,
                    NewStateTransitionInfoPairs = statesToProcess.AsDeferredJobArray(),

                    ActionLookup = policyGraph.ActionLookup.AsParallelWriter(),
                    ActionInfoLookup = policyGraph.ActionInfoLookup.AsParallelWriter(),
                    StateTransitionInfoLookup = stateTransitionInfoLookup.AsParallelWriter(),
                    ResultingStateLookup = resultingStateLookup.AsParallelWriter(),
                    NewStates = newStatesQueue.AsParallelWriter(),
                    PredecessorGraph = policyGraph.PredecessorGraph.AsParallelWriter(),
                    StateDataContext = new TestStateDataContext(),
                    StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
                };

                expansionJob.Schedule(statesToProcess, default).Complete();
            }).SetUp(() =>
            {
                // One root node and all children nodes of a single depth
                policyGraph = PolicyGraphUtility.BuildTree(kActionCount, 1, 1);
                policyGraph.ExpandBy(kActionCount, kActionCount);

                newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
                newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);

                // Extend graph by one depth with the same number of actions
                statesToProcess = new NativeList<StateTransitionInfoPair<int, int, StateTransitionInfo>>(kActionCount, Allocator.TempJob);
                for (var i = 0; i < kActionCount; i++)
                {
                    statesToProcess.Add(new StateTransitionInfoPair<int, int, StateTransitionInfo>(kRootState, i, kActionCount + i, new StateTransitionInfo() { Probability = 1, TransitionUtilityValue = 1 }));
                }

                binnedStateKeys = GetBinnedStateKeys(policyGraph);
            }).CleanUp(() =>
            {
                policyGraph.Dispose();
                newStatesQueue.Dispose();
                statesToProcess.Dispose();
                binnedStateKeys.Dispose();
                newStatesToDestroy.Dispose();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(4.2, 6);
        }

        [Performance, Test]
        public void MatchManyExistingStates()
        {
            const int kRootState = 0;
            const int kActionCount = 1000;

            PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> policyGraph = default;
            NativeMultiHashMap<int, int> binnedStateKeys = default;
            NativeQueue<int> newStatesQueue = default;
            NativeList<StateTransitionInfoPair<int, int, StateTransitionInfo>> statesToProcess = default;
            NativeQueue<int> newStatesToDestroy = default;

            Measure.Method(() =>
            {
                var stateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup;
                var resultingStateLookup = policyGraph.ResultingStateLookup;

                var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
                {
                    BinnedStateKeys = binnedStateKeys,
                    NewStateTransitionInfoPairs = statesToProcess.AsDeferredJobArray(),

                    ActionLookup = policyGraph.ActionLookup.AsParallelWriter(),
                    ActionInfoLookup = policyGraph.ActionInfoLookup.AsParallelWriter(),
                    StateTransitionInfoLookup = stateTransitionInfoLookup.AsParallelWriter(),
                    ResultingStateLookup = resultingStateLookup.AsParallelWriter(),
                    NewStates = newStatesQueue.AsParallelWriter(),
                    PredecessorGraph = policyGraph.PredecessorGraph.AsParallelWriter(),
                    StateDataContext = new TestStateDataContext(),
                    StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
                };

                expansionJob.Schedule(statesToProcess, default).Complete();
            }).SetUp(() =>
            {
                // One root node and all children nodes of a single depth
                policyGraph = PolicyGraphUtility.BuildTree(kActionCount, 1, 1);
                policyGraph.ExpandBy(kActionCount, kActionCount);

                newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
                newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);

                // Extend graph by one depth with the same number of actions / resulting states that loop back on themselves
                statesToProcess = new NativeList<StateTransitionInfoPair<int, int, StateTransitionInfo>>(kActionCount, Allocator.TempJob);
                for (var i = 0; i < kActionCount; i++)
                {
                    statesToProcess.Add(new StateTransitionInfoPair<int, int, StateTransitionInfo>(kRootState, i, i, new StateTransitionInfo() { Probability = 1, TransitionUtilityValue = 1 }));
                }

                binnedStateKeys = GetBinnedStateKeys(policyGraph);
            }).CleanUp(() =>
            {
                policyGraph.Dispose();
                newStatesQueue.Dispose();
                statesToProcess.Dispose();
                binnedStateKeys.Dispose();
                newStatesToDestroy.Dispose();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(4.3, 6.25);
        }
    }
}
#endif
