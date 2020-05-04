using System;
using System.Collections.Generic;
using GraphVisualizer;
using Unity.AI.Planner;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Visualizer
{
    abstract class BaseNode : Node, IVisualizerNode
    {
        public bool ExpansionNode { get; }
        public string Label { get; }

        protected IPlanExecutor m_PlanExecutor;
        protected IPlan m_Plan => m_PlanExecutor.Plan;

        protected BaseNode(IPlanExecutor planExecutor, string label, object content, bool expansion = false, float weight = 1, bool active = false)
            : base(content, weight, active)
        {
            m_PlanExecutor = planExecutor;
            Label = label;
            ExpansionNode = expansion;
        }

        public abstract IEnumerable<Node> GetNodeChildren(int maxDepth, int maxChildrenNodes, IList<object> expandedNodes);

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

        public virtual void DrawInspector(IPlanVisualizer visualizer)
        {
            GUILayout.Label(Label, EditorStyles.whiteLargeLabel);
            EditorGUILayout.Space();
        }
    }
}
