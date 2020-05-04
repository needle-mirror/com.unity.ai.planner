using System;
using System.Collections.Generic;
using GraphVisualizer;
using Unity.AI.Planner;
using UnityEngine;

namespace UnityEditor.AI.Planner.Visualizer
{
    class PlanVisualizer : Graph, IPlanVisualizer
    {
        public int MaxDepth { get; set; }
        public int MaxChildrenNodes { get; set; }
        public IVisualizerNode RootNodeOverride { get; set; }
        public IList<object> ForcedExpandedNodes { get; } = new List<object>();

        IPlanExecutor m_PlanExecutor;

        public PlanVisualizer(IPlanExecutor planExecutor)
        {
            m_PlanExecutor = planExecutor;
        }

        protected override IEnumerable<Node> GetChildren(Node node)
        {
            foreach (var childNode in ((BaseNode)node).GetNodeChildren(MaxDepth * 2, MaxChildrenNodes, ForcedExpandedNodes))
                yield return childNode;
        }

        protected override void Populate()
        {
            if (m_PlanExecutor.Plan == null || !m_PlanExecutor.Plan.TryGetStateInfo(m_PlanExecutor.CurrentPlanStateKey, out _))
                PopulateWithExecutorState();
            else if (RootNodeOverride == null )
                PopulateWithRoot();
            else
                PopulateWithRootOverride();
        }

        void PopulateWithExecutorState()
        {
            AddNode(CreateStateNode(m_PlanExecutor.CurrentExecutorStateKey, 1f, true, rootState: true));
        }

        void PopulateWithRoot()
        {
            var rootStateKey = m_PlanExecutor.CurrentPlanStateKey;

            // If action is in progress only display this action from the current root state
            var actionKey = m_PlanExecutor.CurrentActionKey;
            if (!actionKey.Equals(default))
            {
                var stateRoot = CreateStateNode(rootStateKey, -1, rootState: true);
                var visualizerActionNode = CreateActionNode((rootStateKey, actionKey),1f, true);
                AddNodeHierarchy(visualizerActionNode);

                stateRoot.AddChild(visualizerActionNode);
                AddNode(stateRoot);
                return;
            }

            // If the root has not yet been expanded, root the graph at the policy graph node.
            var rootPolicyNode = CreateStateNode(rootStateKey, 1f, true, rootState: true);
            AddNodeHierarchy(rootPolicyNode);
        }

        void PopulateWithRootOverride()
        {
            var parentVisualizerNode = RootNodeOverride.parent;

            if (RootNodeOverride is ActionNode visActionNode)
            {
                var visualizerNode = CreateActionNode(visActionNode.StateActionKey, 1f, true);
                AddNodeHierarchy(visualizerNode);

                var parentState = (StateNode)parentVisualizerNode;
                var backNode = CreateStateNode(parentState.StateKey, -1f, expansion: true);
                backNode.AddChild(visualizerNode);
                AddNode(backNode);
            }
            else if (RootNodeOverride is StateNode visStateNode)
            {
                var visualizerNode = CreateStateNode(visStateNode.StateKey, 1f, true);
                AddNodeHierarchy(visualizerNode);

                ActionNode parentAction = (ActionNode)parentVisualizerNode;

                var backNode = CreateActionNode(parentAction.StateActionKey, -1f, expansion: true);
                backNode.AddChild(visualizerNode);
                AddNode(backNode);
            }
        }

        BaseNode CreateActionNode((IStateKey, IActionKey) stateActionKey, float weight, bool active = false, bool expansion = false)
        {
            return new ActionNode(m_PlanExecutor, stateActionKey, expansion, weight, active);
        }

        BaseNode CreateStateNode(IStateKey stateKey, float weight, bool active = false, bool expansion = false, bool rootState = false)
        {
            return new StateNode(m_PlanExecutor, stateKey, expansion, weight, active, rootState);
        }
    }
}
