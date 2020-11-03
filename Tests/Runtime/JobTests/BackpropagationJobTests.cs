using System;
using NUnit.Framework;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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

        PlanGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> m_PlanGraph;
        PlanGraphBuilder<int,int> m_Builder;
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
                m_DepthMap = new NativeHashMap<int, int>(m_PlanGraph.Size, Allocator.TempJob);
                using (var queue = new NativeQueue<StateHorizonPair<int>>(Allocator.Temp))
                {
                    m_PlanGraph.GetExpandedDepthMap(k_RootState, m_DepthMap, queue);
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
                planGraph = m_PlanGraph,
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

                    // plan graph info
                    ActionLookup = m_PlanGraph.ActionLookup,
                    PredecessorGraph = m_PlanGraph.PredecessorGraph,
                    ResultingStateLookup = m_PlanGraph.ResultingStateLookup,
                    StateInfoLookup = m_PlanGraph.StateInfoLookup,
                    ActionInfoLookup = m_PlanGraph.ActionInfoLookup,
                    StateTransitionInfoLookup = m_PlanGraph.StateTransitionInfoLookup,

                    PredecessorStatesToUpdate = predecessorStates.AsParallelWriter(),
                }.Schedule(horizonStateList, default, jobHandle);

            }

            jobHandle = new UpdateCompleteStatusJob<int, int>
            {
                StatesToUpdate = predecessorStates,
                planGraph = m_PlanGraph,
            }.Schedule(jobHandle);

            jobHandle.Complete();

            m_SelectedStatesByHorizon.Dispose();
            predecessorStates.Dispose();
            horizonStateList.Dispose();
        }

        [SetUp]
        public void SetupPartialPlanGraph()
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
            m_PlanGraph = new PlanGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo>(10, 10, 10);
            m_Builder = new PlanGraphBuilder<int, int>() { planGraph = m_PlanGraph };
            m_SelectedStates = new NativeMultiHashMap<int, int>(1, Allocator.TempJob);

            // Build common plan graph
            var stateContext = m_Builder.AddState(k_RootState);
            stateContext.AddAction(k_ActionOne).AddResultingState(k_StateOne, transitionUtility: 0);
            stateContext.AddAction(k_ActionTwo).AddResultingState(k_StateTwo, transitionUtility: 1);

            m_Builder.WithState(k_StateOne).UpdateInfo(estimatedReward: 20);
            m_Builder.WithState(k_StateTwo).UpdateInfo(estimatedReward: 20);
        }

        [TearDown]
        public void TearDownContainers()
        {
            m_PlanGraph.Dispose();

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
            m_PlanGraph.StateInfoLookup.TryGetValue(k_RootState, out var stateInfo);
            m_PlanGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionOne), out var actionOneInfo);
            m_PlanGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionTwo), out var actionTwoInfo);

            // Tests -> No values should be updated.
            Assert.IsTrue(Math.Abs(stateInfo.CumulativeRewardEstimate.Average - 0f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(actionOneInfo.CumulativeRewardEstimate.Average - 0f) < float.Epsilon, "Incorrect action reward value.");
            Assert.IsTrue(Math.Abs(actionTwoInfo.CumulativeRewardEstimate.Average - 0f) < float.Epsilon, "Incorrect action reward value.");
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
            m_PlanGraph.StateInfoLookup.TryGetValue(k_RootState, out var stateInfo);
            m_PlanGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionOne), out var actionOneInfo);
            m_PlanGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionTwo), out var actionTwoInfo);

            Assert.IsTrue(Math.Abs(stateInfo.CumulativeRewardEstimate.Average -20f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(actionOneInfo.CumulativeRewardEstimate.Average - 19f) < float.Epsilon, "Incorrect action reward value.");
            Assert.IsTrue(Math.Abs(actionTwoInfo.CumulativeRewardEstimate.Average - 20f) < float.Epsilon, "Incorrect action reward value.");
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
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            m_PlanGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionOne), out var actionOneInfo);
            m_PlanGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionTwo), out _);

            Assert.IsTrue(stateOneInfo.SubplanIsComplete, "State with no actions is not marked complete.");
            Assert.IsTrue(stateOneInfo.CumulativeRewardEstimate.Approximately(default), "Incorrect reward value.");
            Assert.IsTrue(actionOneInfo.CumulativeRewardEstimate.Approximately(default), "Incorrect action reward value.");
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

            m_Builder.WithState(k_StateThree).UpdateInfo(estimatedReward: 25);
            m_Builder.WithState(k_StateFour).UpdateInfo(estimatedReward: 22);

            m_SelectedStates.Add(k_StateOne, 1);
            m_SelectedStates.Add(k_StateTwo, 1);

            // Act
            Backpropagate(1f);

            // Test
            m_PlanGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.CumulativeRewardEstimate.Average -25f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.CumulativeRewardEstimate.Average -25f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.CumulativeRewardEstimate.Average -23f) < float.Epsilon, "Incorrect reward value.");
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
            m_Builder.WithState(k_StateFour).UpdateInfo(estimatedReward: 25);

            m_SelectedStates.Add(k_StateThree, 2);

            // Act
            Backpropagate(1f);

            // Test
            m_PlanGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateThree, out var stateThreeInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.CumulativeRewardEstimate.Average -27f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.CumulativeRewardEstimate.Average -25f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.CumulativeRewardEstimate.Average -26f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateThreeInfo.CumulativeRewardEstimate.Average -25f) < float.Epsilon, "Incorrect reward value.");
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

            m_Builder.WithState(k_StateThree).UpdateInfo(estimatedReward: 25);
            m_Builder.WithState(k_StateFive).UpdateInfo(estimatedReward: 20);

            m_SelectedStates.Add(k_StateOne, 1);
            m_SelectedStates.Add(k_StateFour, 2);

            // Act
            Backpropagate(1f);

            // Test
            m_PlanGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateFour, out var stateFourInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.CumulativeRewardEstimate.Average -25f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.CumulativeRewardEstimate.Average -25f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.CumulativeRewardEstimate.Average -20f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateFourInfo.CumulativeRewardEstimate.Average -20f) < float.Epsilon, "Incorrect reward value.");
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
                .UpdateInfo(estimatedReward: 0f)
                .AddAction(k_ActionOne).AddResultingState(k_StateOne, transitionUtility: 15);

            m_DepthMap = new NativeHashMap<int, int>(m_PlanGraph.Size, Allocator.TempJob);
            using (var queue = new NativeQueue<StateHorizonPair<int>>(Allocator.Temp))
            {
                m_PlanGraph.GetExpandedDepthMap(k_RootState, m_DepthMap, queue);
            }
            m_DepthMap[k_StateOne] = 2; // visit state 1 twice

            m_SelectedStates.Add(k_StateOne, 2); // select state 1 at depth 2

            // Act
            Backpropagate(1f);

            // Test
            m_PlanGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionOne), out var actionOneInfo);
            m_PlanGraph.ActionInfoLookup.TryGetValue(new StateActionPair<int, int>(k_RootState, k_ActionTwo), out var actionTwoInfo);

            Assert.IsTrue(actionOneInfo.CumulativeRewardEstimate.Average > actionTwoInfo.CumulativeRewardEstimate.Average);
            // Note: Testing the reward values here is undesirable as the exact reward values depend on the
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
            m_Builder.WithState(k_StateOne).UpdateInfo(estimatedReward: 30, complete: false);

            m_SelectedStates.Add(k_StateOne, 1);

            // Act
            Backpropagate(1f);

            // Test
            m_PlanGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.CumulativeRewardEstimate.Average -30f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.CumulativeRewardEstimate.Average -30f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(stateOneInfo.SubplanIsComplete);
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
            m_PlanGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);

            Assert.AreEqual(100, rootStateInfo.CumulativeRewardEstimate.Average, "Incorrect reward value.");
            Assert.IsTrue(stateOneInfo.SubplanIsComplete);
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
            m_Builder.WithState(k_StateOne).UpdateInfo(estimatedReward: 30, complete: false);
            m_Builder.WithState(k_RootState).UpdateInfo(estimatedReward: 0, complete: false); // set value to test for later

            m_SelectedStates.Add(k_StateOne, 1);

            // Act
            Backpropagate(1f);

            // Test
            m_PlanGraph.StateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            m_PlanGraph.StateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);

            // As backup terminates early (at k_StateOne), the root node should not be updated. In truth, the root
            // state should have a reward value of 30, but we're testing that the value is not updated by this
            // process, so we're maintaining the original value of 0.
            Assert.IsTrue(Math.Abs(rootStateInfo.CumulativeRewardEstimate.Average - 0f) < float.Epsilon, "Incorrect reward value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.CumulativeRewardEstimate.Average - 30f) < float.Epsilon, "Incorrect reward value.");
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    using PerformanceTesting;

    [Category("Performance")]
    public class BackpropagationJobPerformanceTests
    {
        const int kRootState = 0;
        const int kActionsPerState = 2;
        const int kMaxDepth = 11;

        [Performance, Test]
        public void BackupFromMultipleStates()
        {
            PlanGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> planGraph = default;
            NativeHashMap<int, int> depthMap = default;
            NativeQueue<StateHorizonPair<int>> queue = default;
            NativeMultiHashMap<int, int> m_SelectedStates = default;
            var builder = new PlanGraphBuilder<int, int>();

            Measure.Method(() =>
            {
                var backpropJob = new BackpropagationJob<int, int>
                {
                    DiscountFactor = 1f,
                    DepthMap = depthMap,
                    planGraph = planGraph,
                    SelectedStates = m_SelectedStates,
                };
                backpropJob.Schedule().Complete();
                Assert.IsTrue(planGraph.StateInfoLookup[0].CumulativeRewardEstimate.Approximately(new BoundedValue(1,1,1)));
            }).SetUp(() =>
            {
                var nodeCount = PlanGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth);
                var leafNodeStart = PlanGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 1);
                var expandedNodeStart = PlanGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 2);

                m_SelectedStates = new NativeMultiHashMap<int, int>(leafNodeStart - expandedNodeStart, Allocator.TempJob);
                depthMap = new NativeHashMap<int, int>(nodeCount, Allocator.TempJob);
                queue = new NativeQueue<StateHorizonPair<int>>(Allocator.TempJob);
                planGraph = PlanGraphUtility.BuildTree(kActionsPerState, 1, kMaxDepth);
                planGraph.GetExpandedDepthMap(kRootState, depthMap, queue);

                // Provide the leaf nodes with some value to propagate to the root
                builder.planGraph = planGraph;
                for (var i = leafNodeStart; i < nodeCount; i++)
                {
                    builder.WithState(i).UpdateInfo(estimatedReward: 1);
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
                queue.Dispose();
                planGraph.Dispose();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.4, 0.7);
        }

        [Performance, Test]
        public void BackupFromMultipleStatesParallel()
        {
            var builder = new PlanGraphBuilder<int, int>();
            PlanGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> planGraph = default;

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

                        // Plan graph info
                        ActionLookup = planGraph.ActionLookup,
                        PredecessorGraph = planGraph.PredecessorGraph,
                        ResultingStateLookup = planGraph.ResultingStateLookup,
                        StateInfoLookup = planGraph.StateInfoLookup,
                        ActionInfoLookup = planGraph.ActionInfoLookup,
                        StateTransitionInfoLookup = planGraph.StateTransitionInfoLookup,

                        PredecessorStatesToUpdate = predecessorStates.AsParallelWriter(),
                    }.Schedule(horizonStateList, default, jobHandle);

                }

                jobHandle = new UpdateCompleteStatusJob<int, int>
                {
                    StatesToUpdate = predecessorStates,
                    planGraph = planGraph,
                }.Schedule(jobHandle);

                jobHandle.Complete();
                Assert.IsTrue(planGraph.StateInfoLookup[0].CumulativeRewardEstimate.Approximately(new BoundedValue(1,1,1)));

            }).SetUp(() =>
            {
                // Rebuild plan graph
                planGraph = PlanGraphUtility.BuildTree(kActionsPerState, 1, kMaxDepth);
                builder.planGraph = planGraph;

                // Provide the leaf nodes with some value to propagate to the root
                var nodeCount = PlanGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth);
                var leafNodeStart = PlanGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 1);
                var expandedNodeStart = PlanGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 2);

                // Provide the leaf nodes with some value to propagate to the root
                builder.planGraph = planGraph;
                for (var i = leafNodeStart; i < nodeCount; i++)
                {
                    builder.WithState(i).UpdateInfo(estimatedReward: 1);
                }

                // Ignore the leaf nodes as those would be expanded this turn; We backup the parents of those nodes
                for (var i = expandedNodeStart; i < leafNodeStart; i++)
                {
                    m_SelectedStatesByHorizon.Add(kMaxDepth - 2, i);
                }

                // Misc job containers
                var numStates = planGraph.StateInfoLookup.Count();
                m_SelectedStatesByHorizon.Capacity = numStates;
                predecessorStates.Capacity = numStates;
                horizonStateList.Capacity = numStates;

            }).CleanUp(() =>
            {
                planGraph.Dispose();

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
