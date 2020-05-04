using System;
using NUnit.Framework;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

#if ENABLE_PERFORMANCE_TESTS
using Unity.PerformanceTesting;
#endif

namespace Unity.AI.Planner.Tests.Unit
{
    [Category("Unit")]
    [TestFixture(BackpropagationJobMode.Sequential)]
    [TestFixture(BackpropagationJobMode.Parallel)]
    class BackpropagationJobTests
    {
        BackpropagationJobMode m_JobMode;

        const int k_RootState = -1;
        const int k_StateOne = 1;
        const int k_StateTwo = 2;
        const int k_StateThree = 3;
        const int k_StateFour = 4;
        const int k_StateFive = 5;

        const int k_ActionOne = 1;
        const int k_ActionTwo = 2;

        PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> m_PolicyGraph;
        PolicyGraphBuilder<int,int> m_Builder;
        NativeHashMap<int, int> m_DepthMap;
        NativeMultiHashMap<int, int> m_SelectedStates;

        public BackpropagationJobTests(BackpropagationJobMode jobMode)
        {
            m_JobMode = jobMode;
        }

        void Backpropagate(float discountFactor = 1f)
        {
            if (!m_DepthMap.IsCreated)
            {
                m_DepthMap = new NativeHashMap<int, int>(m_PolicyGraph.Size, Allocator.TempJob);
                using (var queue = new NativeQueue<StateHorizonPair<int>>(Allocator.Temp))
                {
                    m_PolicyGraph.GetExpandedDepthMap(k_RootState, m_DepthMap, queue);
                }
            }

            switch (m_JobMode)
            {
                case BackpropagationJobMode.Sequential:
                    BackpropagateSequential(discountFactor);
                    return;
                case BackpropagationJobMode.Parallel:
                    BackpropagateParallel(discountFactor);
                    return;
            }
        }

        void BackpropagateSequential(float discountFactor)
        {
            var backpropJob = new BackpropagationJob<int, int>
            {
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = m_SelectedStates,
                DiscountFactor = discountFactor,
            };
            backpropJob.Schedule().Complete();
        }

        void BackpropagateParallel(float discountFactor)
        {
            JobHandle jobHandle = default;
            int maxDepth = 0;
            using (var depths = m_DepthMap.GetValueArray(Allocator.Temp))
            {
                for (int i = 0; i < depths.Length; i++)
                    maxDepth = math.max(maxDepth, depths[i]);
            }

            // Containers
            var m_SelectedStatesByHorizon = new NativeMultiHashMap<int, int>(m_DepthMap.Count(), Allocator.TempJob);
            var predecessorStates  = new NativeHashMap<int, byte>(m_DepthMap.Count(), Allocator.TempJob);
            var horizonStateList = new NativeList<int>(m_DepthMap.Count(), Allocator.TempJob);

            jobHandle = new UpdateDepthMapAndResizeContainersJob<int>
            {
                SelectedStates = m_SelectedStates,
                MaxDepth = maxDepth,

                DepthMap = m_DepthMap,
                SelectedStatesByHorizon = m_SelectedStatesByHorizon,
                PredecessorStates = predecessorStates,
                HorizonStateList = horizonStateList,
            }.Schedule(jobHandle);

            // horizons of backprop
            for (int horizon = maxDepth + 1; horizon >= 0; horizon--)
            {
                // Prepare info
                jobHandle = new PrepareBackpropagationHorizon<int>
                {
                    Horizon = horizon,
                    SelectedStatesByHorizon = m_SelectedStatesByHorizon,
                    PredecessorInputStates = predecessorStates,
                    OutputStates = horizonStateList,
                }.Schedule(jobHandle);

                // Compute updated values
                jobHandle = new ParallelBackpropagationJob<int, int>
                {
                    DiscountFactor = discountFactor,
                    StatesToUpdate = horizonStateList.AsDeferredJobArray(),

                    // policy graph info
                    ActionLookup = m_PolicyGraph.ActionLookup,
                    PredecessorGraph = m_PolicyGraph.PredecessorGraph,
                    ResultingStateLookup = m_PolicyGraph.ResultingStateLookup,
                    StateInfoLookup = m_PolicyGraph.StateInfoLookup,
                    ActionInfoLookup = m_PolicyGraph.ActionInfoLookup,
                    StateTransitionInfoLookup = m_PolicyGraph.StateTransitionInfoLookup,

                    PredecessorStatesToUpdate = predecessorStates.AsParallelWriter(),
                }.Schedule(horizonStateList, default, jobHandle);

            }

            jobHandle = new UpdateCompleteStatusJob<int, int>
            {
                StatesToUpdate = predecessorStates,
                PolicyGraph = m_PolicyGraph,
            }.Schedule(jobHandle);

            jobHandle.Complete();

            m_SelectedStatesByHorizon.Dispose();
            predecessorStates.Dispose();
            horizonStateList.Dispose();
        }

        [SetUp]
        public void SetupPartialPolicyGraph()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=20"]
                s2 [label="s2 - inc, V=20"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=20 |
                +----------------+          +----------------+
                  |
                  | a2 Q=0
                  v
                +----------------+
                | s2 - inc, V=20 |
                +----------------+
            */
            m_PolicyGraph = new PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo>(10, 10, 10);
            m_Builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
            m_SelectedStates = new NativeMultiHashMap<int, int>(1, Allocator.TempJob);

            // Build common policy graph
            var stateContext = m_Builder.AddState(k_RootState);
            stateContext.AddAction(k_ActionOne).AddResultingState(k_StateOne, transitionUtility: 0);
            stateContext.AddAction(k_ActionTwo).AddResultingState(k_StateTwo, transitionUtility: 1);

            m_Builder.WithState(k_StateOne).UpdateInfo(policyValue: 20);
            m_Builder.WithState(k_StateTwo).UpdateInfo(policyValue: 20);
        }

        [TearDown]
        public void TearDownContainers()
        {
            m_PolicyGraph.Dispose();

            if (m_DepthMap.IsCreated)
                m_DepthMap.Dispose();

            if (m_SelectedStates.IsCreated)
                m_SelectedStates.Dispose();
        }

        [Test]
        public void BackupFromNoSelectedStates()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=20"]
                s2 [label="s2 - inc, V=20"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=20 |
                +----------------+          +----------------+
                  |
                  | a2 Q=0
                  v
                +----------------+
                | s2 - inc, V=20 |
                +----------------+
            */
            // Act
            Backpropagate();

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var stateInfo);
            m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionOne), out var actionOneInfo);
            m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionTwo), out var actionTwoInfo);

            // Tests -> No values should be updated.
            Assert.IsTrue(Math.Abs(stateInfo.PolicyValue.Average - 0f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(actionOneInfo.ActionValue.Average - 0f) < float.Epsilon, "Incorrect action value.");
            Assert.IsTrue(Math.Abs(actionTwoInfo.ActionValue.Average - 0f) < float.Epsilon, "Incorrect action value.");
        }

        [Test]
        public void BackupExpandedRoot()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=20"]
                s2 [label="s2 - inc, V=20"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=20 |
                +----------------+          +----------------+
                  |
                  | a2 Q=0
                  v
                +----------------+
                | s2 - inc, V=20 |
                +----------------+
            */
            // Setup
            m_SelectedStates.Add(k_RootState, 0);

            // Act
            Backpropagate(0.95f);

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var stateInfo);
            m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionOne), out var actionOneInfo);
            m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionTwo), out var actionTwoInfo);

            Assert.IsTrue(Math.Abs(stateInfo.PolicyValue.Average -20f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(actionOneInfo.ActionValue.Average - 19f) < float.Epsilon, "Incorrect action value.");
            Assert.IsTrue(Math.Abs(actionTwoInfo.ActionValue.Average - 20f) < float.Epsilon, "Incorrect action value.");
        }

        [Test]
        public void BackupFromNonTerminalStateWithNoActions()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=20"]
                s2 [label="s2 - inc, V=20"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=20 |
                +----------------+          +----------------+
                  |
                  | a2 Q=0
                  v
                +----------------+
                | s2 - inc, V=20 |
                +----------------+
            */
            // Setup
            m_SelectedStates.Add(k_StateOne, 1);

            // Act
            Backpropagate(0.95f);

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionOne), out var actionOneInfo);
            m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionTwo), out _);

            Assert.IsTrue(stateOneInfo.SubgraphComplete, "State with no actions is not marked complete.");
            Assert.IsTrue(stateOneInfo.PolicyValue.Approximately(default), "Incorrect policy value.");
            Assert.IsTrue(actionOneInfo.ActionValue.Approximately(default), "Incorrect action value.");
        }

        [Test]
        public void BackupOverMultipleStepsToRoot()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=20"]
                s2 [label="s2 - inc, V=20"]
                s3 [label="s3 - inc, V=25"]
                s4 [label="s4 - inc, V=22"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
                s1 -> s3 [label="a1 Q=0"]
                s1 -> s4 [label="a2 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=20 | -------> | s3 - inc, V=25 |
                +----------------+          +----------------+          +----------------+
                  |                           |
                  | a2 Q=0                    | a2 Q=0
                  v                           v
                +----------------+          +----------------+
                | s2 - inc, V=20 |          | s4 - inc, V=22 |
                +----------------+          +----------------+
            */
            // Setup
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionOne).AddResultingState(k_StateThree, transitionUtility: 0);
            m_Builder.WithState(k_StateTwo)
                .AddAction(k_ActionTwo).AddResultingState(k_StateFour, transitionUtility: 1);

            m_Builder.WithState(k_StateThree).UpdateInfo(policyValue: 25);
            m_Builder.WithState(k_StateFour).UpdateInfo(policyValue: 22);

            m_SelectedStates.Add(k_StateOne, 1);
            m_SelectedStates.Add(k_StateTwo, 1);

            // Act
            Backpropagate(1f);

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue.Average -25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue.Average -25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.PolicyValue.Average -23f) < float.Epsilon, "Incorrect policy value.");
        }

        [Test]
        public void BackupThroughJoinedBranches()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=20"]
                s2 [label="s2 - inc, V=20"]
                s3 [label="s3 - inc, V=0"]
                s4 [label="s4 - inc, V=25"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
                s1 -> s3 [label="a1 Q=0"]
                s2 -> s3 [label="a2 Q=0"]
                s3 -> s4 [label="a1 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=20 |
                +----------------+          +----------------+
                  |                           |
                  | a2 Q=0                    | a1 Q=0
                  v                           v
                +----------------+  a2 Q=0  +----------------+  a1 Q=0  +----------------+
                | s2 - inc, V=20 | -------> | s3 - inc, V=0  | -------> | s4 - inc, V=25 |
                +----------------+          +----------------+          +----------------+
            */
            // Setup
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionOne).AddResultingState(k_StateThree, transitionUtility: 0);
            m_Builder.WithState(k_StateTwo)
                .AddAction(k_ActionTwo).AddResultingState(k_StateThree, transitionUtility: 1);
            m_Builder.WithState(k_StateThree)
                .AddAction(k_ActionOne).AddResultingState(k_StateFour, transitionUtility: 0);
            m_Builder.WithState(k_StateFour).UpdateInfo(policyValue: 25);

            m_SelectedStates.Add(k_StateThree, 2);

            // Act
            Backpropagate(1f);

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateThree, out var stateThreeInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue.Average -27f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue.Average -25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.PolicyValue.Average -26f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateThreeInfo.PolicyValue.Average -25f) < float.Epsilon, "Incorrect policy value.");
        }

        [Test]
        public void BackupFromStatesAtVariedDepths()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=20"]
                s2 [label="s2 - inc, V=20"]
                s3 [label="s3 - inc, V=25"]
                s4 [label="s4 - inc, V=0"]
                s5 [label="s5 - inc, V=20"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
                s1 -> s3 [label="a1 Q=0"]
                s2 -> s4 [label="a1 Q=0"]
                s4 -> s5 [label="a1 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=20 | -------> | s3 - inc, V=25 |
                +----------------+          +----------------+          +----------------+
                  |
                  | a2 Q=0
                  v
                +----------------+  a2 Q=0  +----------------+  a1 Q=0  +----------------+
                | s2 - inc, V=20 | -------> | s4 - inc, V=0  | -------> | s5 - inc, V=20 |
                +----------------+          +----------------+          +----------------+
            */
            // Setup
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionOne).AddResultingState(k_StateThree, transitionUtility: 0);
            m_Builder.WithState(k_StateTwo)
                .AddAction(k_ActionOne).AddResultingState(k_StateFour, transitionUtility: 0);
            m_Builder.WithState(k_StateFour)
                .AddAction(k_ActionOne).AddResultingState(k_StateFive, transitionUtility: 0);

            m_Builder.WithState(k_StateThree).UpdateInfo(policyValue: 25);
            m_Builder.WithState(k_StateFive).UpdateInfo(policyValue: 20);

            m_SelectedStates.Add(k_StateOne, 1);
            m_SelectedStates.Add(k_StateFour, 2);

            // Act
            Backpropagate(1f);

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateFour, out var stateFourInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue.Average -25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue.Average -25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.PolicyValue.Average -20f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateFourInfo.PolicyValue.Average -20f) < float.Epsilon, "Incorrect policy value.");
        }

        [Test]
        public void BackupThroughCycle()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=0"]
                s2 [label="s2 - inc, V=20"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
                s1 -> s1 [label="a1 Q=0, r=15"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=0  | ----+
                +----------------+          +----------------+     |
                  |                             ^                  | a1 Q=0, r=15
                  | a2 Q=0                      |                  |
                  v                             +------------------+
                +----------------+
                | s2 - inc, V=20 |
                +----------------+
            */
            // Setup
            m_Builder.WithState(k_StateOne)
                .UpdateInfo(policyValue: 0f)
                .AddAction(k_ActionOne).AddResultingState(k_StateOne, transitionUtility: 15);

            m_DepthMap = new NativeHashMap<int, int>(m_PolicyGraph.Size, Allocator.TempJob);
            using (var queue = new NativeQueue<StateHorizonPair<int>>(Allocator.Temp))
            {
                m_PolicyGraph.GetExpandedDepthMap(k_RootState, m_DepthMap, queue);
            }
            m_DepthMap[k_StateOne] = 2; // visit state 1 twice

            m_SelectedStates.Add(k_StateOne, 2); // select state 1 at depth 2

            // Act
            Backpropagate(1f);

            // Test
            m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionOne), out var actionOneInfo);
            m_PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionTwo), out var actionTwoInfo);

            Assert.IsTrue(actionOneInfo.ActionValue.Average > actionTwoInfo.ActionValue.Average);
            // Note: Testing the policy values here is undesirable as the exact policy values depend on the
            // order in which states are updated. If StateOne is updated before RootState, the value is updated
            // three times; otherwise the value is updated twice.
        }

        [Test]
        public void BackupContinuesIfCompleteStatusChanges()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=30"]
                s2 [label="s2 - inc, V=20"]
                s3 [label="s3 - com, V=0"]
                s4 [label="s4 - com, V=0"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
                s1 -> s3 [label="a1 Q=0"]
                s1 -> s4 [label="a2 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=30 | -------> | s3 - com, V=0  |
                +----------------+          +----------------+          +----------------+
                  |                           |
                  | a2 Q=0                    | a2 Q=0
                  v                           v
                +----------------+          +----------------+
                | s2 - inc, V=20 |          | s4 - com, V=0  |
                +----------------+          +----------------+
            */
            // Setup
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionOne).AddResultingState(k_StateThree, complete: true, transitionUtility: 25);
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionTwo).AddResultingState(k_StateFour, complete: true, transitionUtility: 30);
            m_Builder.WithState(k_StateOne).UpdateInfo(policyValue: 30, complete: false);

            m_SelectedStates.Add(k_StateOne, 1);

            // Act
            Backpropagate(1f);

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue.Average -30f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue.Average -30f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(stateOneInfo.SubgraphComplete);
        }

        [Test]
        public void BackupSkipsIncompleteDominatedSubgraphs()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=30"]
                s2 [label="s2 - inc, V=20"]
                s3 [label="s3 - com, V=0"]
                s4 [label="s4 - com, V=0"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
                s1 -> s3 [label="a1 Q=0"]
                s1 -> s4 [label="a2 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=30 | -------> | s3 - com, V=0  |
                +----------------+          +----------------+          +----------------+
                  |                           |
                  | a2 Q=0                    | a2 Q=0
                  v                           v
                +----------------+          +----------------+
                | s2 - inc, V=20 |          | s4 - com, V=0  |
                +----------------+          +----------------+
            */
            // Setup
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionOne).AddResultingState(k_StateThree, complete: true, value: new float3(100, 100, 100));
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionTwo).AddResultingState(k_StateFour, complete: false, value: new float3(-1,-1,-1));
            m_Builder.WithState(k_StateOne).UpdateInfo(complete: false);

            m_SelectedStates.Add(k_StateOne, 1);

            // Act
            Backpropagate(1f);

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);

            Assert.AreEqual(100, rootStateInfo.PolicyValue.Average, "Incorrect policy value.");
            Assert.IsTrue(stateOneInfo.SubgraphComplete);
        }

        [Test]
        public void BackupTerminatesEarly()
        {
            /*
            DOT format: (Visual editor for convenience: http://magjac.com/graphviz-visual-editor/)
            digraph {
                rankdir= LR
                r  [label="r - inc, V=0"]
                s1 [label="s1 - inc, V=30"]
                s2 [label="s2 - inc, V=20"]
                s3 [label="s3 - inc, V=0"]
                s4 [label="s4 - inc, V=0"]
                r -> s1 [label="a1 Q=0"]
                r -> s2 [label="a2 Q=0"]
                s1 -> s3 [label="a1 Q=0"]
                s1 -> s4 [label="a2 Q=0"]
            }

            ASCII:
                +----------------+  a1 Q=0  +----------------+  a1 Q=0  +----------------+
                | r - inc, V=0   | -------> | s1 - inc, V=30 | -------> | s3 - inc, V=0  |
                +----------------+          +----------------+          +----------------+
                  |                           |
                  | a2 Q=0                    | a2 Q=0
                  v                           v
                +----------------+          +----------------+
                | s2 - inc, V=20 |          | s4 - inc, V=0  |
                +----------------+          +----------------+
            */
            // Setup
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionOne).AddResultingState(k_StateThree, complete: false, transitionUtility: 25);
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionTwo).AddResultingState(k_StateFour, complete: false, transitionUtility: 30);
            m_Builder.WithState(k_StateOne).UpdateInfo(policyValue: 30, complete: false);
            m_Builder.WithState(k_RootState).UpdateInfo(policyValue: 0, complete: false); // set value to test for later

            m_SelectedStates.Add(k_StateOne, 1);

            // Act
            Backpropagate(1f);

            // Test
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PolicyGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);

            // As backup terminates early (at k_StateOne), the root node should not be updated. In truth, the root
            // state should have a policy value of 30, but we're testing that the value is not updated by this
            // process, so we're maintaining the original value of 0.
            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue.Average - 0f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue.Average - 30f) < float.Epsilon, "Incorrect policy value.");
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    [Category("Performance")]
    public class BackpropagationJobPerformanceTests
    {
        const int kRootState = 0;
        const int kActionsPerState = 2;
        const int kMaxDepth = 11;

        [Performance, Test]
        public void BackupFromMultipleStates()
        {
            PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> policyGraph = default;
            NativeHashMap<int, int> depthMap = default;
            NativeMultiHashMap<int, int> m_SelectedStates = default;
            var builder = new PolicyGraphBuilder<int, int>();

            Measure.Method(() =>
            {
                var backpropJob = new BackpropagationJob<int, int>
                {
                    DiscountFactor = 1f,
                    DepthMap = depthMap,
                    PolicyGraph = policyGraph,
                    SelectedStates = m_SelectedStates,
                };
                backpropJob.Schedule().Complete();
                Assert.IsTrue(policyGraph.StateInfoLookup[0].PolicyValue.Approximately(new BoundedValue(1,1,1)));
            }).SetUp(() =>
            {
                policyGraph = PolicyGraphUtility.BuildTree(kActionsPerState, 1, kMaxDepth);
                depthMap = policyGraph.GetExpandedDepthMap(kRootState);

                var nodeCount = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth);
                var leafNodeStart = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 1);
                var expandedNodeStart = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 2);
                m_SelectedStates = new NativeMultiHashMap<int, int>(leafNodeStart - expandedNodeStart, Allocator.TempJob);

                // Provide the leaf nodes with some value to propagate to the root
                builder.PolicyGraph = policyGraph;
                for (var i = leafNodeStart; i < nodeCount; i++)
                {
                    builder.WithState(i).UpdateInfo(policyValue: 1);
                }

                // Ignore the leaf nodes as those would be expanded this turn; We backup the parents of those nodes
                for (var i = expandedNodeStart; i < leafNodeStart; i++)
                {
                    m_SelectedStates.Add(i, kMaxDepth - 2);
                }
            }).CleanUp(() =>
            {
                m_SelectedStates.Dispose();
                depthMap.Dispose();
                policyGraph.Dispose();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.4, 0.7);
        }

        [Performance, Test]
        public void BackupFromMultipleStatesParallel()
        {
            var builder = new PolicyGraphBuilder<int, int>();
            PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> policyGraph = default;

            var m_SelectedStatesByHorizon = new NativeMultiHashMap<int, int>(kMaxDepth, Allocator.TempJob);
            var predecessorStates = new NativeHashMap<int, byte>(1, Allocator.TempJob);
            var horizonStateList = new NativeList<int>(1, Allocator.TempJob);


            Measure.Method(() =>
            {
                JobHandle jobHandle = default;
                // horizons of backprop
                for (int horizon = kMaxDepth - 2; horizon >= 0; horizon--)
                {
                    // Prepare info
                    jobHandle = new PrepareBackpropagationHorizon<int>
                    {
                        Horizon = horizon,
                        SelectedStatesByHorizon = m_SelectedStatesByHorizon,
                        PredecessorInputStates = predecessorStates,
                        OutputStates = horizonStateList,
                    }.Schedule(jobHandle);

                    // Compute updated values
                    jobHandle = new ParallelBackpropagationJob<int, int>
                    {
                        DiscountFactor = 1f,
                        StatesToUpdate = horizonStateList.AsDeferredJobArray(),

                        // policy graph info
                        ActionLookup = policyGraph.ActionLookup,
                        PredecessorGraph = policyGraph.PredecessorGraph,
                        ResultingStateLookup = policyGraph.ResultingStateLookup,
                        StateInfoLookup = policyGraph.StateInfoLookup,
                        ActionInfoLookup = policyGraph.ActionInfoLookup,
                        StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup,

                        PredecessorStatesToUpdate = predecessorStates.AsParallelWriter(),
                    }.Schedule(horizonStateList, default, jobHandle);

                }

                jobHandle = new UpdateCompleteStatusJob<int, int>
                {
                    StatesToUpdate = predecessorStates,
                    PolicyGraph = policyGraph,
                }.Schedule(jobHandle);

                jobHandle.Complete();
                Assert.IsTrue(policyGraph.StateInfoLookup[0].PolicyValue.Approximately(new BoundedValue(1,1,1)));

            }).SetUp(() =>
            {
                // Rebuild policy graph
                policyGraph = PolicyGraphUtility.BuildTree(kActionsPerState, 1, kMaxDepth);
                builder.PolicyGraph = policyGraph;

                // Provide the leaf nodes with some value to propagate to the root
                var nodeCount = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth);
                var leafNodeStart = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 1);
                var expandedNodeStart = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 2);

                // Provide the leaf nodes with some value to propagate to the root
                builder.PolicyGraph = policyGraph;
                for (var i = leafNodeStart; i < nodeCount; i++)
                {
                    builder.WithState(i).UpdateInfo(policyValue: 1);
                }

                // Ignore the leaf nodes as those would be expanded this turn; We backup the parents of those nodes
                for (var i = expandedNodeStart; i < leafNodeStart; i++)
                {
                    m_SelectedStatesByHorizon.Add(kMaxDepth - 2, i);
                }

                // Misc job containers
                var numStates = policyGraph.StateInfoLookup.Length;
                m_SelectedStatesByHorizon.Capacity = numStates;
                predecessorStates.Capacity = numStates;
                horizonStateList.Capacity = numStates;

            }).CleanUp(() =>
            {
                policyGraph.Dispose();

                m_SelectedStatesByHorizon.Clear();
                predecessorStates.Clear();
                horizonStateList.Clear();

            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            m_SelectedStatesByHorizon.Dispose();
            predecessorStates.Dispose();
            horizonStateList.Dispose();

            PerformanceUtility.AssertRange(0.4, 0.7);
        }
    }
}
#endif
