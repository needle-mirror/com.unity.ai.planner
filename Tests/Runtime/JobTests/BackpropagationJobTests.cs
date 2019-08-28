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
    class BackpropagationJobTests
    {
        const int k_RootState = -1;
        const int k_StateOne = 1;
        const int k_StateTwo = 2;
        const int k_StateThree = 3;
        const int k_StateFour = 4;
        const int k_StateFive = 5;

        const int k_ActionOne = 1;
        const int k_ActionTwo = 2;

        PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult> m_PolicyGraph;
        PolicyGraphBuilder<int,int> m_Builder;
        NativeHashMap<int, int> m_DepthMap;

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
            m_PolicyGraph = new PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult>(10, 10);

            m_Builder = new PolicyGraphBuilder<int, int>() { PolicyGraph = m_PolicyGraph };
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
            m_DepthMap.Dispose();
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
            var selectedStates = new NativeList<int>(0, Allocator.TempJob);
            m_DepthMap = m_PolicyGraph.GetExpandedDepthMap(k_RootState);

            var backpropJob = new BackpropagationJob<int, int>()
            {
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = selectedStates,
            };

            backpropJob.Schedule().Complete();

            var stateInfoLookup = m_PolicyGraph.StateInfoLookup;
            var actionInfoLookup = m_PolicyGraph.ActionInfoLookup;

            stateInfoLookup.TryGetValue(k_RootState, out var stateInfo);
            actionInfoLookup.TryGetValue((k_RootState, k_ActionOne), out var actionOneInfo);
            actionInfoLookup.TryGetValue((k_RootState, k_ActionTwo), out var actionTwoInfo);

            // Tests -> No values should be updated.
            Assert.IsTrue(Math.Abs(stateInfo.PolicyValue - 0f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(actionOneInfo.ActionValue - 0f) < float.Epsilon, "Incorrect action value.");
            Assert.IsTrue(Math.Abs(actionTwoInfo.ActionValue - 0f) < float.Epsilon, "Incorrect action value.");

            selectedStates.Dispose();
        }

        [Test]
        public void BackupDepthOneGraph()
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
            var selectedStates = new NativeList<int>(1, Allocator.TempJob);
            selectedStates.Add(k_RootState);

            m_DepthMap = m_PolicyGraph.GetExpandedDepthMap(k_RootState);

            var backpropJob = new BackpropagationJob<int, int>()
            {
                DiscountFactor = 0.95f,
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = selectedStates,
            };

            backpropJob.Schedule().Complete();

            var stateInfoLookup = m_PolicyGraph.StateInfoLookup;
            var actionInfoLookup = m_PolicyGraph.ActionInfoLookup;

            stateInfoLookup.TryGetValue(k_RootState, out var stateInfo);
            actionInfoLookup.TryGetValue((k_RootState, k_ActionOne), out var actionOneInfo);
            actionInfoLookup.TryGetValue((k_RootState, k_ActionTwo), out var actionTwoInfo);

            // Tests
            Assert.IsTrue(Math.Abs(stateInfo.PolicyValue - 20f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(actionOneInfo.ActionValue - 19f) < float.Epsilon, "Incorrect action value.");
            Assert.IsTrue(Math.Abs(actionTwoInfo.ActionValue - 20f) < float.Epsilon, "Incorrect action value.");

            selectedStates.Dispose();
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

            m_DepthMap = m_PolicyGraph.GetExpandedDepthMap(k_RootState);

            var selectedStates = new NativeList<int>(2, Allocator.TempJob);
            selectedStates.Add(k_StateOne);
            selectedStates.Add(k_StateTwo);

            // Act
            var backpropJob = new BackpropagationJob<int, int>()
            {
                DiscountFactor = 1f,
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = selectedStates,
            };
            backpropJob.Schedule().Complete();

            // Test
            var stateInfoLookup = m_PolicyGraph.StateInfoLookup;
            stateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            stateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            stateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue - 25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue - 25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.PolicyValue - 23f) < float.Epsilon, "Incorrect policy value.");

            selectedStates.Dispose();
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

            m_DepthMap = m_PolicyGraph.GetExpandedDepthMap(k_RootState);

            var selectedStates = new NativeList<int>(1, Allocator.TempJob);
            selectedStates.Add(k_StateThree);

            // Act
            var backpropJob = new BackpropagationJob<int, int>()
            {
                DiscountFactor = 1f,
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = selectedStates,
            };
            backpropJob.Schedule().Complete();

            // Test
            var stateInfoLookup = m_PolicyGraph.StateInfoLookup;
            stateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            stateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            stateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);
            stateInfoLookup.TryGetValue(k_StateThree, out var stateThreeInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue - 27f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue - 25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.PolicyValue - 26f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateThreeInfo.PolicyValue - 25f) < float.Epsilon, "Incorrect policy value.");

            selectedStates.Dispose();
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

            m_DepthMap = m_PolicyGraph.GetExpandedDepthMap(k_RootState);

            var selectedStates = new NativeList<int>(2, Allocator.TempJob);
            selectedStates.Add(k_StateOne);
            selectedStates.Add(k_StateFour);

            // Act
            var backpropJob = new BackpropagationJob<int, int>()
            {
                DiscountFactor = 1f,
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = selectedStates,
            };
            backpropJob.Schedule().Complete();

            // Test
            var stateInfoLookup = m_PolicyGraph.StateInfoLookup;
            stateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            stateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);
            stateInfoLookup.TryGetValue(k_StateTwo, out var stateTwoInfo);
            stateInfoLookup.TryGetValue(k_StateFour, out var stateFourInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue - 25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue - 25f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateTwoInfo.PolicyValue - 20f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateFourInfo.PolicyValue - 20f) < float.Epsilon, "Incorrect policy value.");

            selectedStates.Dispose();
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
            m_Builder.WithState(k_StateOne)
                .UpdateInfo(policyValue: 0f)
                .AddAction(k_ActionOne).AddResultingState(k_StateOne, transitionUtility: 15);

            m_DepthMap = m_PolicyGraph.GetExpandedDepthMap(k_RootState);
            m_DepthMap.Remove(k_StateOne);
            m_DepthMap.TryAdd(k_StateOne, 2); // visit state 1 twice

            var selectedStates = new NativeList<int>(1, Allocator.TempJob);
            selectedStates.Add(k_StateOne);

            // Act
            var backpropJob = new BackpropagationJob<int, int>()
            {
                DiscountFactor = 1f,
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = selectedStates,
            };
            backpropJob.Schedule().Complete();

            // Test
            var actionInfoLookup = m_PolicyGraph.ActionInfoLookup;
            actionInfoLookup.TryGetValue((k_RootState, k_ActionOne), out var actionOneInfo);
            actionInfoLookup.TryGetValue((k_RootState, k_ActionTwo), out var actionTwoInfo);

            Assert.IsTrue(actionOneInfo.ActionValue > actionTwoInfo.ActionValue);
            // Note: Testing the policy values here is undesirable as the exact policy values depend on the
            // order in which states are updated. If StateOne is updated before RootState, the value is updated
            // three times; otherwise the value is updated twice.

            selectedStates.Dispose();
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
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionOne).AddResultingState(k_StateThree, complete: true, transitionUtility: 25);
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionTwo).AddResultingState(k_StateFour, complete: true, transitionUtility: 30);
            m_Builder.WithState(k_StateOne).UpdateInfo(policyValue: 30, complete: false);

            m_DepthMap = m_PolicyGraph.GetExpandedDepthMap(k_RootState);

            var selectedStates = new NativeList<int>(1, Allocator.TempJob);
            selectedStates.Add(k_StateOne);

            // Act
            var backpropJob = new BackpropagationJob<int, int>()
            {
                DiscountFactor = 1f,
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = selectedStates,
            };
            backpropJob.Schedule().Complete();

            var stateInfoLookup = m_PolicyGraph.StateInfoLookup;
            stateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            stateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);

            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue - 30f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue - 30f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(stateOneInfo.Complete);

            selectedStates.Dispose();
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
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionOne).AddResultingState(k_StateThree, complete: false, transitionUtility: 25);
            m_Builder.WithState(k_StateOne)
                .AddAction(k_ActionTwo).AddResultingState(k_StateFour, complete: false, transitionUtility: 30);
            m_Builder.WithState(k_StateOne).UpdateInfo(policyValue: 30, complete: false);
            m_Builder.WithState(k_RootState).UpdateInfo(policyValue: 0, complete: false); // set value to test for later

            m_DepthMap = m_PolicyGraph.GetExpandedDepthMap(k_RootState);

            var selectedStates = new NativeList<int>(1, Allocator.TempJob);
            selectedStates.Add(k_StateOne);

            // Act
            var backpropJob = new BackpropagationJob<int, int>()
            {
                DiscountFactor = 1f,
                DepthMap = m_DepthMap,
                PolicyGraph = m_PolicyGraph,
                SelectedStates = selectedStates,
            };
            backpropJob.Schedule().Complete();

            var stateInfoLookup = m_PolicyGraph.StateInfoLookup;
            stateInfoLookup.TryGetValue(k_RootState, out var rootStateInfo);
            stateInfoLookup.TryGetValue(k_StateOne, out var stateOneInfo);

            // As backup terminates early (at k_StateOne), the root node should not be updated. In truth, the root
            // state should have a policy value of 30, but we're testing that the value is not updated by this
            // process, so we're maintaining the original value of 0.
            Assert.IsTrue(Math.Abs(rootStateInfo.PolicyValue - 0f) < float.Epsilon, "Incorrect policy value.");
            Assert.IsTrue(Math.Abs(stateOneInfo.PolicyValue - 30f) < float.Epsilon, "Incorrect policy value.");

            selectedStates.Dispose();
        }
    }
}

#if ENABLE_PERFORMANCE_TESTS
namespace Unity.AI.Planner.Tests.Performance
{
    [Category("Performance")]
    public class BackpropagationJobPerformanceTests
    {
        [Performance, Test]
        public void BackupFromMultipleStates()
        {
            const int kRootState = 0;
            const int kActionsPerState = 2;
            const int kMaxDepth = 11;
            const int kSelectCount = 10;

            PolicyGraph<int, StateInfo, int, ActionInfo, ActionResult> policyGraph = default;
            NativeHashMap<int, int> depthMap = default;
            NativeList<int> selectedStates = default;
            var builder = new PolicyGraphBuilder<int, int>();

            Measure.Method(() =>
            {
                var backpropJob = new BackpropagationJob<int, int>()
                {
                    DiscountFactor = 1f,
                    DepthMap = depthMap,
                    PolicyGraph = policyGraph,
                    SelectedStates = selectedStates,
                };
                backpropJob.Schedule().Complete();
            }).SetUp(() =>
            {
                policyGraph = PolicyGraphUtility.BuildTree(kActionsPerState, 1, kMaxDepth);
                depthMap = policyGraph.GetExpandedDepthMap(kRootState);

                var nodeCount = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth);
                var leafNodeStart = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 1);
                selectedStates = new NativeList<int>(kSelectCount, Allocator.TempJob);

                // Provide the leaf nodes with some value to propagate to the root
                builder.PolicyGraph = policyGraph;
                var leafNodeCount = nodeCount - leafNodeStart;
                var leafNodeEnd = leafNodeStart + leafNodeCount;
                for (var i = leafNodeStart; i < leafNodeEnd; i++)
                {
                    builder.WithState(i).UpdateInfo(policyValue: 1);
                }

                // Ignore the leaf nodes as those would be expanded this turn; We backup the parents of those nodes
                var expandedHorizonNodeStart = PolicyGraphUtility.GetTotalNodeCountForTreeDepth(kActionsPerState, kMaxDepth - 2);
                var expandedHorizonNodeCount = leafNodeCount - expandedHorizonNodeStart;
                var stride = expandedHorizonNodeCount / kSelectCount;
                for (var i = 0; i < kSelectCount; i++)
                {
                    var state = expandedHorizonNodeStart + stride * i;
                    selectedStates.Add(state);
                }
            }).CleanUp(() =>
            {
                selectedStates.Dispose();
                depthMap.Dispose();
                policyGraph.Dispose();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.29, 0.46);
        }
    }
}
#endif
