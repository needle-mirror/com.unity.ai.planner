using System;
using NUnit.Framework;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.PerformanceTesting;
using UnityEngine;

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

        PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult> m_PolicyGraph;

        [SetUp]
        public void SetupPartialPolicyGraph()
        {
            m_PolicyGraph = new PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult>(10, 10);

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

        [Test]
        public void AddLinksToMultipleExistingStates()
        {
            var statesToProcess = new NativeList<(int, int, ActionResult, int)>(2, Allocator.TempJob);
            statesToProcess.Add((k_StateOne, k_ActionOne, new ActionResult { Probability = 1, TransitionUtilityValue = 1}, k_StateOne));
            statesToProcess.Add((k_StateTwo, k_ActionTwo, new ActionResult { Probability = 1, TransitionUtilityValue = 1}, k_StateTwo));

            var stateKeyArray = m_PolicyGraph.StateInfoLookup.GetKeyArray(Allocator.TempJob);
            var actionResultLookup = m_PolicyGraph.ActionResultLookup;
            var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;
            var newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
            var newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);
            var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
            {
                ExistingStateKeys = stateKeyArray,
                NewStateTransitionInfo = statesToProcess.AsDeferredJobArray(),

                StateActionLookup = m_PolicyGraph.StateActionLookup.AsParallelWriter(),
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup.AsParallelWriter(),
                ActionResultLookup = actionResultLookup.AsParallelWriter(),
                ActionToStateLookup = resultingStateLookup.AsParallelWriter(),
                NewStates = newStatesQueue.AsParallelWriter(),
                PredecessorGraph = m_PolicyGraph.PredecessorGraph.AsParallelWriter(),
                StateDataContext = new TestStateDataContext(),
                StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
            };

            // Check to ensure edges do not exist
            Assert.IsFalse(actionResultLookup.TryGetValue((k_StateOne, k_ActionOne, k_StateOne), out _));
            Assert.IsFalse(actionResultLookup.TryGetValue((k_StateTwo, k_ActionTwo, k_StateTwo), out _));

            expansionJob.Schedule(statesToProcess, default).Complete();

            // No new states
            Assert.AreEqual(0, newStatesQueue.Count);
            Assert.AreEqual(2, newStatesToDestroy.Count);

            // Action results for new edges
            Assert.IsTrue(actionResultLookup.TryGetValue((k_StateOne, k_ActionOne, k_StateOne), out _));
            Assert.IsTrue(actionResultLookup.TryGetValue((k_StateTwo, k_ActionTwo, k_StateTwo), out _));

            // Check for added edges (forward and reverse)
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue((k_StateOne, k_ActionOne), out _, out _));
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue((k_StateTwo, k_ActionTwo), out _, out _));

            statesToProcess.Dispose();
            stateKeyArray.Dispose();
            newStatesQueue.Dispose();
            newStatesToDestroy.Dispose();
        }

        [Test]
        public void AddMultipleNewStates()
        {
            var statesToProcess = new NativeList<(int, int, ActionResult, int)>(2, Allocator.TempJob);
            statesToProcess.Add((k_StateOne, k_ActionOne, new ActionResult(){ Probability = 1, TransitionUtilityValue = 1}, k_StateThree));
            statesToProcess.Add((k_StateTwo, k_ActionTwo, new ActionResult(){ Probability = 1, TransitionUtilityValue = 1}, k_StateFour));

            var stateKeyArray = m_PolicyGraph.StateInfoLookup.GetKeyArray(Allocator.TempJob);
            var actionResultLookup = m_PolicyGraph.ActionResultLookup;
            var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;
            var newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
            var newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);
            var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
            {
                ExistingStateKeys = stateKeyArray,
                NewStateTransitionInfo = statesToProcess.AsDeferredJobArray(),

                StateActionLookup = m_PolicyGraph.StateActionLookup.AsParallelWriter(),
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup.AsParallelWriter(),
                ActionResultLookup = actionResultLookup.AsParallelWriter(),
                ActionToStateLookup = resultingStateLookup.AsParallelWriter(),
                NewStates = newStatesQueue.AsParallelWriter(),
                PredecessorGraph = m_PolicyGraph.PredecessorGraph.AsParallelWriter(),
                StateDataContext = new TestStateDataContext(),
                StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
            };

            // Check to ensure edges do not exist
            Assert.IsFalse(actionResultLookup.TryGetValue((k_StateOne, k_ActionOne, k_StateThree), out _));
            Assert.IsFalse(actionResultLookup.TryGetValue((k_StateTwo, k_ActionTwo, k_StateFour), out _));

            expansionJob.Schedule(statesToProcess, default).Complete();

            // No new states
            Assert.AreEqual(2, newStatesQueue.Count);
            Assert.AreEqual(0, newStatesToDestroy.Count);

            // Action results for new edges
            Assert.IsTrue(actionResultLookup.TryGetValue((k_StateOne, k_ActionOne, k_StateThree), out _));
            Assert.IsTrue(actionResultLookup.TryGetValue((k_StateTwo, k_ActionTwo, k_StateFour), out _));

            // Check for added edges (forward and reverse)
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue((k_StateOne, k_ActionOne), out _, out _));
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue((k_StateTwo, k_ActionTwo), out _, out _));

            statesToProcess.Dispose();
            stateKeyArray.Dispose();
            newStatesQueue.Dispose();
            newStatesToDestroy.Dispose();
        }

        [Test]
        public void AddMixedNewAndExistingStates()
        {
            var statesToProcess = new NativeList<(int, int, ActionResult, int)>(2, Allocator.TempJob);
            statesToProcess.Add((k_StateOne, k_ActionOne, new ActionResult(){ Probability = 1, TransitionUtilityValue = 1}, k_StateOne));
            statesToProcess.Add((k_StateTwo, k_ActionTwo, new ActionResult(){ Probability = 1, TransitionUtilityValue = 1}, k_StateFour));

            var stateKeyArray = m_PolicyGraph.StateInfoLookup.GetKeyArray(Allocator.TempJob);
            var actionResultLookup = m_PolicyGraph.ActionResultLookup;
            var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;
            var newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
            var newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);

            var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
            {
                ExistingStateKeys = stateKeyArray,
                NewStateTransitionInfo = statesToProcess.AsDeferredJobArray(),

                StateActionLookup = m_PolicyGraph.StateActionLookup.AsParallelWriter(),
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup.AsParallelWriter(),
                ActionResultLookup = actionResultLookup.AsParallelWriter(),
                ActionToStateLookup = resultingStateLookup.AsParallelWriter(),
                NewStates = newStatesQueue.AsParallelWriter(),
                PredecessorGraph = m_PolicyGraph.PredecessorGraph.AsParallelWriter(),
                StateDataContext = new TestStateDataContext(),
                StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
            };

            // Check to ensure edges do not exist
            Assert.IsFalse(actionResultLookup.TryGetValue((k_StateOne, k_ActionOne, k_StateOne), out _));
            Assert.IsFalse(actionResultLookup.TryGetValue((k_StateTwo, k_ActionTwo, k_StateFour), out _));

            expansionJob.Schedule(statesToProcess, default).Complete();

            // Only one new state; One was existing
            Assert.AreEqual(1, newStatesQueue.Count);
            Assert.AreEqual(1, newStatesToDestroy.Count);

            // Action results for new edges
            Assert.IsTrue(actionResultLookup.TryGetValue((k_StateOne, k_ActionOne, k_StateOne), out _));
            Assert.IsTrue(actionResultLookup.TryGetValue((k_StateTwo, k_ActionTwo, k_StateFour), out _));

            // Check for added edges (forward and reverse)
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue((k_StateOne, k_ActionOne), out _, out _));
            Assert.IsTrue(resultingStateLookup.TryGetFirstValue((k_StateTwo, k_ActionTwo), out _, out _));

            statesToProcess.Dispose();
            stateKeyArray.Dispose();
            newStatesQueue.Dispose();
            newStatesToDestroy.Dispose();
        }

        [Test]
        public void AddNoStates()
        {
            var statesToProcess = new NativeList<(int, int, ActionResult, int)>(0, Allocator.TempJob);

            var stateKeyArray = m_PolicyGraph.StateInfoLookup.GetKeyArray(Allocator.TempJob);
            var newStatesQueue = new NativeQueue<int>(Allocator.TempJob);
            var actionResultLookup = m_PolicyGraph.ActionResultLookup;
            var resultingStateLookup = m_PolicyGraph.ResultingStateLookup;
            var predecessorGraph = m_PolicyGraph.PredecessorGraph;
            var newStatesToDestroy = new NativeQueue<int>(Allocator.TempJob);

            var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
            {
                ExistingStateKeys = stateKeyArray,
                NewStateTransitionInfo = statesToProcess.AsDeferredJobArray(),

                StateActionLookup = m_PolicyGraph.StateActionLookup.AsParallelWriter(),
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup.AsParallelWriter(),
                ActionResultLookup = actionResultLookup.AsParallelWriter(),
                ActionToStateLookup = resultingStateLookup.AsParallelWriter(),
                NewStates = newStatesQueue.AsParallelWriter(),
                PredecessorGraph = predecessorGraph.AsParallelWriter(),
                StateDataContext = new TestStateDataContext(),
                StatesToDestroy = newStatesToDestroy.AsParallelWriter(),
            };

            var actionResultsBefore = actionResultLookup.Length;
            var resultingStateLookupBefore = resultingStateLookup.Length;
            var predecessorGraphBefore = predecessorGraph.Length;

            expansionJob.Schedule(statesToProcess, default).Complete();

            // No new action results, states, predecessor links, etc.
            Assert.AreEqual(0, newStatesQueue.Count);
            Assert.AreEqual(0, newStatesToDestroy.Count);
            Assert.AreEqual(actionResultsBefore, actionResultLookup.Length);
            Assert.AreEqual(resultingStateLookupBefore, resultingStateLookup.Length);
            Assert.AreEqual(predecessorGraphBefore, predecessorGraph.Length);

            statesToProcess.Dispose();
            stateKeyArray.Dispose();
            newStatesQueue.Dispose();
            newStatesToDestroy.Dispose();
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    // Test performance going wide; probably doesn't need to be deep
    [Category("Performance")]
    public class ExpansionJobPerformanceTests
    {
        [Performance, Test]
        public void ExpandByManyUniqueStates()
        {
            const int kRootState = 0;
            const int kActionCount = 1000;

            PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult> policyGraph = default;
            NativeArray<int> stateKeyArray = default;
            NativeQueue<int> newStatesQueue = default;
            NativeList<(int, int, ActionResult, int)> statesToProcess = default;
            NativeQueue<int> newStatesToDestroy = default;

            Measure.Method(() =>
            {
                var actionResultLookup = policyGraph.ActionResultLookup;
                var resultingStateLookup = policyGraph.ResultingStateLookup;

                var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
                {
                    ExistingStateKeys = stateKeyArray,
                    NewStateTransitionInfo = statesToProcess.AsDeferredJobArray(),

                    StateActionLookup = policyGraph.StateActionLookup.AsParallelWriter(),
                    ActionInfoLookup = policyGraph.ActionInfoLookup.AsParallelWriter(),
                    ActionResultLookup = actionResultLookup.AsParallelWriter(),
                    ActionToStateLookup = resultingStateLookup.AsParallelWriter(),
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
                statesToProcess = new NativeList<(int, int, ActionResult, int)>(kActionCount, Allocator.TempJob);
                for (var i = 0; i < kActionCount; i++)
                {
                    statesToProcess.Add((kRootState, i, new ActionResult() { Probability = 1, TransitionUtilityValue = 1 }, kActionCount + i));
                }

                stateKeyArray = policyGraph.StateInfoLookup.GetKeyArray(Allocator.TempJob);
            }).CleanUp(() =>
            {
                policyGraph.Dispose();
                newStatesQueue.Dispose();
                statesToProcess.Dispose();
                stateKeyArray.Dispose();
                newStatesToDestroy.Dispose();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(4.2, 6);
        }

        [Performance, Test]
        public void MatchManyExistingStates()
        {
            const int kRootState = 0;
            const int kActionCount = 1000;

            PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult> policyGraph = default;
            NativeArray<int> stateKeyArray = default;
            NativeQueue<int> newStatesQueue = default;
            NativeList<(int, int, ActionResult, int)> statesToProcess = default;
            NativeQueue<int> newStatesToDestroy = default;

            Measure.Method(() =>
            {
                var actionResultLookup = policyGraph.ActionResultLookup;
                var resultingStateLookup = policyGraph.ResultingStateLookup;

                var expansionJob = new GraphExpansionJob<int, int, TestStateDataContext, int>
                {
                    ExistingStateKeys = stateKeyArray,
                    NewStateTransitionInfo = statesToProcess.AsDeferredJobArray(),

                    StateActionLookup = policyGraph.StateActionLookup.AsParallelWriter(),
                    ActionInfoLookup = policyGraph.ActionInfoLookup.AsParallelWriter(),
                    ActionResultLookup = actionResultLookup.AsParallelWriter(),
                    ActionToStateLookup = resultingStateLookup.AsParallelWriter(),
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
                statesToProcess = new NativeList<(int, int, ActionResult, int)>(kActionCount, Allocator.TempJob);
                for (var i = 0; i < kActionCount; i++)
                {
                    statesToProcess.Add((kRootState, i, new ActionResult() { Probability = 1, TransitionUtilityValue = 1 }, i));
                }

                stateKeyArray = policyGraph.StateInfoLookup.GetKeyArray(Allocator.TempJob);
            }).CleanUp(() =>
            {
                policyGraph.Dispose();
                newStatesQueue.Dispose();
                statesToProcess.Dispose();
                stateKeyArray.Dispose();
                newStatesToDestroy.Dispose();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(4.3, 6.25);
        }
    }
}
#endif
