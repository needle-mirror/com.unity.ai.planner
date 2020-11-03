using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Planner.Controller;
using Unity.AI.Planner.Traits;
using Unity.Semantic.Traits;
using Unity.Semantic.Traits.Queries;
using Unity.Entities;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using ITrait = Unity.AI.Planner.Traits.ITrait;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(DecisionController), true)]
    class DecisionControllerInspector : Editor
    {
        static string s_LastPathUsedForNewPlan;

        void OnEnable()
        {
            PlannerAssetDatabase.Refresh();
        }

        void AssignPlanGUI(SerializedProperty definition)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Create and assign a Problem Definition to start working with the AI Planner", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+", EditorStyles.miniButtonMid, GUILayout.Width(22)))
            {
                var newPlan = PlannerAssetDatabase.CreateNewPlannerAsset<ProblemDefinition>("Plan");
                definition.objectReferenceValue = newPlan;
            }
            EditorGUILayout.PropertyField(definition, GUIContent.none);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var definition = serializedObject.FindProperty("m_ProblemDefinition");
            if (!definition.objectReferenceValue)
            {
                AssignPlanGUI(definition);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            definition.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.settings, definition.isExpanded, AIPlannerPreferences.displayControllerAdvancedSettings, value => AIPlannerPreferences.displayControllerAdvancedSettings = value);
            if (definition.isExpanded)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(definition);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.FindProperty("m_ActionExecuteInfos").ClearArray();
                }

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_InitializeOnStart"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AutoUpdate"));
                serializedObject.ApplyModifiedProperties();

                if (AIPlannerPreferences.displayControllerAdvancedSettings)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PlannerSettings"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ExecutionSettings"));
                }

                EditorGUILayout.Space();
            }

            if (definition.objectReferenceValue != null)
            {
                var actionMappingArray = serializedObject.FindProperty("m_ActionExecuteInfos");
                actionMappingArray.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.actionExecution, actionMappingArray.isExpanded);
                if (actionMappingArray.isExpanded)
                {
                    var plan = definition.objectReferenceValue as ProblemDefinition;
                    if (plan != null && plan.ActionDefinitions != null)
                    {
                        // Remove actionInfo for actions not present in the plan
                        for (int i = actionMappingArray.arraySize - 1; i >= 0; i--)
                        {
                            var actionInfoProperty = actionMappingArray.GetArrayElementAtIndex(i);
                            var actionName = actionInfoProperty.FindPropertyRelative("m_ActionName").stringValue;

                            if (plan.ActionDefinitions.FirstOrDefault(a => a.Name == actionName) == default)
                                actionMappingArray.DeleteArrayElementAtIndex(i);
                        }

                        foreach (var actionDefinition in plan.ActionDefinitions)
                        {
                            if (actionDefinition == null)
                                continue;

                            EditorGUILayout.BeginVertical("Box");
                            EditorGUILayout.LabelField(actionDefinition.Name, EditorStyleHelper.namedObjectLabel);
                            var actionMappingProperty = actionMappingArray.FindPropertyInArray(property => property.FindPropertyRelative("m_ActionName").stringValue == actionDefinition.Name);

                            if (actionMappingProperty == null)
                            {
                                actionMappingProperty = actionMappingArray.InsertArrayElement();
                                actionMappingProperty.FindPropertyRelative("m_ActionName").stringValue = actionDefinition.Name;
                                actionMappingProperty.FindPropertyRelative("m_Source").objectReferenceValue = null;
                            }

                            var sourceGameObjectProperty = actionMappingProperty.FindPropertyRelative("m_SourceGameObject");
                            if (sourceGameObjectProperty.objectReferenceValue == null)
                            {
                                using (new EditorGUI.IndentLevelScope())
                                {
                                    EditorGUILayout.Space();
                                    EditorGUILayout.HelpBox("Drag a GameObject to the field below (or use the selection icon) to match a planner action to a callback that will cause an effect in the world", MessageType.Info);
                                    sourceGameObjectProperty.objectReferenceValue = EditorGUILayout.ObjectField(GUIContent.none, sourceGameObjectProperty.objectReferenceValue, typeof(GameObject), true);
                                    EditorGUILayout.Space();
                                }
                            }
                            else
                            {
                                EditorGUI.BeginChangeCheck();
                                sourceGameObjectProperty.objectReferenceValue = EditorGUILayout.ObjectField(EditorStyleHelper.onActionStart, sourceGameObjectProperty.objectReferenceValue, typeof(GameObject), true);

                                if (EditorGUI.EndChangeCheck())
                                {
                                    if (sourceGameObjectProperty.objectReferenceValue == null)
                                    {
                                        // Reset serialized data, when source object reference is removed
                                        actionMappingProperty.FindPropertyRelative("m_Source").objectReferenceValue = null;
                                        actionMappingProperty.FindPropertyRelative("m_Method").stringValue = String.Empty;
                                        actionMappingProperty.FindPropertyRelative("m_Arguments").ClearArray();
                                    }
                                }

                                var sourceGameObject = sourceGameObjectProperty.objectReferenceValue as GameObject;
                                if (sourceGameObject)
                                {
                                    var components = sourceGameObject.GetComponents<MonoBehaviour>();
                                    var sourceProperty = actionMappingProperty.FindPropertyRelative("m_Source");

                                    using (new EditorGUI.IndentLevelScope())
                                    {
                                        var component = sourceProperty.objectReferenceValue as Component;

                                        MethodInfo selectedMethod = null;
                                        var methods = components.Where(c => c != null)
                                            .SelectMany(c => c.GetType()
                                            .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                                            .Where(m => !m.IsSpecialName)
                                            .Select(m => ((Component)c, m))).Prepend((default, default)).ToArray();;
                                        var methodsName = methods.Select(m => m.Item1 == default?"-":$"{m.Item1.GetType().Name}/{m.Item2.Name}");

                                        var methodProperty = actionMappingProperty.FindPropertyRelative("m_Method");
                                        var arguments = actionMappingProperty.FindPropertyRelative("m_Arguments");
                                        var methodName = methodProperty.stringValue;

                                        int selectIndex = Array.FindIndex(methods, m => m.Item1 == component && m.Item2?.Name == methodName);
                                        EditorGUI.BeginChangeCheck();
                                        selectIndex = EditorGUILayout.Popup("Method", Math.Max(0, selectIndex), methodsName.ToArray());

                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            var method = methods[selectIndex];
                                            sourceProperty.objectReferenceValue = method.Item1;
                                            methodProperty.stringValue = method.Item2?.Name;
                                            arguments.ClearArray();
                                        }

                                        if (selectIndex >= 0)
                                            selectedMethod = methods[selectIndex].Item2;

                                        if (selectedMethod != null)
                                        {
                                            using (new EditorGUI.IndentLevelScope())
                                            {
                                                const int operatorSize = 50;

                                                var parameters = selectedMethod.GetParameters();
                                                if (arguments.arraySize != parameters.Length)
                                                {
                                                    arguments.arraySize = parameters.Length;
                                                }

                                                for (var index = 0; index < parameters.Length; index++)
                                                {
                                                    var argumentInfos = arguments.GetArrayElementAtIndex(index);

                                                    var parameterInfo = parameters[index];
                                                    GUILayout.BeginHorizontal();
                                                    EditorGUILayout.LabelField($"{parameterInfo.Name} ({parameterInfo.ParameterType.Name})", EditorStyles.miniLabel);

                                                    Rect r = GUILayoutUtility.GetRect(operatorSize, operatorSize * 2,
                                                        EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight);
                                                    r.y += 2;
                                                    DrawOperandSelectorField(r, argumentInfos, null, actionDefinition.Parameters.ToList(), parameterInfo.ParameterType);

                                                    GUILayout.EndHorizontal();
                                                }
                                            }
                                        }
                                    }

                                    EditorGUILayout.Space();

                                    var actionCompleteProperty = actionMappingProperty.FindPropertyRelative("m_PlanExecutorStateUpdateMode");
                                    EditorGUILayout.PropertyField(actionCompleteProperty, EditorStyleHelper.stateUpdate);
                                }
                            }

                            EditorGUILayout.EndVertical();

                            EditorGUILayout.Space();
                        }
                    }
                }
            }

            var queryProperty = serializedObject.FindProperty("m_WorldObjectQuery");

            queryProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.includeObjects, queryProperty.isExpanded);
            if (queryProperty.isExpanded)
            {
                if (!queryProperty.objectReferenceValue)
                    EditorGUILayout.HelpBox("Assign a semantic query to populate the Planner state", MessageType.Warning);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(queryProperty, new GUIContent("World Query"));

                if (!queryProperty.objectReferenceValue)
                {
                    var source = ((DecisionController)target).gameObject;
                    if (source.GetComponent<SemanticQuery>() != null)
                    {
                        if (GUILayout.Button("Assign", EditorStyles.miniButtonMid, GUILayout.Width(45)))
                        {
                            queryProperty.objectReferenceValue = source.GetComponent<SemanticQuery>();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Add", EditorStyles.miniButtonMid, GUILayout.Width(35)))
                        {
                            var newQuery = source.AddComponent<SemanticQuery>();
                            queryProperty.objectReferenceValue = newQuery;
                        }
                    }

                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawOperandSelectorField(Rect rect, SerializedProperty operand, SerializedProperty @operator, List<ParameterDefinition> parameters, Type expectedType)
        {
            var content = TraitGUIUtility.GetOperandDisplayContent(operand, @operator, parameters, rect.size.x, EditorStyleHelper.listPopupStyle);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var allowParameterSelection = expectedType != null &&
                    (typeof(TraitBasedObjectId).IsAssignableFrom(expectedType) || expectedType == typeof(GameObject) || expectedType == typeof(Entity));
                var allowTraitSelection = !allowParameterSelection && expectedType != null && typeof(ITrait).IsAssignableFrom(expectedType);

                var popup = new OperandSelectorPopup(operand, parameters, allowParameterSelection, allowTraitSelection, null, expectedType);
                PopupWindow.Show(rect, popup);
            }
        }
    }
}
