using NUnit.Framework;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using TestSearchContext = Unity.AI.Planner.SearchContext<int, int, Unity.AI.Planner.Tests.TestStateManager, int, Unity.AI.Planner.Tests.TestStateDataContext>;

namespace Unity.AI.Planner.Tests.Unit
{
    [Category("Unit")]
    [TestFixture(SelectionJobMode.Sequential)]
    [TestFixture(SelectionJobMode.Parallel)]
    class SelectionJobTests
    {
        SelectionJobMode m_JobMode;

        const int rootState = -1;

        const int stateOne = 1;
        const int stateTwo = 2;
        const int stateThree = 3;
        const int actionOne = 1;
        const int actionTwo = 2;

        PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> m_PolicyGraph;
        PolicyGraphBuilder<int,int> m_Builder;
        NativeList<int> m_SelectedUnexpandedStates;
        NativeMultiHashMap<int, int> m_SelectedStateHorizons;
        NativeHashMap<int, int> m_DepthMap;

        public SelectionJobTests(SelectionJobMode jobMode)
        {
            m_JobMode = jobMode;
        }

        void SelectStates()
        {
            if (!m_DepthMap.IsCreated)
            {
                m_DepthMap = new NativeHashMap<int, int>(m_PolicyGraph.Size, Allocator.TempJob);
                using (var queue = new NativeQueue<StateHorizonPair<int>>(Allocator.Temp))
                {
                    m_PolicyGraph.GetReachableDepthMap(rootState, m_DepthMap, queue);
                }
            }

            switch (m_JobMode)
            {
                case SelectionJobMode.Sequential:
                    SelectSequential();
                    return;
                case SelectionJobMode.Parallel:
                    SelectParallel();
                    return;
            }
        }

        void SelectSequential()
        {
            var selectJob = new SelectionJob<int, int>()
            {
                SearchBudget = 1,
                RootStateKey = rootState,
                StateDepthLookup = m_DepthMap,
                StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                ActionLookup = m_PolicyGraph.ActionLookup,
                ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                StateTransitionInfoLookup = m_PolicyGraph.StateTransitionInfoLookup,

                SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                AllSelectedStates = m_SelectedStateHorizons
            };
            selectJob.Schedule().Complete();
        }

        void SelectParallel()
        {
            int maxDepth = 0;
            using (var depths = m_DepthMap.GetValueArray(Allocator.Temp))
            {
                for (int i = 0; i < depths.Length; i++)
                    maxDepth = math.max(maxDepth, depths[i]);
            }

            var inputStates = new NativeList<int>(1, Allocator.TempJob);
            inputStates.Add(rootState);
            var inputBudgets = new NativeList<int>(1, Allocator.TempJob);
            inputBudgets.Add(1);

            var outputStateBudgets = new NativeMultiHashMap<int, int>(1, Allocator.TempJob);
            var selectedUnexpanded = new NativeHashMap<int, byte>(1, Allocator.TempJob);

            JobHandle jobHandle = default;
            for (int iteration = 0; iteration <= maxDepth; iteration++)
            {
                // Selection job
                jobHandle = new ParallelSelectionJob<int, int>()
                {
                    StateDepthLookup = m_DepthMap,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    ActionLookup = m_PolicyGraph.ActionLookup,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    StateTransitionInfoLookup = m_PolicyGraph.StateTransitionInfoLookup,

                    Horizon = iteration,
                    InputStates = inputStates.AsDeferredJobArray(),
                    InputBudgets = inputBudgets.AsDeferredJobArray(),

                    OutputStateBudgets = outputStateBudgets.AsParallelWriter(),
                    SelectedStateHorizons = m_SelectedStateHorizons.AsParallelWriter(),
                    SelectedUnexpandedStates = selectedUnexpanded.AsParallelWriter(),
                }.Schedule(inputStates, default, jobHandle);

                // Collect output job
                jobHandle = new CollectAndAssignSelectionBudgets<int>
                {
                    InputStateBudgets = outputStateBudgets,
                    OutputStates = inputStates,
                    OutputBudgets = inputBudgets,
                }.Schedule(jobHandle);
            }

            jobHandle.Complete();

            using (var keys = selectedUnexpanded.GetKeyArray(Allocator.TempJob))
            {
                foreach (var key in keys)
                    m_SelectedUnexpandedStates.Add(key);
            }

            selectedUnexpanded.Dispose();
            inputStates.Dispose();
            inputBudgets.Dispose();
            outputStateBudgets.Dispose();
        }

        [SetUp]
        public void SetupContainers()
        {
            m_SelectedUnexpandedStates = new NativeList<int>(1, Allocator.TempJob);
            m_SelectedStateHorizons = new NativeMultiHashMap<int, int>(1, Allocator.TempJob);
            m_PolicyGraph = new PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo>(1, 1, 1);
            m_Builder = new PolicyGraphBuilder<int, int> { PolicyGraph = m_PolicyGraph };
        }

        [TearDown]
        public void TeardownContainers()
        {
            m_SelectedUnexpandedStates.Dispose();
            m_SelectedStateHorizons.Dispose();
            m_PolicyGraph.Dispose();

            if (m_DepthMap.IsCreated)
                m_DepthMap.Dispose();
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
            // Setup
            // Add root state to policy graph
            m_Builder.AddState(rootState);

            // Act
            SelectStates();

            // Test
            // Job has selected one state for expansion
            Assert.AreEqual(1, m_SelectedStateHorizons.Count());

            // Root state selected for expansion
            Assert.IsTrue(m_SelectedStateHorizons.ContainsKey(rootState));

            // No state selected has not been expanded
            Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

            // Unexpanded state is state 1
            Assert.IsTrue(m_SelectedUnexpandedStates[0] == rootState);
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
            // Setup
            var stateContext = m_Builder.AddState(rootState);
            stateContext.AddAction(actionOne).AddResultingState(stateOne);
            stateContext.AddAction(actionTwo, complete: true).AddResultingState(stateTwo, complete: true);

            // Act
            SelectStates();

            // Test
            // Job has selected one state for expansion
            Assert.AreEqual(1, m_SelectedStateHorizons.Count());

            // Incomplete state selected for expansion
            Assert.IsTrue(m_SelectedStateHorizons.ContainsKey(stateOne));

            // No state selected has not been expanded
            Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

            // Unexpanded state is state 1
            Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateOne);
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
            // Setup
            var rootStateContext = m_Builder.AddState(rootState, complete: true);
            rootStateContext.AddAction(actionOne, complete: true).AddResultingState(stateOne, complete: true);
            rootStateContext.AddAction(actionTwo, complete: true).AddResultingState(stateTwo, complete: true);

            // Act
            SelectStates();

            // Test
            Assert.AreEqual(0, m_SelectedStateHorizons.Count());
            Assert.AreEqual(0, m_SelectedUnexpandedStates.Length);
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
            // Setup
            var rootStateContext = m_Builder.AddState(rootState);
            rootStateContext.AddAction(actionOne).AddResultingState(stateOne);
            rootStateContext.AddAction(actionTwo).AddResultingState(stateTwo);

            m_Builder.WithState(stateOne).AddAction(actionOne).AddResultingState(stateThree);
            m_Builder.WithState(stateTwo).AddAction(actionOne).AddResultingState(stateThree);

            // Act
            SelectStates();

            // Test
            // Job has selected one state for expansion
            Assert.AreEqual(1, m_SelectedStateHorizons.Count());

            // Root state selected for expansion
            Assert.IsTrue(m_SelectedStateHorizons.ContainsKey(stateThree));

            // No state selected has not been expanded
            Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

            // Unexpanded state is state 1
            Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateThree);
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
            // Setup
            m_Builder.AddState(rootState)
                .AddAction(actionOne)
                .AddResultingState(stateOne, probability: float.Epsilon)
                .AddResultingState(stateTwo, probability: 1f - float.Epsilon, complete: true);
            m_Builder.WithState(rootState)
                .AddAction(actionTwo)
                .AddResultingState(stateOne, probability: float.Epsilon)
                .AddResultingState(stateTwo, probability: 1f - float.Epsilon, complete: true);

            // Act
            SelectStates();

            // Test
            // Job has selected one state for expansion
            Assert.AreEqual(1, m_SelectedStateHorizons.Count());

            // Root state selected for expansion
            Assert.IsTrue(m_SelectedStateHorizons.ContainsKey(stateOne));

            // No state selected has not been expanded
            Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

            // Unexpanded state is state 1
            Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateOne);
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
            // Setup
            m_Builder.AddState(rootState)
                .AddAction(1)
                .AddResultingState(rootState, probability: float.Epsilon)
                .AddResultingState(stateOne, probability: 1f-float.Epsilon, complete: true);

            // Act
            SelectStates();

            // Test
            // Job has selected one state for expansion
            Assert.AreEqual(1, m_SelectedStateHorizons.Count());

            // Root state selected for expansion
            Assert.IsTrue(m_SelectedStateHorizons.ContainsKey(rootState));

            // No unexpanded states selected
            Assert.IsTrue(m_SelectedUnexpandedStates.Length == 0);
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
            // Setup
            var stateContext = m_Builder.AddState(rootState);
            stateContext.AddAction(actionOne).AddResultingState(stateOne);
            stateContext.AddAction(actionTwo, actionValue: 1f).AddResultingState(stateTwo);

            // Act
            SelectStates();

            // Test
            // Job has selected one state for expansion
            Assert.AreEqual(1, m_SelectedStateHorizons.Count());

            // Incomplete state selected for expansion
            Assert.IsTrue(m_SelectedStateHorizons.ContainsKey(stateTwo));

            // No state selected has not been expanded
            Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

            // Unexpanded state is state 1
            Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateTwo);
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
            // Setup
            var stateContext = m_Builder.AddState(rootState, visitCount: 100);
            stateContext.AddAction(actionOne, actionValue: new BoundedValue(0, 0, 0)).AddResultingState(stateOne);
            stateContext.AddAction(actionTwo, actionValue: new BoundedValue(0, 1, 2)).AddResultingState(stateTwo);

            // Act
            SelectStates();

            // Test
            // Job has selected one state for expansion
            Assert.AreEqual(1, m_SelectedStateHorizons.Count());

            // Incomplete state selected for expansion
            Assert.IsTrue(m_SelectedStateHorizons.ContainsKey(stateTwo));

            // No state selected has not been expanded
            Assert.IsTrue(m_SelectedUnexpandedStates.Length == 1);

            // Unexpanded state is state 1
            Assert.IsTrue(m_SelectedUnexpandedStates[0] == stateTwo);
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    using System.Collections;
    using Unity.PerformanceTesting;
    using UnityEngine.TestTools;

    [Category("Performance")]
    class SelectionJobPerformanceTests
    {
        [Performance, UnityTest]
        public IEnumerator TestPerformanceOnLargeTree()
        {
            var policyGraph = PolicyGraphUtility.BuildTree(actionsPerState: 2, resultsPerAction: 1, depth: 10);
            var searchContext = new TestSearchContext()
            {
                RootStateKey = 0,
                PolicyGraph = policyGraph,
                StateDepthLookup = policyGraph.GetExpandedDepthMap(0),
            };

            var selectedUnexpandedStates = new NativeList<int>(1, Allocator.Persistent);
            var allExpandedStates = new NativeMultiHashMap<int, int>(1, Allocator.Persistent);

            yield return null;

            // Set up performance test
            Measure.Method(() =>
            {
                var selectJob = new SelectionJob<int, int>()
                {
                    SearchBudget = 1,
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = policyGraph.StateInfoLookup,
                    ActionLookup = policyGraph.ActionLookup,
                    ActionInfoLookup = policyGraph.ActionInfoLookup,
                    ResultingStateLookup = policyGraph.ResultingStateLookup,
                    StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup,

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

            searchContext.Dispose();
            selectedUnexpandedStates.Dispose();
            allExpandedStates.Dispose();

            // Check performance times
            PerformanceUtility.AssertRange(0.04, 0.15);
        }

        [Performance, UnityTest]
        public IEnumerator TestPerformanceOnLargeGraph()
        {
            var policyGraph = PolicyGraphUtility.BuildLattice(10);
            var searchContext = new TestSearchContext
            {
                RootStateKey = 0,
                PolicyGraph = policyGraph,
                StateDepthLookup = policyGraph.GetExpandedDepthMap(0),
            };

            var selectedUnexpandedStates = new NativeList<int>(1, Allocator.Persistent);
            var allExpandedStates = new NativeMultiHashMap<int, int>(1, Allocator.Persistent);

            yield return null;

            // Set up performance test
            Measure.Method(() =>
            {
                var selectJob = new SelectionJob<int, int>()
                {
                    SearchBudget = 1,
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = policyGraph.StateInfoLookup,
                    ActionLookup = policyGraph.ActionLookup,
                    ActionInfoLookup = policyGraph.ActionInfoLookup,
                    ResultingStateLookup = policyGraph.ResultingStateLookup,
                    StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup,

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

            searchContext.Dispose();
            selectedUnexpandedStates.Dispose();
            allExpandedStates.Dispose();

            // Check performance times
            PerformanceUtility.AssertRange(0.03, 0.10);
        }

        [Performance, UnityTest]
        public IEnumerator TestPerformanceOnLargeGraphBudget10()
        {
            var policyGraph = PolicyGraphUtility.BuildLattice(midLatticeDepth: 10);
            var searchContext = new TestSearchContext()
            {
                RootStateKey = 0,
                PolicyGraph = policyGraph,
                StateDepthLookup = policyGraph.GetExpandedDepthMap(0),
            };

            var selectedUnexpandedStates = new NativeList<int>(1, Allocator.Persistent);
            var allExpandedStates = new NativeMultiHashMap<int, int>(1, Allocator.Persistent);

            yield return null;

            // Set up performance test
            Measure.Method(() =>
            {
                var selectJob = new SelectionJob<int, int>()
                {
                    SearchBudget = 10,
                    RootStateKey = searchContext.RootStateKey,
                    StateDepthLookup = searchContext.StateDepthLookup,
                    StateInfoLookup = policyGraph.StateInfoLookup,
                    ActionLookup = policyGraph.ActionLookup,
                    ActionInfoLookup = policyGraph.ActionInfoLookup,
                    ResultingStateLookup = policyGraph.ResultingStateLookup,
                    StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup,

                    SelectedUnexpandedStates = selectedUnexpandedStates,
                    AllSelectedStates = allExpandedStates
                };
                selectJob.Schedule().Complete();

            }).WarmupCount(1).MeasurementCount(1).IterationsPerMeasurement(1).CleanUp(() =>
            {
                searchContext.StateDepthLookup.Dispose();
                searchContext.StateDepthLookup = policyGraph.GetExpandedDepthMap(0);

                selectedUnexpandedStates.Clear();
                allExpandedStates.Clear();
            }).Run();

            searchContext.Dispose();
            selectedUnexpandedStates.Dispose();
            allExpandedStates.Dispose();

            // Check performance times
            PerformanceUtility.AssertRange(0.00, 5);
        }

        [Performance, UnityTest]
        public IEnumerator TestPerformanceOnLargeGraphBudget10Parallel()
        {
            var policyGraph = PolicyGraphUtility.BuildLattice(10);
            var depthMap = policyGraph.GetReachableDepthMap(0);
            var searchContext = new TestSearchContext()
            {
                RootStateKey = 0,
                PolicyGraph = policyGraph,
                StateDepthLookup = depthMap,
            };

            int budget = 10;
            int size = math.min(budget, depthMap.Length);
            UnityEngine.Assertions.Assert.IsTrue(size > 0);

            var inputStates = new NativeList<int>(size, Allocator.TempJob);
            inputStates.Add(0);
            var inputBudgets = new NativeList<int>(size, Allocator.TempJob);
            inputBudgets.Add(budget);

            var outputStateBudgets = new NativeMultiHashMap<int, int>(size, Allocator.TempJob);
            var m_SelectedStateHorizons = new NativeMultiHashMap<int, int>(size, Allocator.TempJob);
            var m_SelectedUnexpandedStates = new NativeHashMap<int, byte>(size, Allocator.TempJob);

            // Determine max number of job iterations
            int maxDepth = 0;
            using (var depths = depthMap.GetValueArray(Allocator.Temp))
            {
                for (int i = 0; i < depths.Length; i++)
                    maxDepth = math.max(maxDepth, depths[i]);
            }

            yield return null;

            // Set up performance test
            Measure.Method(() =>
            {
                JobHandle lastHandle = default;
                for (int iteration = 0; iteration <= maxDepth; iteration++)
                {
                    // Selection job
                    lastHandle = new ParallelSelectionJob<int, int>
                    {
                        StateDepthLookup = searchContext.StateDepthLookup,
                        StateInfoLookup = policyGraph.StateInfoLookup,
                        ActionInfoLookup = policyGraph.ActionInfoLookup,
                        ActionLookup = policyGraph.ActionLookup,
                        ResultingStateLookup = policyGraph.ResultingStateLookup,
                        StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup,

                        Horizon = iteration,
                        InputStates = inputStates.AsDeferredJobArray(),
                        InputBudgets = inputBudgets.AsDeferredJobArray(),

                        OutputStateBudgets = outputStateBudgets.AsParallelWriter(),
                        SelectedStateHorizons = m_SelectedStateHorizons.AsParallelWriter(),
                        SelectedUnexpandedStates = m_SelectedUnexpandedStates.AsParallelWriter(),
                    }.Schedule(inputStates, default, lastHandle);

                    // Collect output job
                    lastHandle = new CollectAndAssignSelectionBudgets<int>
                    {
                        InputStateBudgets = outputStateBudgets,
                        OutputStates = inputStates,
                        OutputBudgets = inputBudgets,
                    }.Schedule(lastHandle);
                }

                // Run jobs
                lastHandle.Complete();

            }).WarmupCount(1).MeasurementCount(3).IterationsPerMeasurement(1).CleanUp(() =>
            {
                inputStates.Clear();
                inputStates.Add(0);

                inputBudgets.Clear();
                inputBudgets.Add(budget);

                outputStateBudgets.Clear();
                m_SelectedStateHorizons.Clear();
                m_SelectedUnexpandedStates.Clear();
            }).Run();


            searchContext.Dispose();
            inputStates.Dispose();
            inputBudgets.Dispose();
            outputStateBudgets.Dispose();
            m_SelectedStateHorizons.Dispose();
            m_SelectedUnexpandedStates.Dispose();

            // Check performance times
            PerformanceUtility.AssertRange(0.00, 5);
        }

    }
}
#endif
