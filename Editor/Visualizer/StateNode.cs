using System;
using System.Collections.Generic;
using GraphVisualizer;
using Unity.AI.Planner;
using UnityEditor.AI.Planner.Editors;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;

namespace UnityEditor.AI.Planner.Visualizer
{
    class StateNode : BaseNode
    {
        static bool s_ActionsGUIFoldout = true;
        static bool s_StateGUIFoldout = true;

        public IStateKey StateKey => (IStateKey)content;
        bool m_RootState;

        struct Successor : IComparable<Successor>
        {
            public IActionKey ActionKey;
            public ActionInfo ActionInfo;

            public int CompareTo(Successor other) => other.ActionInfo.ActionValue.Average.CompareTo(ActionInfo.ActionValue.Average);
        }

        // Local cache
        static List<IActionKey> s_Actions = new List<IActionKey>();
        static List<Successor> s_Successors = new List<Successor>();

        public StateNode(IPlanExecutor planExecutor, IStateKey stateKey, bool expansion = false, float weight = 1, bool active = false, bool rootState = false)
            : base(planExecutor, stateKey.Label, stateKey, expansion, weight, active)
        {
            m_RootState = rootState;
        }

        public override Type GetContentType()
        {
            if (m_Plan.TryGetStateInfo(StateKey, out var stateInfo))
                return stateInfo.GetType();

            return base.GetContentType();
        }

        protected override string GetExpansionString()
        {
            if (weight < 0f)
                return base.GetExpansionString(); // Back to parent node

            var actionsCount = m_Plan.GetActions(StateKey, null);
            return $"{actionsCount} Action(s)";
        }

        public override Color GetColor()
        {
            if (m_RootState && !active)
                return Color.gray;

            return base.GetColor();
        }

        public override void DrawInspector(IPlanVisualizer visualizer)
        {
            if (m_Plan.TryGetStateInfo(StateKey, out var stateInfo))
            {
                base.DrawInspector(visualizer);

                EditorGUILayout.LabelField("Policy Value", $"{stateInfo.PolicyValue}", EditorStyles.whiteLabel);
                EditorGUILayout.LabelField("Complete", $"{stateInfo.SubgraphComplete}", EditorStyles.whiteLabel);

                if (m_Plan.TryGetOptimalAction(StateKey, out var optimalActionKey))
                {
                    EditorGUILayout.LabelField("Optimal Action", m_PlanExecutor.GetActionName(optimalActionKey), EditorStyles.whiteLabel);
                }

                if (!m_RootState || active)
                {
                     s_ActionsGUIFoldout = EditorStyleHelper.DrawSubHeader(new GUIContent("Successor Actions"), s_ActionsGUIFoldout);
                    if (s_ActionsGUIFoldout)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            m_Plan.GetActions(StateKey, s_Actions);
                            SortSuccessorActions(s_Actions, ref s_Successors, out _, out _);

                            for (var i = 0; i < s_Successors.Count; i++)
                            {
                                var successor = s_Successors[i];
                                var actionKey = successor.ActionKey;
                                if (m_Plan.TryGetActionInfo(StateKey, actionKey, out var actionInfo))
                                {
                                    var rect = EditorGUILayout.BeginHorizontal();

                                    var stateActionKey = (StateKey, actionKey);
                                    bool toggle = i < visualizer.MaxChildrenNodes || visualizer.ForcedExpandedNodes.Contains(stateActionKey);

                                    if (i < visualizer.MaxChildrenNodes)
                                        GUI.enabled = false;

                                    EditorGUI.BeginChangeCheck();
                                    toggle = GUI.Toggle(rect, toggle, string.Empty);

                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        if (toggle)
                                            visualizer.ForcedExpandedNodes.Add(stateActionKey);
                                        else
                                            visualizer.ForcedExpandedNodes.Remove(stateActionKey);
                                    }
                                    GUI.enabled = true;

                                    EditorGUILayout.LabelField( new GUIContent(m_PlanExecutor.GetActionName(actionKey)), new GUIContent($"{actionInfo.ActionValue.Average:0.000}", $"{actionInfo.ActionValue}"), EditorStyles.whiteLabel);
                                    EditorGUILayout.EndHorizontal();

                                    using (new EditorGUI.IndentLevelScope())
                                    {
                                        var parameters = m_PlanExecutor.GetActionParametersInfo(StateKey, actionKey);
                                        foreach (var param in parameters)
                                        {
                                            EditorGUILayout.LabelField(new GUIContent(param.ParameterName), new GUIContent(param.TraitObjectName, $"Object: {param.TraitObjectId}"), EditorStyles.whiteMiniLabel);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

            }

            var stateString = m_Plan.GetStateData(StateKey)?.ToString();
            if (string.IsNullOrEmpty(stateString)) // State may no longer exist but be selected as an override.
                return;

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

        void SortSuccessorActions(List<IActionKey> actions, ref List<Successor> successors, out float minActionValue, out float maxActionValue)
        {
            // Grab all child nodes and sort them
            minActionValue = float.MaxValue;
            maxActionValue = float.MinValue;
            successors.Clear();
            for (var i = 0; i < actions.Count; i++)
            {
                var actionKey = actions[i];
                if (m_Plan.TryGetActionInfo(StateKey, actionKey, out var actionInfo))
                {
                    var actionValue = actionInfo.ActionValue;
                    minActionValue = Mathf.Min(minActionValue, actionValue.Average);
                    maxActionValue = Mathf.Max(maxActionValue, actionValue.Average);

                    successors.Add(new Successor()
                    {
                        ActionKey = actionKey,
                        ActionInfo = actionInfo,
                    });
                }
            }

            successors.Sort();
        }

        public override IEnumerable<Node> GetNodeChildren(int maxDepth, int maxChildrenNodes, IList<object> expandedNodes)
        {
            m_Plan.GetActions(StateKey, s_Actions);

            // Handle current node
            if (maxDepth > 0 && depth > maxDepth)
            {
                if (depth == maxDepth + 1 && s_Actions.Count > 0)
                {
                    var expansionNode = new StateNode(m_PlanExecutor, StateKey,  true,
                        Mathf.Min(weight, float.Epsilon));
                    yield return expansionNode;
                }

                yield break;
            }

            var successors = new List<Successor>();
            SortSuccessorActions(s_Actions, ref successors, out var minActionValue, out var maxActionValue);

            // Yield children
            if (m_Plan.TryGetOptimalAction(StateKey, out var optimalActionKey))
            {
                var c = 0;
                foreach (var successor in successors)
                {
                    var actionKey = successor.ActionKey;
                    var stateActionKey = (StateKey, actionKey);

                    if (!expandedNodes.Contains(stateActionKey) && c >= maxChildrenNodes)
                    {
                        continue;
                    }

                    var nodeWeight = actionKey.Equals(optimalActionKey)
                        ? float.MaxValue
                        : Mathf.InverseLerp(minActionValue, maxActionValue, successor.ActionInfo.ActionValue.Average);

                    yield return new ActionNode(m_PlanExecutor, stateActionKey, weight: nodeWeight);

                    c++;
                }
            }
        }
    }
}
