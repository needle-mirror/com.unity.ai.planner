using System;
using System.Collections;
using NUnit.Framework;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

using TestSearchContext = Unity.AI.Planner.SearchContext<int, int, Unity.AI.Planner.Tests.TestStateManager, int, Unity.AI.Planner.Tests.TestStateDataContext>;

namespace Unity.AI.Planner.Tests.Unit
{
    [Category("Unit")]
    class SelectionJobTests
    {
        NativeList<int> m_SelectedUnexpandedStates;
        NativeList<int> m_AllExpandedStates;
        PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult> m_PolicyGraph;

        [SetUp]
        public void SetupContainers()
        {
            m_SelectedUnexpandedStates = new NativeList<int>(Allocator.TempJob);
            m_AllExpandedStates = new NativeList<int>(Allocator.TempJob);
            m_PolicyGraph = new PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult>(1, 1);
        }

        [TearDown]
        public void TeardownContainers()
        {
            m_SelectedUnexpandedStates.Dispose();
            m_AllExpandedStates.Dispose();
            m_PolicyGraph.Dispose();
        }

        [Test]
        public void ThrowsExceptionWithEmptyGraph()
        {
            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(default))
            {
                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = default,
                    StateDepthLookup = depthMap,
                };

                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };

                // Schedule runs jobs on a different thread, so we execute on the main thread to verify that an exception is thrown
                Assert.Throws<ArgumentException>(selectJob.Execute);
            }
        }

        [Test]
        public void SelectsWithOnlyRoot()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                s0 [label="s0 - incomplete"]
            }

            ASCII:
                +-----------------+
                | s0 - incomplete |
                +-----------------+
            */

            // vars for test
            const int rootState = -1;

            // Add root state to policy graph
            var builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
            builder.AddState(rootState);

            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(rootState))
            {
                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = rootState,
                    StateDepthLookup = depthMap,
                };

                // Run selection job
                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };
                selectJob.Schedule().Complete();

                // Job has selected one state for expansion
                Assert.AreEqual(1, m_AllExpandedStates.Length);

                // Root state selected for expansion
                Assert.IsTrue(m_AllExpandedStates[0] == rootState);

                // No state selected has not been expanded
                Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

                // Unexpanded state is state 1
                Assert.IsTrue(m_SelectedUnexpandedStates[0] == rootState);

                // State has been added to search depth list
                Assert.IsTrue(searchContext.StateDepthLookup.TryGetValue(rootState, out var stateDepth));

                // Found at horizon 0
                Assert.AreEqual(0, stateDepth);
            }
        }

        [Test]
        public void SelectsOnlyUnexpandedState()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                s0 [label="s0 - incomplete"]
                s1 [label="s1 - incomplete"]
                s2 [label="s2 - complete"]
                s0 -> s1 [label="a1"]
                s0 -> s2 [label="a2"]
            }

            ASCII:
                +-----------------+  a1   +-----------------+
                | s0 - incomplete | ----> | s1 - incomplete |
                +-----------------+       +-----------------+
                  |
                  | a2
                  v
                +---------------+
                | s2 - complete |
                +---------------+
            */
            const int rootState = -1;
            const int stateOne = 1;
            const int stateTwo = 2;
            const int actionOne = 1;
            const int actionTwo = 2;

            var builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
            var stateContext = builder.AddState(rootState);
            stateContext.AddAction(actionOne).AddResultingState(stateOne);
            stateContext.AddAction(actionTwo).AddResultingState(stateTwo, complete: true);

            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(rootState))
            {
                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = rootState,
                    StateDepthLookup = depthMap,
                };

                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };
                selectJob.Schedule().Complete();

                // Job has selected one state for expansion
                Assert.AreEqual(1, m_AllExpandedStates.Length);

                // Incomplete state selected for expansion
                Assert.IsTrue(m_AllExpandedStates[0] == stateOne);

                // No state selected has not been expanded
                Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

                // Unexpanded state is state 1
                Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateOne);

                // State has been added to search depth list
                Assert.IsTrue(searchContext.StateDepthLookup.TryGetValue(stateOne, out var stateDepth));

                // Found at horizon 1
                Assert.AreEqual(1, stateDepth);
            }
        }

        [Test]
        public void SelectsNoStatesWithCompleteGraph()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                s0 [label="s0 - complete"]
                s1 [label="s1 - complete"]
                s2 [label="s2 - complete"]
                s0 -> s1 [label="a1"]
                s0 -> s2 [label="a2"]
            }

            ASCII:
                +-----------------+  a1   +-----------------+
                |  s0 - complete  | ----> |  s1 - complete  |
                +-----------------+       +-----------------+
                  |
                  | a2
                  v
                +---------------+
                | s2 - complete |
                +---------------+
            */
            const int rootState = -1;
            const int stateOne = 1;
            const int stateTwo = 2;
            const int actionOne = 1;
            const int actionTwo = 2;

            var builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
            var rootStateContext = builder.AddState(rootState, complete: true);
            rootStateContext.AddAction(actionOne, complete: true).AddResultingState(stateOne, complete: true);
            rootStateContext.AddAction(actionTwo, complete: true).AddResultingState(stateTwo, complete: true);

            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(rootState))
            {
                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = rootState,
                    StateDepthLookup = depthMap,
                };

                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };
                selectJob.Schedule().Complete();

                // Job has selected one state for expansion
                Assert.AreEqual(0, m_AllExpandedStates.Length);

                // No state selected has not been expanded
                Assert.IsTrue(m_SelectedUnexpandedStates.Length == 0);
            }
        }

        [Test]
        public void SelectsWithJoinedBranches()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                s0 [label="s0 - incomplete"]
                s1 [label="s1 - incomplete"]
                s2 [label="s2 - incomplete"]
                s3 [label="s3 - incomplete"]
                s0 -> s1 [label="a1"]
                s0 -> s2 [label="a2"]
                s1 -> s3 [label="a1"]
                s2 -> s3 [label="a1"]
            }

            ASCII:
                +-----------------+  a1   +-----------------+
                | s0 - incomplete | ----> | s1 - incomplete |
                +-----------------+       +-----------------+
                  |                         |
                  | a2                      | a1
                  v                         v
                +-----------------+  a1   +-----------------+
                | s2 - incomplete | ----> | s3 - incomplete |
                +-----------------+       +-----------------+
            */
            const int rootState = -1;
            const int stateOne = 1;
            const int stateTwo = 2;
            const int stateThree = 3;
            const int actionOne = 1;
            const int actionTwo = 2;

            var builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };

            var rootStateContext = builder.AddState(rootState);
            rootStateContext.AddAction(actionOne).AddResultingState(stateOne);
            rootStateContext.AddAction(actionTwo).AddResultingState(stateTwo);

            builder.WithState(stateOne).AddAction(actionOne).AddResultingState(stateThree);
            builder.WithState(stateTwo).AddAction(actionOne).AddResultingState(stateThree);

            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(rootState))
            {
                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = rootState,
                    StateDepthLookup = depthMap,
                };

                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };
                selectJob.Schedule().Complete();

                // Job has selected one state for expansion
                Assert.AreEqual(1, m_AllExpandedStates.Length);

                // Root state selected for expansion
                Assert.IsTrue(m_AllExpandedStates[0] == stateThree);

                // No state selected has not been expanded
                Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

                // Unexpanded state is state 1
                Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateThree);

                // State has been added to search depth list
                Assert.IsTrue(searchContext.StateDepthLookup.TryGetValue(stateThree, out var stateDepth));

                // Found at horizon 0
                Assert.AreEqual(2, stateDepth);
            }
        }

        [Test]
        public void SelectsWithNonDeterminism()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                s0 [label="s0 - complete"]
                s1 [label="s1 - complete"]
                s2 [label="s2 - complete"]
                s0 -> s1 [label="a1"]
                s0 -> s2 [label="a2"]
            } //todo this digraph does not support forked branches

            ASCII:
                +-------------------+  a1 p=e  +-------------------+
                |  s0 - incomplete  | -------> |  s1 - incomplete  |
                +-------------------+  |       +-------------------+
                                       |
                                       | a2 p=1-e
                                       v
                                     +---------------+
                                     | s2 - complete |
                                     +---------------+
            */
            const int rootState = -1;
            const int stateOne = 1;
            const int stateTwo = 2;
            const int actionOne = 1;
            const int actionTwo = 2;

            var builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
            builder.AddState(rootState)
                .AddAction(actionOne)
                .AddResultingState(stateOne, probability: float.Epsilon)
                .AddResultingState(stateTwo, probability: 1f - float.Epsilon, complete: true);

            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(rootState))
            {
                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = rootState,
                    StateDepthLookup = depthMap,
                };

                // Build graph
                builder.WithState(rootState)
                    .AddAction(actionTwo)
                    .AddResultingState(stateOne, probability: float.Epsilon)
                    .AddResultingState(stateTwo, probability: 1f - float.Epsilon, complete: true);

                searchContext.StateDepthLookup.TryAdd(rootState, 0);

                // Run selection
                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };
                selectJob.Schedule().Complete();

                // Job has selected one state for expansion
                Assert.AreEqual(1, m_AllExpandedStates.Length);

                // Root state selected for expansion
                Assert.IsTrue(m_AllExpandedStates[0] == stateOne);

                // No state selected has not been expanded
                Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

                // Unexpanded state is state 1
                Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateOne);

                // State has been added to search depth list
                Assert.IsTrue(searchContext.StateDepthLookup.TryGetValue(stateOne, out var stateDepth));

                // Found at horizon 1
                Assert.AreEqual(1, stateDepth);
            }
        }

        [Test]
        public void SelectsWithCycle()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                s0 [label="s0 - complete"]
                s1 [label="s1 - complete"]
                s2 [label="s2 - complete"]
                s0 -> s1 [label="a1"]
                s0 -> s2 [label="a2"]
            } //todo this digraph does not support forked branches

            ASCII:
                +-------------------+  a1 p=1-e  +-----------------+
                |  s0 - incomplete  | ---------> |  s1 - complete  |
                +-------------------+  |         +-----------------+
                   ^                   |
                   |                   | p=e
                   +-------------------+
            */
            const int rootState = -1;
            const int stateOne = 1;

            var builder = new PolicyGraphBuilder<int, int> { PolicyGraph = m_PolicyGraph };

            // Build graph
            builder.AddState(rootState)
                .AddAction(1)
                .AddResultingState(rootState, probability: float.Epsilon)
                .AddResultingState(stateOne, probability: 1f-float.Epsilon, complete: true);

            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(rootState))
            {

                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = rootState,
                    StateDepthLookup = depthMap,
                };

                // Run selection
                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };
                selectJob.Schedule().Complete();

                // Job has selected one state for expansion
                Assert.AreEqual(1, m_AllExpandedStates.Length);

                // Root state selected for expansion
                Assert.IsTrue(m_AllExpandedStates[0] == rootState);

                // No unexpanded states selected
                Assert.IsTrue(m_SelectedUnexpandedStates.Length == 0);

                // State has been added to search depth list
                Assert.IsTrue(searchContext.StateDepthLookup.TryGetValue(rootState, out var stateDepth));

                // Found at horizon 1
                Assert.AreEqual(1, stateDepth);
            }
        }

        [Test]
        public void SelectsMaximalActionValue()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                s0 [label="s0 - incomplete"]
                s1 [label="s1 - incomplete"]
                s2 [label="s2 - incomplete"]
                s0 -> s1 [label="a1"]
                s0 -> s2 [label="a2"]
            }

            ASCII:
                +-----------------+  a1   +-----------------+
                | s0 - incomplete | ----> | s1 - incomplete |
                +-----------------+       +-----------------+
                  |
                  | a2
                  v
                +-----------------+
                | s2 - incomplete |
                +-----------------+
            */
            const int rootState = -1;
            const int stateOne = 1;
            const int stateTwo = 2;
            const int actionOne = 1;
            const int actionTwo = 2;

            var builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
            var stateContext = builder.AddState(rootState);
            stateContext.AddAction(actionOne).AddResultingState(stateOne);
            stateContext.AddAction(actionTwo, actionValue: 1f).AddResultingState(stateTwo);

            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(rootState))
            {
                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = rootState,
                    StateDepthLookup = depthMap,
                };

                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };
                selectJob.Schedule().Complete();

                // Job has selected one state for expansion
                Assert.AreEqual(1, m_AllExpandedStates.Length);

                // Incomplete state selected for expansion
                Assert.IsTrue(m_AllExpandedStates[0] == stateTwo);

                // No state selected has not been expanded
                Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

                // Unexpanded state is state 1
                Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateTwo);

                // State has been added to search depth list
                Assert.IsTrue(searchContext.StateDepthLookup.TryGetValue(stateTwo, out var stateDepth));

                // Found at horizon 1
                Assert.AreEqual(1, stateDepth);
            }
        }

        [Test]
        public void SelectsActionWithLeastVisitCountUnderEqualValues()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                s0 [label="s0 - incomplete"]
                s1 [label="s1 - incomplete"]
                s2 [label="s2 - incomplete"]
                s0 -> s1 [label="a1"]
                s0 -> s2 [label="a2"]
            }

            ASCII:
                +-----------------+  a1   +-----------------+
                | s0 - incomplete | ----> | s1 - incomplete |
                +-----------------+       +-----------------+
                  |
                  | a2
                  v
                +-----------------+
                | s2 - incomplete |
                +-----------------+
            */
            const int rootState = -1;
            const int stateOne = 1;
            const int stateTwo = 2;
            const int actionOne = 1;
            const int actionTwo = 2;

            var builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
            var stateContext = builder.AddState(rootState, visitCount: 100);
            stateContext.AddAction(actionOne, visitCount: 100).AddResultingState(stateOne);
            stateContext.AddAction(actionTwo).AddResultingState(stateTwo);

            using (var depthMap = m_PolicyGraph.GetExpandedDepthMap(rootState))
            {
                var searchContext = new TestSearchContext()
                {
                    PolicyGraph = m_PolicyGraph,
                    RootStateKey = rootState,
                    StateDepthLookup = depthMap,
                };

                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    StateActionLookup = m_PolicyGraph.StateActionLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    ActionResultLookup = m_PolicyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                    AllSelectedStates = m_AllExpandedStates
                };
                selectJob.Schedule().Complete();

                // Job has selected one state for expansion
                Assert.AreEqual(1, m_AllExpandedStates.Length);

                // Incomplete state selected for expansion
                Assert.IsTrue(m_AllExpandedStates[0] == stateTwo);

                // No state selected has not been expanded
                Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

                // Unexpanded state is state 1
                Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateTwo);

                // State has been added to search depth list
                Assert.IsTrue(searchContext.StateDepthLookup.TryGetValue(stateTwo, out var stateDepth));

                // Found at horizon 1
                Assert.AreEqual(1, stateDepth);
            }
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    [Category("Performance")]
    class SelectionJobPerformanceTests
    {
        [Performance, UnityTest]
        public IEnumerator TestPerformanceOnLargeTree()
        {
            var policyGraph = PolicyGraphUtility.BuildTree(actionsPerState:2, resultsPerAction:1, depth: 10);
            var searchContext = new TestSearchContext()
            {
                RootStateKey = 0,
                PolicyGraph = policyGraph,
                StateDepthLookup = policyGraph.GetExpandedDepthMap(0),
            };

            var selectedUnexpandedStates = new NativeList<int>(1, Allocator.Persistent);
            var allExpandedStates = new NativeList<int>(1, Allocator.Persistent);

            yield return null;

            // Set up performance test
            Measure.Method(() =>
            {
                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = policyGraph.StateInfoLookup,
                    StateActionLookup = policyGraph.StateActionLookup,
                    ActionInfoLookup = policyGraph.ActionInfoLookup,
                    ResultingStateLookup = policyGraph.ResultingStateLookup,
                    ActionResultLookup = policyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = selectedUnexpandedStates,
                    AllSelectedStates = allExpandedStates
                };
                selectJob.Schedule().Complete();

            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).CleanUp(() =>
            {
                searchContext.StateDepthLookup.Dispose();
                searchContext.StateDepthLookup = policyGraph.GetExpandedDepthMap(0);

                selectedUnexpandedStates.Clear();
                allExpandedStates.Clear();
            }).Run();

            // Check performance times
            PerformanceUtility.AssertRange(0.04, 0.15);

            searchContext.Dispose();
            selectedUnexpandedStates.Dispose();
            allExpandedStates.Dispose();
        }

        [Performance, UnityTest]
        public IEnumerator TestPerformanceOnLargeGraph()
        {
            var policyGraph = PolicyGraphUtility.BuildLattice(midLatticeDepth: 10);
            var searchContext = new TestSearchContext()
            {
                RootStateKey = 0,
                PolicyGraph = policyGraph,
                StateDepthLookup = policyGraph.GetExpandedDepthMap(0),
            };

            var selectedUnexpandedStates = new NativeList<int>(1, Allocator.Persistent);
            var allExpandedStates = new NativeList<int>(1, Allocator.Persistent);

            yield return null;

            // Set up performance test
            Measure.Method(() =>
            {
                var selectJob = new SelectionJob<int, int>()
                {
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = policyGraph.StateInfoLookup,
                    StateActionLookup = policyGraph.StateActionLookup,
                    ActionInfoLookup = policyGraph.ActionInfoLookup,
                    ResultingStateLookup = policyGraph.ResultingStateLookup,
                    ActionResultLookup = policyGraph.ActionResultLookup,

                    SelectedUnexpandedStates = selectedUnexpandedStates,
                    AllSelectedStates = allExpandedStates
                };
                selectJob.Schedule().Complete();

            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).CleanUp(() =>
            {
                searchContext.StateDepthLookup.Dispose();
                searchContext.StateDepthLookup = policyGraph.GetExpandedDepthMap(0);

                selectedUnexpandedStates.Clear();
                allExpandedStates.Clear();
            }).Run();

            // Check performance times
            PerformanceUtility.AssertRange(0.03, 0.16);

            searchContext.Dispose();
            selectedUnexpandedStates.Dispose();
            allExpandedStates.Dispose();
        }
    }
}
#endif
