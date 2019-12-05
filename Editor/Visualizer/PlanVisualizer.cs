using System;
using System.Collections.Generic;
using GraphVisualizer;
using Unity.AI.Planner;
using UnityEditor.AI.Planner.Editors;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;

namespace UnityEditor.AI.Planner.Visualizer
{
    abstract class BaseNode : Node, IVisualizerNode
    {
        public bool ExpansionNode { get; }
        public string Label { get; }

        protected IPlanExecutionInfo m_PlanExecutionInfo;

        protected BaseNode(IPlanExecutionInfo planExecutionInfo, string label, object content, bool expansion = false,  float weight = 1, bool active = false)
            : base(content, weight, active)
        {
            Label = label;
            ExpansionNode = expansion;
            m_PlanExecutionInfo = planExecutionInfo;
        }

        public abstract IEnumerable<Node> GetNodeChildren(int maxDepth, int maxChildrenNodes);

        protected virtual string GetExpansionString()
        {
            return "\u2026";
        }

        public override Color GetColor()
        {
            return ExpansionNode ? Color.gray : base.GetColor();
        }

        public override string GetContentTypeName()
        {
            if (ExpansionNode)
                return GetExpansionString();

            return Label ?? base.GetContentTypeName();
        }

        public virtual void DrawInspector()
        {
            GUILayout.Label(Label, EditorStyles.whiteLargeLabel);
            EditorGUILayout.Space();
        }
    }

    class ActionNode : BaseNode
    {
        public (IStateKey, IActionKey) StateActionKey => ((IStateKey, IActionKey))content;

        struct Successor : IComparable<Successor>
        {
            public StateTransitionInfo StateTransitionInfo;
            public IStateKey StateKey;

            public int CompareTo(Successor other) => other.StateTransitionInfo.Probability.CompareTo(StateTransitionInfo.Probability);
        }

        // Local cache
        List<(StateTransitionInfo, IStateKey)> m_StateTransitions = new List<(StateTransitionInfo, IStateKey)>();
        List<Successor> m_Successors = new List<Successor>();

        public ActionNode(IPlanExecutionInfo planExecutionInfo, (IStateKey, IActionKey) stateActionKey, bool expansion = false, float weight = 1, bool active = false)
            : base(planExecutionInfo, planExecutionInfo.GetActionName(stateActionKey.Item2), stateActionKey, expansion, weight, active)
        {
        }

        public override Color GetColor()
        {
            if (!ExpansionNode && !string.IsNullOrEmpty(Label))
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

            var stateTransitionCount = m_PlanExecutionInfo.GetActionResults(StateActionKey, null);
            return $"{stateTransitionCount} State(s)";
        }

        public override void DrawInspector()
        {
            base.DrawInspector();

            var actionInfo = m_PlanExecutionInfo.GetActionInfo(StateActionKey);

            EditorGUILayout.LabelField("Action Value", $"{actionInfo.ActionValue}", EditorStyles.whiteLabel);
            EditorGUILayout.LabelField("Complete", $"{actionInfo.SubgraphComplete}", EditorStyles.whiteLabel);

            SortSuccessorStates(m_StateTransitions, ref m_Successors, out _, out _);

            var successor = m_Successors[0];

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resulting state", $"{successor.StateKey.Label}", EditorStyles.whiteLabel);
            EditorGUILayout.LabelField("Probability", $"{successor.StateTransitionInfo.Probability}", EditorStyles.whiteLabel);
            EditorGUILayout.LabelField("Transition Utility Value", $"{successor.StateTransitionInfo.TransitionUtilityValue}", EditorStyles.whiteLabel);

            // TODO display Action arguments

        }

        static void SortSuccessorStates(List<(StateTransitionInfo, IStateKey)> stateTransitions, ref List<Successor> successors, out float minProbability, out float maxProbability)
        {
            // Grab all child nodes and sort them
            minProbability = float.MaxValue;
            maxProbability = float.MinValue;
            successors.Clear();
            for (var i = 0; i < stateTransitions.Count; i++)
            {
                var stateTransitionPair = stateTransitions[i];
                var stateTransitionInfo = stateTransitionPair.Item1;
                var resultingState = stateTransitionPair.Item2;

                var resultProbability = stateTransitionInfo.Probability;
                minProbability = Mathf.Min(minProbability, resultProbability);
                maxProbability = Mathf.Max(maxProbability, resultProbability);

                successors.Add(new Successor()
                {
                    StateTransitionInfo = stateTransitionInfo,
                    StateKey = resultingState
                });
            }

            successors.Sort();
        }

        public override IEnumerable<Node> GetNodeChildren(int maxDepth, int maxChildrenNodes)
        {
            m_PlanExecutionInfo.GetActionResults(StateActionKey, m_StateTransitions);

            // Handle current node
            if (maxDepth > 0 && depth > maxDepth)
            {
                if (depth == maxDepth + 1 && m_StateTransitions.Count > 0)
                {
                    var expansionNode = new ActionNode(m_PlanExecutionInfo, StateActionKey, true,
                        Mathf.Min(weight, float.Epsilon));
                    yield return expansionNode;
                }

                yield break;
            }

            var successors = new List<Successor>();
            SortSuccessorStates(m_StateTransitions, ref successors, out var minProbability, out var maxProbability);

            // Yield children
            var c = 0;
            foreach (var successor in successors)
            {
                if (c >= maxChildrenNodes)
                    yield break;

                var probability = successor.StateTransitionInfo.Probability;
                var nodeWeight = Math.Abs(probability - 1.0f) < 10e-6 ?
                    float.MaxValue :
                    Mathf.InverseLerp(minProbability, maxProbability, probability);

                yield return new StateNode(m_PlanExecutionInfo, successor.StateKey, weight: nodeWeight);

                c++;
            }
        }
    }

    class StateNode : BaseNode
    {
        static bool s_ActionsGUIFoldout = true;
        static bool s_StateGUIFoldout = true;

        public IStateKey StateKey => (IStateKey)content;

        struct Successor : IComparable<Successor>
        {
            public IActionKey ActionKey;
            public ActionInfo ActionInfo;

            public int CompareTo(Successor other) => other.ActionInfo.ActionValue.Average.CompareTo(ActionInfo.ActionValue.Average);
        }

        // Local cache
        static List<IActionKey> s_Actions = new List<IActionKey>();
        static List<Successor> s_Successors = new List<Successor>();

        public StateNode(IPlanExecutionInfo planExecutionInfo, IStateKey stateKey, bool expansion = false, float weight = 1, bool active = false)
            : base(planExecutionInfo, stateKey.Label, stateKey, expansion, weight, active)
        {
        }

        public override Type GetContentType()
        {
            var stateInfo = m_PlanExecutionInfo.GetStateInfo(StateKey);
            return stateInfo.GetType();
        }

        protected override string GetExpansionString()
        {
            if (weight < 0f)
                return base.GetExpansionString(); // Back to parent node

            var actionsCount = m_PlanExecutionInfo.GetActions(StateKey, null);
            return $"{actionsCount} Action(s)";
        }

        public override void DrawInspector()
        {
            base.DrawInspector();

            var stateInfo = m_PlanExecutionInfo.GetStateInfo(StateKey);

            EditorGUILayout.LabelField("Policy Value", $"{stateInfo.PolicyValue}", EditorStyles.whiteLabel);
            EditorGUILayout.LabelField("Complete", $"{stateInfo.SubgraphComplete}", EditorStyles.whiteLabel);

            if (m_PlanExecutionInfo.GetOptimalAction(StateKey, out var optimalActionKey))
            {
                EditorGUILayout.LabelField("Optimal Action", m_PlanExecutionInfo.GetActionName(optimalActionKey), EditorStyles.whiteLabel);
            }

            var stateManager = m_PlanExecutionInfo.StateManager;
            if (stateManager != null) // null when stateManager does not implement IStateManagerInternal
            {
                try
                {
                    var stateData = stateManager.GetStateData(StateKey, false);

                    s_ActionsGUIFoldout = EditorStyleHelper.DrawSubHeader(new GUIContent("Successor Actions"), s_ActionsGUIFoldout);
                    if (s_ActionsGUIFoldout)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            m_PlanExecutionInfo.GetActions(StateKey, s_Actions);
                            SortSuccessorActions(s_Actions, ref s_Successors, out _, out _);

                            foreach (var successor in s_Successors)
                            {
                                var actionKey = successor.ActionKey;
                                var stateActionKey = (StateKey, actionKey);

                                var actionInfo = m_PlanExecutionInfo.GetActionInfo(stateActionKey);

                                EditorGUILayout.LabelField($"{m_PlanExecutionInfo.GetActionName(actionKey)}", $"{actionInfo.ActionValue}", EditorStyles.whiteLabel);
                            }
                        }
                    }

                    var stateString = stateData.ToString();
                    EditorGUILayout.Space();
                    s_StateGUIFoldout = EditorStyleHelper.DrawSubHeader(new GUIContent("State information"), s_StateGUIFoldout);
                    if (s_StateGUIFoldout)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.LabelField(stateString, EditorStyleHelper.inspectorStyleLabel);
                        }
                    }
                    if (GUILayout.Button("Copy State", EditorStyles.miniButton))
                        stateString.CopyToClipboard();
                }
                catch (ArgumentException) {}
            }
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
                var actionInfo = m_PlanExecutionInfo.GetActionInfo((StateKey, actionKey));
                var actionValue = actionInfo.ActionValue;
                minActionValue = Mathf.Min(minActionValue, actionValue.Average);
                maxActionValue = Mathf.Max(maxActionValue, actionValue.Average);

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
            m_PlanExecutionInfo.GetActions(StateKey, s_Actions);

            // Handle current node
            if (maxDepth > 0 && depth > maxDepth)
            {
                if (depth == maxDepth + 1 && s_Actions.Count > 0)
                {
                    var expansionNode = new StateNode(m_PlanExecutionInfo, StateKey,  true,
                        Mathf.Min(weight, float.Epsilon));
                    yield return expansionNode;
                }

                yield break;
            }

            var successors = new List<Successor>();
            SortSuccessorActions(s_Actions, ref successors, out var minActionValue, out var maxActionValue);

            // Yield children
            m_PlanExecutionInfo.GetOptimalAction(StateKey, out var optimalActionKey);
            var c = 0;
            foreach (var successor in successors)
            {
                if (c >= maxChildrenNodes)
                    yield break;

                var actionKey = successor.ActionKey;
                var nodeWeight = actionKey.Equals(optimalActionKey) ? float.MaxValue
                    : Mathf.InverseLerp(minActionValue, maxActionValue, successor.ActionInfo.ActionValue.Average);

                yield return new ActionNode(m_PlanExecutionInfo, (StateKey, actionKey), weight: nodeWeight);

                c++;
            }
        }
    }

    class PlanVisualizer : Graph, IPlanVisualizer
    {
        public int MaxDepth { get; set; }
        public int MaxChildrenNodes { get; set; }
        public IVisualizerNode RootNodeOverride { get; set; }

        IPlanExecutionInfo m_PlanExecutionInfo;

        public PlanVisualizer(IPlanExecutionInfo planExecutionInfo)
        {
            m_PlanExecutionInfo = planExecutionInfo;
        }

        protected override IEnumerable<Node> GetChildren(Node node)
        {
            foreach (var childNode in ((BaseNode)node).GetNodeChildren(MaxDepth, MaxChildrenNodes))
                yield return childNode;
        }

        protected override void Populate()
        {
            if (!m_PlanExecutionInfo.PlanExists)
                return;

            if (RootNodeOverride == null)
                PopulateWithRoot();
            else
                PopulateWithRootOverride();
        }

        void PopulateWithRoot()
        {
            var rootStateKey = m_PlanExecutionInfo.RootStateKey;

            // If action is in progress use action node as root.
            var actionKey = m_PlanExecutionInfo.CurrentAction;
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
            return new ActionNode(m_PlanExecutionInfo, stateActionKey, expansion, weight, active);
        }

        BaseNode CreateStateNode(IStateKey stateKey, float weight, bool active = false, bool expansion = false)
        {
            return new StateNode(m_PlanExecutionInfo, stateKey, expansion, weight, active);
        }
    }
}
