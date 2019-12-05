using System;
using GraphVisualizer;
using UnityEditor;
using UnityEditor.AI.Planner.Visualizer;
using UnityEngine;

class PlanGraphRenderer : IGraphRenderer
{
    internal class GraphOnlyRenderer : DefaultGraphRenderer
    {
        public Node SelectedNode { get; set; }

        public GraphOnlyRenderer(Action<GraphOnlyRenderer, IVisualizerNode> nodeCallback)
        {
            nodeClicked += node => nodeCallback(this, node as IVisualizerNode);
            nodeClicked += UpdateSelection;
        }

        void UpdateSelection(Node node)
        {
            SelectedNode = node;
        }

        public void ResetSelection()
        {
            Reset();
            SelectedNode = null;
        }
    }

    static readonly float s_BorderSize = 15;
    static readonly float s_InspectorFixedWidth = 100;

    static readonly float s_DefaultMaximumNormalizedNodeSize = 0.8f;
    static readonly float s_DefaultMaximumNodeSizeInPixels = 100.0f;
    static readonly float s_DefaultAspectRatio = 1.5f;
    static readonly Color s_InspectorBackground = new Color(0, 0, 0, 0.1f);

    GraphOnlyRenderer m_GraphOnlyRenderer;
    GraphSettings m_GraphOnlySettings;
    Vector2 m_ScrollPos;

    public PlanGraphRenderer(Action<GraphOnlyRenderer, IVisualizerNode> nodeCallback)
    {
        m_GraphOnlyRenderer = new GraphOnlyRenderer(nodeCallback);
        m_GraphOnlySettings = new GraphSettings { showInspector = false, showLegend = false };
    }

    void DrawInspector(GraphSettings graphSettings, Rect inspectorArea)
    {
        EditorGUI.DrawRect(inspectorArea, s_InspectorBackground);

        inspectorArea.x += s_BorderSize;
        inspectorArea.width -= s_BorderSize * 2;
        inspectorArea.y += s_BorderSize;
        inspectorArea.height -= s_BorderSize * 2;

        GUILayout.BeginArea(inspectorArea);
        GUILayout.BeginVertical();

        if (graphSettings.showInspector)
        {
            if (m_GraphOnlyRenderer.SelectedNode != null)
            {
                var node = m_GraphOnlyRenderer.SelectedNode;
                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPos))
                {
                    m_ScrollPos = scrollView.scrollPosition;

                    if (node is BaseNode baseNode && !baseNode.ExpansionNode)
                    {
                        baseNode.DrawInspector();
                    }
                }
            }
            else
            {
                GUILayout.Label("Click on a node\nto display its details.");
            }
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    public void Draw(IGraphLayout graphLayout, Rect drawingArea)
    {
        GraphSettings defaults;
        defaults.maximumNormalizedNodeSize = s_DefaultMaximumNormalizedNodeSize;
        defaults.maximumNodeSizeInPixels = s_DefaultMaximumNodeSizeInPixels;
        defaults.aspectRatio = s_DefaultAspectRatio;
        defaults.showInspector = false;
        defaults.showLegend = false;
        Draw(graphLayout, drawingArea, defaults);
    }

    public void Draw(IGraphLayout graphLayout, Rect totalDrawingArea, GraphSettings graphSettings)
    {
        var drawingArea = new Rect(totalDrawingArea);

        if (graphSettings.showInspector)
        {
            var inspectorArea = new Rect(totalDrawingArea)
            {
                width = Mathf.Max(s_InspectorFixedWidth, drawingArea.width * 0.25f) + s_BorderSize * 2
            };

            inspectorArea.x = drawingArea.xMax - inspectorArea.width;
            drawingArea.width -= inspectorArea.width;

            DrawInspector(graphSettings, inspectorArea);
        }

        m_GraphOnlySettings.maximumNormalizedNodeSize = graphSettings.maximumNormalizedNodeSize;
        m_GraphOnlySettings.maximumNodeSizeInPixels = graphSettings.maximumNodeSizeInPixels;
        m_GraphOnlySettings.aspectRatio = graphSettings.aspectRatio;
        m_GraphOnlyRenderer.Draw(graphLayout, drawingArea, m_GraphOnlySettings);
    }
}
