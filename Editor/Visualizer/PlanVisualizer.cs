using System;
using System.Collections.Generic;
using System.Text;
using GraphVisualizer;
using Unity.AI.Planner;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;

namespace UnityEditor.AI.Planner.Visualizer
{
    abstract class BaseNode : Node, IVisualizerNode
    {
        public bool ExpansionNode { get; }
        public string Label { get; }

        protected IPlanInternal m_Plan;

        protected BaseNode(IPlanInternal plan, string label, object content, bool expansion = false,  float weight = 1, bool active = false)
            : base(content, weight, active)
        {
            Label = label;
            ExpansionNode = expansion;
            m_Plan = plan;
        }

        public abstract IEnumerable<Node> GetNodeChildren(int maxDepth, int maxChildrenNodes);

        protected virtual string GetExpansionString()
        {
            return "\u2026";
        }

        public override string GetContentTypeName()
        {
            if (ExpansionNode)
                return GetExpansionString();

            return Label ?? base.GetContentTypeName();
        }

        protected static string InfoString(string key, double value) => string.Format(
                Math.Abs(value) < 100000.0 ? "<b>{0}:</b> {1:0.000}" : "<b>{0}:</b> {1:E4}", key, value);

        protected static string InfoString(string key, int value) => $"<b>{key}:</b> {value:D}";

        protected static string InfoString(string key, object value) => "<b>" + key + ":</b> " + (value ?? string.Empty);
    }

    class ActionNode : BaseNode
    {
        public (IStateKey, IActionKey) StateActionKey => ((IStateKey, IActionKey))content;

        struct Successor : IComparable<Successor>
        {
            public ActionResult ActionResult;
            public IStateKey StateKey;

            public int CompareTo(Successor other) => other.ActionResult.Probability.CompareTo(ActionResult.Probability);
        }

        // Local cache
        List<(ActionResult, IStateKey)> s_ActionResults = new List<(ActionResult, IStateKey)>();
        List<Successor> s_Successors = new List<Successor>();


        public ActionNode(IPlanInternal plan, (IStateKey, IActionKey) stateActionKey, bool expansion = false, float weight = 1, bool active = false)
            : base(plan, plan.GetActionName(stateActionKey.Item2), stateActionKey, expansion, weight, active)
        {
        }

        public override Color GetColor()
        {
            if (!string.IsNullOrEmpty(Label))
            {
                float h = (float)Math.Abs(Label.GetHashCode()) / int.MaxValue;
                return Color.HSVToRGB(h, 0.6f, 1.0f);
            }

            return base.GetColor();
        }

        protected override string GetExpansionString()
        {
            if (weight < 0f)
                return base.GetExpansionString(); // Back to parent node

            var actionResultCount = m_Plan.GetActionResults(StateActionKey, null);
            return $"{actionResultCount} State(s)";
        }

        public override string ToString()
        {
            if (ExpansionNode)
                return base.ToString();

            var actionInfo = m_Plan.GetActionInfo(StateActionKey);
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine(Label);

            sb.AppendLine();
            sb.AppendLine(InfoString("Action Value", actionInfo.ActionValue));
            sb.AppendLine(InfoString("Visit Count", actionInfo.VisitCount));
            sb.AppendLine(InfoString("Complete", actionInfo.Complete));

            SortSuccessorStates(s_ActionResults, ref s_Successors, out _, out _);

            sb.AppendLine();
            var successor = s_Successors[0];
            sb.AppendLine(InfoString("Resulting state", $"{successor.StateKey.Label}"));
            sb.AppendLine($"Probability: {successor.ActionResult.Probability}");
            sb.AppendLine($"Transition Utility Value: {successor.ActionResult.TransitionUtilityValue}");

            // TODO arguments?

            return sb.ToString();
        }

        static void SortSuccessorStates(List<(ActionResult, IStateKey)> actionResults, ref List<Successor> successors, out float minProbability, out float maxProbability)
        {
            // Grab all child nodes and sort them
            minProbability = float.MaxValue;
            maxProbability = float.MinValue;
            successors.Clear();
            for (var i = 0; i < actionResults.Count; i++)
            {
                var actionResultPair = actionResults[i];
                var actionResult = actionResultPair.Item1;
                var resultingState = actionResultPair.Item2;

                var resultProbability = actionResult.Probability;
                minProbability = Mathf.Min(minProbability, resultProbability);
                maxProbability = Mathf.Max(maxProbability, resultProbability);

                successors.Add(new Successor()
                {
                    ActionResult = actionResult,
                    StateKey = resultingState
                });
            }

            successors.Sort();
        }

        public override IEnumerable<Node> GetNodeChildren(int maxDepth, int maxChildrenNodes)
        {
            m_Plan.GetActionResults(StateActionKey, s_ActionResults);

            // Handle current node
            if (maxDepth > 0 && depth > maxDepth)
            {
                if (depth == maxDepth + 1 && s_ActionResults.Count > 0)
                {
                    var expansionNode = new ActionNode(m_Plan, StateActionKey, true,
                        Mathf.Min(weight, float.Epsilon));
                    yield return expansionNode;
                }

                yield break;
            }

            var successors = new List<Successor>();
            SortSuccessorStates(s_ActionResults, ref successors, out var minProbability, out var maxProbability);

            // Yield children
            var c = 0;
            foreach (var successor in successors)
            {
                if (c >= maxChildrenNodes)
                    yield break;

                var probability = successor.ActionResult.Probability;
                var nodeWeight = Math.Abs(probability - 1.0f) < 10e-6 ?
                    float.MaxValue :
                    Mathf.InverseLerp(minProbability, maxProbability, probability);

                yield return new StateNode(m_Plan, successor.StateKey, weight: nodeWeight);

                c++;
            }
        }
    }

    class StateNode : BaseNode
    {
        public IStateKey StateKey => (IStateKey)content;

        struct Successor : IComparable<Successor>
        {
            public IActionKey ActionKey;
            public ActionInfo ActionInfo;

            public int CompareTo(Successor other) => other.ActionInfo.ActionValue.CompareTo(ActionInfo.ActionValue);
        }

        // Local cache
        static List<IActionKey> s_Actions = new List<IActionKey>();
        static List<Successor> s_Successors = new List<Successor>();

        public StateNode(IPlanInternal plan, IStateKey stateKey, bool expansion = false, float weight = 1, bool active = false)
            : base(plan, stateKey.Label, stateKey, expansion, weight, active)
        {
        }

        public override Type GetContentType()
        {
            var stateInfo = m_Plan.GetStateInfo(StateKey);
            return stateInfo.GetType();
        }

        protected override string GetExpansionString()
        {
            if (weight < 0f)
                return base.GetExpansionString(); // Back to parent node

            var actionsCount = m_Plan.GetActions(StateKey, null);
            return $"{actionsCount} Action(s)";
        }

        public override string ToString()
        {
            if (ExpansionNode)
                return base.ToString();

            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine(Label);

            var stateInfo = m_Plan.GetStateInfo(StateKey);
            sb.AppendLine();
            sb.AppendLine(InfoString("Policy Value", stateInfo.PolicyValue));
            sb.AppendLine(InfoString("Visit Count", stateInfo.VisitCount));

            sb.AppendLine();
            sb.AppendLine(InfoString("Complete", stateInfo.Complete));

            sb.AppendLine();
            if (m_Plan.GetOptimalAction(StateKey, out var optimalActionKey))
                sb.AppendLine(InfoString("Optimal Action", m_Plan.GetActionName(optimalActionKey)));

            m_Plan.GetActions(StateKey, s_Actions);

            SortSuccessorActions(s_Actions, ref s_Successors, out _, out _);

            foreach (var successor in s_Successors)
            {
                var actionKey = successor.ActionKey;
                var stateActionKey = (StateKey, actionKey);

                var actionInfo = m_Plan.GetActionInfo(stateActionKey);
                sb.AppendLine($"    {m_Plan.GetActionName(actionKey)}: {actionInfo.ActionValue}");
            }

            sb.AppendLine();
            var stateManager = m_Plan.StateManager;
            if (stateManager != null) // null when stateManager does not implement IStateManagerInternal
            {
                var stateData = stateManager.GetStateData(StateKey, false);
                var stateString = stateData.ToString();
                if (GUILayout.Button("Copy State", EditorStyles.miniButton))
                    stateString.CopyToClipboard();

                sb.AppendLine(InfoString("State", null));
                sb.AppendLine(stateString);
            }

            return sb.ToString();
        }

        void SortSuccessorActions(List<IActionKey> actions, ref List<Successor> successors, out float minActionValue, out float maxActionValue)
        {
            // Grab all child nodes and sort them
            minActionValue = float.MaxValue;
            maxActionValue = float.MinValue;
            successors.Clear();
            for (var i = 0; i < actions.Count; i++)
            {
                var actionKey = actions[i];
                var actionInfo = m_Plan.GetActionInfo((StateKey, actionKey));
                var actionValue = actionInfo.ActionValue;
                minActionValue = Mathf.Min(minActionValue, actionValue);
                maxActionValue = Mathf.Max(maxActionValue, actionValue);

                successors.Add(new Successor()
                {
                    ActionKey = actionKey,
                    ActionInfo = actionInfo,
                });
            }

            successors.Sort();
        }

        public override IEnumerable<Node> GetNodeChildren(int maxDepth, int maxChildrenNodes)
        {
            m_Plan.GetActions(StateKey, s_Actions);

            // Handle current node
            if (maxDepth > 0 && depth > maxDepth)
            {
                if (depth == maxDepth + 1 && s_Actions.Count > 0)
                {
                    var expansionNode = new StateNode(m_Plan, StateKey,  true,
                        Mathf.Min(weight, float.Epsilon));
                    yield return expansionNode;
                }

                yield break;
            }

            var successors = new List<Successor>();
            SortSuccessorActions(s_Actions, ref successors, out var minActionValue, out var maxActionValue);

            // Yield children
            m_Plan.GetOptimalAction(StateKey, out var optimalActionKey);
            var c = 0;
            foreach (var successor in successors)
            {
                if (c >= maxChildrenNodes)
                    yield break;

                var actionKey = successor.ActionKey;
                var nodeWeight = actionKey.Equals(optimalActionKey) ? float.MaxValue
                    : Mathf.InverseLerp(minActionValue, maxActionValue, successor.ActionInfo.ActionValue);
                yield return new ActionNode(m_Plan, (StateKey, actionKey), weight: nodeWeight);

                c++;
            }
        }
    }

    class PlanVisualizer : Graph, IPlanVisualizer
    {
        public int MaxDepth { get; set; }
        public int MaxChildrenNodes { get; set; }
        public IVisualizerNode RootNodeOverride { get; set; }

        IPlanInternal m_Plan;

        public PlanVisualizer(IPlanInternal plan)
        {
            m_Plan = plan;
        }

        protected override IEnumerable<Node> GetChildren(Node node)
        {
            foreach (var childNode in ((BaseNode)node).GetNodeChildren(MaxDepth, MaxChildrenNodes))
                yield return childNode;
        }

        protected override void Populate()
        {
            if (RootNodeOverride == null)
                PopulateWithRoot();
            else
                PopulateWithRootOverride();
        }

        void PopulateWithRoot()
        {
            var rootStateKey = m_Plan.RootStateKey;

            // If action is in progress or optimal action is assigned, use action node as root.
            var actionKey = m_Plan.CurrentAction;
            if (actionKey == null)
                m_Plan.GetOptimalAction(rootStateKey, out actionKey);

            if (actionKey != null)
            {
                var visualizerActionNode = CreateActionNode((rootStateKey, actionKey),1f, true);
                AddNodeHierarchy(visualizerActionNode);
                return;
            }

            // If the root has not yet been expanded, root the graph at the policy graph node.
            var rootPolicyNode = CreateStateNode(rootStateKey, 1f, true);
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
            return new ActionNode(m_Plan, stateActionKey, expansion, weight, active);
        }

        BaseNode CreateStateNode(IStateKey stateKey, float weight, bool active = false, bool expansion = false)
        {
            return new StateNode(m_Plan, stateKey, expansion, weight, active);
        }
    }
}
