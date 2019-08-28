using System;
using System.Collections.Generic;
using System.Reflection;
using GraphVisualizer;
using Unity.AI.Planner;
using Unity.AI.Planner.Agent;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Jobs;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Visualizer
{
    class PlanVisualizerWindow : EditorWindow, IHasCustomMenu
    {
        List<Type> m_AgentTypes;

        IGraphRenderer m_Renderer;
        IGraphLayout m_Layout;

        [NonSerialized]
        IPlanInternal m_Plan;
        IPlanVisualizer m_Visualizer;
        IVisualizerNode m_RootNodeOverride;

        GraphSettings m_GraphSettings;
        int m_MaxDepth = k_DefaultMaxDepth;
        int m_MaxChildrenNodes = k_DefaultMaxChildrenNodes;

        const float k_DefaultMaximumNormalizedNodeSize = 0.8f;
        const float k_DefaultMaximumNodeSizeInPixels = 100.0f;
        const float k_DefaultAspectRatio = 1.5f;
        const int k_DefaultMaxDepth = 2;
        const int k_DefaultMaxChildrenNodes = 3;

        PlanVisualizerWindow()
        {
            m_GraphSettings.maximumNormalizedNodeSize = k_DefaultMaximumNormalizedNodeSize;
            m_GraphSettings.maximumNodeSizeInPixels = k_DefaultMaximumNodeSizeInPixels;
            m_GraphSettings.aspectRatio = k_DefaultAspectRatio;
            m_GraphSettings.showLegend = false;
            m_GraphSettings.showInspector = false;

            m_AgentTypes = new List<Type>();
            ReflectionUtils.ForEachType(t =>
            {
                var baseType = t.BaseType;
                if (!t.IsAbstract && baseType != null && baseType.IsGenericType
                    && baseType.GetGenericTypeDefinition() == typeof(BaseAgent<,,,,,,,,,>))
                {
                    m_AgentTypes.Add(t);
                }
            });
        }

        [MenuItem("Window/AI/Plan Visualizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlanVisualizerWindow>("Plan Visualizer");
            if (window.position.x > Screen.width)
            {
                var position = window.position;
                position.x -= Screen.width - position.width;
                window.position = position;
            }
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        void OnSelectionChange()
        {
            SelectPlan();
        }

        void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            SelectPlan();
        }

        GameObject FindBaseAgentObject()
        {
            foreach (var agentType in m_AgentTypes)
            {
                var agent = (MonoBehaviour)FindObjectOfType(agentType);
                return agent != null ? agent.gameObject : null;
            }

            return null;
        }


        void SelectPlan()
        {
            var activeGameObject = Selection.activeGameObject;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!activeGameObject)
                    activeGameObject = FindBaseAgentObject();

                if (activeGameObject)
                {
                    if (!GetPlan(activeGameObject, out var plan))
                    {
                        activeGameObject = FindBaseAgentObject();
                        GetPlan(activeGameObject, out plan);
                    }

                    if (plan != null)
                    {
                        m_Plan = plan;
                        InitializePlanVisualizer(m_Plan);
                    }
                }
            }
            else
            {
                m_Visualizer = null;
                m_Plan = null;
            }
        }

        void InitializePlanVisualizer(IPlanInternal plan)
        {
            m_Visualizer = new PlanVisualizer(plan);
        }

        static bool GetPlan(GameObject go, out IPlanInternal plan)
        {
            plan = null;

            if (go == null)
                return false;

            var components = go.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                if (!component.enabled)
                    continue;

                var componentType = component.GetType();
                var baseType = componentType.BaseType; // BaseAgent
                if (baseType != null)
                {
                    var fields = baseType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        if (plan == null && typeof(IPlanInternal).IsAssignableFrom(field.FieldType))
                        {
                            plan = (IPlanInternal)field.GetValue(component);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static bool IsAssignableToGenericType(Type givenType, Type genericType)
        {
            var interfaceTypes = givenType.GetInterfaces();

            foreach (var it in interfaceTypes)
            {
                if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
                    return true;
            }

            if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
                return true;

            var baseType = givenType.BaseType;
            if (baseType == null) return false;

            return IsAssignableToGenericType(baseType, genericType);
        }

        void ShowMessage(string msg)
        {
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.Label(msg);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        void Update()
        {
            if (EditorApplication.isPlaying && m_Plan == null)
                SelectPlan();

            // If in Play mode, refresh the plan each update.
            if (EditorApplication.isPlaying)
                Repaint();
        }

        void OnInspectorUpdate()
        {
            // If not in Play mode, refresh the plan less frequently.
            if (!EditorApplication.isPlaying)
                Repaint();
        }

        void OnGUI()
        {
            GraphOnGUI();
            DrawGraph();
        }

        void DrawGraph()
        {
            if (m_Visualizer == null)
                return;

            if (m_Layout == null)
                m_Layout = new ReingoldTilford();

            m_Visualizer.MaxDepth = m_MaxDepth;
            m_Visualizer.MaxChildrenNodes = m_MaxChildrenNodes;
            m_Visualizer.RootNodeOverride = m_RootNodeOverride;
            m_Visualizer.Refresh();
            m_Layout.CalculateLayout((Graph)m_Visualizer);

            if (m_Renderer == null)
            {
                m_Renderer = new PlanGraphRenderer((renderer, vn) =>
                {
                    if (vn != null && vn.ExpansionNode)
                    {
                        if (vn.parent != null)
                        {
                            // We're looking to go into the children expansion of a node, so select the actual node;
                            // The one that was clicked on was placeholder for all of the children
                            m_RootNodeOverride = (IVisualizerNode)vn.parent;
                        }
                        else
                        {
                            // Navigate back up the hierarchy
                            m_RootNodeOverride = (IVisualizerNode)m_RootNodeOverride.parent;

                            // If there isn't another parent, then we're actually back at the root
                            if (m_RootNodeOverride != null && m_RootNodeOverride.parent == null)
                                m_RootNodeOverride = null;
                        }

                        renderer.Reset();
                    }

                    m_GraphSettings.showInspector = vn != null;
                });
            }

            var toolbarHeight = EditorStyles.toolbar.fixedHeight;
            var graphRect = new Rect(0, toolbarHeight, position.width, position.height - toolbarHeight);

            m_Renderer.Draw(m_Layout, graphRect, m_GraphSettings);
        }

        void GraphOnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Max Depth:");
            m_MaxDepth = EditorGUILayout.IntSlider(m_MaxDepth, 1, 4);
            EditorGUILayout.Space();
            GUILayout.Label("Show Top Actions:");
            m_MaxChildrenNodes = EditorGUILayout.IntSlider(m_MaxChildrenNodes, 1, 16);
            GUILayout.FlexibleSpace();
            if (m_Plan != null)
            {
                EditorGUILayout.Space();
                GUILayout.Label($"Max Plan Depth: {m_Plan.MaxHorizonFromRoot}");
            }

            EditorGUILayout.EndHorizontal();

            if (m_Visualizer == null && m_Plan == null)
            {
                // Early out if there are no graphs.
                if (!EditorApplication.isPlaying)
                {
                    ShowMessage("You must be in play mode to visualize a plan");
                    return;
                }

                ShowMessage("Select an agent that has an AI controller");
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Legend"), m_GraphSettings.showLegend, ToggleLegend);
            menu.AddItem(new GUIContent("Inspector"), m_GraphSettings.showInspector, ToggleInspector);
        }

        void ToggleLegend()
        {
            m_GraphSettings.showLegend = !m_GraphSettings.showLegend;
        }

        void ToggleInspector()
        {
            m_GraphSettings.showInspector = !m_GraphSettings.showInspector;
        }
    }
}
