using System;
using System.Collections.Generic;
using GraphVisualizer;
using Unity.AI.Planner;
using UnityEditor.AI.Planner.Editors;
using UnityEngine;

namespace UnityEditor.AI.Planner.Visualizer
{
    class ActionNode : BaseNode
    {
        static bool s_ParameterGUIFoldout = true;

        public (IStateKey, IActionKey) StateActionKey => ((IStateKey, IActionKey))content;

        struct Successor : IComparable<Successor>
        {
            public StateTransitionInfo StateTransitionInfo;
            public IStateKey StateKey;

            public int CompareTo(Successor other) => other.StateTransitionInfo.Probability.CompareTo(StateTransitionInfo.Probability);
        }

        // Local cache
        List<IStateKey> m_StateTransitions = new List<IStateKey>();
        List<Successor> m_Successors = new List<Successor>();

        public ActionNode(IPlanExecutor planExecutor, (IStateKey, IActionKey) stateActionKey, bool expansion = false, float weight = 1, bool active = false)
            : base(planExecutor, planExecutor.GetActionName(stateActionKey.Item2), stateActionKey, expansion, weight, active)
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

            var (stateKey, actionKey) = StateActionKey;
            var stateTransitionCount = m_Plan.GetResultingStates(stateKey, actionKey, null);
            return $"{stateTransitionCount} State(s)";
        }

        public override void DrawInspector(IPlanVisualizer visualizer)
        {
            var (stateKey, actionKey) = StateActionKey;
            if (m_Plan.TryGetActionInfo(stateKey, actionKey, out var actionInfo))
            {
                base.DrawInspector(visualizer);

                EditorGUILayout.LabelField("Action Value", $"{actionInfo.ActionValue}", EditorStyles.whiteLabel);
                EditorGUILayout.LabelField("Complete", $"{actionInfo.SubgraphComplete}", EditorStyles.whiteLabel);

                SortSuccessorStates(stateKey, actionKey, m_StateTransitions, ref m_Successors, out _, out _);

                if (m_Successors.Count > 0)
                {
                    var successor = m_Successors[0];

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Resulting state", $"{successor.StateKey.Label}", EditorStyles.whiteLabel);
                    EditorGUILayout.LabelField("Probability", $"{successor.StateTransitionInfo.Probability}", EditorStyles.whiteLabel);
                    EditorGUILayout.LabelField("Transition Utility Value", $"{successor.StateTransitionInfo.TransitionUtilityValue}", EditorStyles.whiteLabel);

                    s_ParameterGUIFoldout = EditorStyleHelper.DrawSubHeader(new GUIContent("Parameter information"), s_ParameterGUIFoldout);

                    if (s_ParameterGUIFoldout)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            var parameters = m_PlanExecutor.GetActionParametersInfo(stateKey, actionKey);
                            foreach (var param in parameters)
                            {
                                EditorGUILayout.LabelField(new GUIContent(param.ParameterName), new GUIContent(param.TraitObjectName, $"Object : {param.TraitObjectId}"), EditorStyles.whiteLabel);
                            }
                        }
                    }
                }
            }
        }

        void SortSuccessorStates(IStateKey stateKey, IActionKey actionKey, List<IStateKey> stateTransitions, ref List<Successor> successors, out float minProbability, out float maxProbability)
        {
            // Grab all child nodes and sort them
            minProbability = float.MaxValue;
            maxProbability = float.MinValue;
            successors.Clear();
            for (var i = 0; i < stateTransitions.Count; i++)
            {
                var resultingState = stateTransitions[i];
                if (m_Plan.TryGetStateTransitionInfo(stateKey, actionKey, resultingState, out var stateTransitionInfo))
                {
                    var resultProbability = stateTransitionInfo.Probability;
                    minProbability = Mathf.Min(minProbability, resultProbability);
                    maxProbability = Mathf.Max(maxProbability, resultProbability);

                    successors.Add(new Successor()
                    {
                        StateTransitionInfo = stateTransitionInfo,
                        StateKey = resultingState
                    });
                }
            }

            successors.Sort();
        }

        public override IEnumerable<Node> GetNodeChildren(int maxDepth, int maxChildrenNodes, IList<object> expandedNodes)
        {
            var (stateKey, actionKey) = StateActionKey;
            m_Plan.GetResultingStates(stateKey, actionKey, m_StateTransitions);

            // Handle current node
            if (maxDepth > 0 && depth > maxDepth)
            {
                if (depth == maxDepth + 1 && m_StateTransitions.Count > 0)
                {
                    var expansionNode = new ActionNode(m_PlanExecutor, StateActionKey, true,
                        Mathf.Min(weight, float.Epsilon));
                    yield return expansionNode;
                }

                yield break;
            }

            var successors = new List<Successor>();
            SortSuccessorStates(stateKey, actionKey, m_StateTransitions, ref successors, out var minProbability, out var maxProbability);

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

                yield return new StateNode(m_PlanExecutor, successor.StateKey, weight: nodeWeight);

                c++;
            }
        }
    }
}
