using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.Controller;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(DecisionController), true)]
    class DecisionControllerInspector : BaseTraitObjectEditor
    {
        static string s_LastPathUsedForNewPlan;

        ReorderableList m_QueryFilterList;
        Dictionary<string, Type> m_QueryTypes;
        Vector2 m_PreviewScrollviewPosition;

        void OnEnable()
        {
            PlannerAssetDatabase.Refresh();
            CacheQueryTypes();
            InitializeReorderableLists();
        }

        void InitializeReorderableLists()
        {
            var queryProperty = serializedObject.FindProperty("m_WorldObjectQuery");

            m_QueryFilterList = new ReorderableList(serializedObject, queryProperty.FindPropertyRelative("m_Filters"),
                true, false, true, true)
            {
                drawElementCallback = DrawQueryFilterElement,
                elementHeight = EditorGUIUtility.singleLineHeight * 1 + EditorGUIUtility.standardVerticalSpacing * 3,
                headerHeight = 2,
                elementHeightCallback = SetQueryFilterHeight,
                onAddDropdownCallback = ShowQueryFilterMenu,
                drawElementBackgroundCallback = DrawElementBackground,
                drawNoneElementCallback = DrawNoElement,
                onReorderCallback = ReorderQueryList,
            };
        }

        void AssignPlanGUI(SerializedProperty definition)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Create and assign a Plan Definition to start working with the AI Planner", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+", EditorStyles.miniButtonMid, GUILayout.Width(22)))
            {
                var newPlan = PlannerAssetDatabase.CreateNewPlannerAsset<PlanDefinition>("Plan");
                definition.objectReferenceValue = newPlan;
            }
            EditorGUILayout.PropertyField(definition, GUIContent.none);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        public override void OnInspectorGUI()
        {
            const float previewObjectHeight = 250;

            base.OnInspectorGUI();

            serializedObject.Update();

            var definition = serializedObject.FindProperty("m_PlanDefinition");
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
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SearchSettings"));
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
                    var plan = definition.objectReferenceValue as PlanDefinition;
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
                                                    DrawOperandSelectorField(r, argumentInfos, actionDefinition.Parameters.ToList(), parameterInfo.ParameterType);

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
                EditorGUILayout.LabelField(EditorStyleHelper.worldQuery, EditorStyles.boldLabel);
                m_QueryFilterList.DoLayoutList();

                EditorGUILayout.LabelField(EditorStyleHelper.worldQueryPreview, EditorStyles.boldLabel);
                var objectQuery = SerializedPropertyExtensions.GetValue<TraitBasedObjectQuery>(queryProperty);

                var targetGameObject = (target as Component)?.gameObject;
                var validTraitComponents = FindObjectsOfType<TraitComponent>().ToList();

                for (int i = validTraitComponents.Count - 1; i >= 0; i--)
                {
                    // Make sure that the parent object is set on the trait component (used in FromGameObjectQuery)
                    if (!EditorApplication.isPlaying)
                        validTraitComponents[i].Initialize();

                    if (!objectQuery.IsValid(targetGameObject, validTraitComponents[i]))
                    {
                        validTraitComponents.RemoveAt(i);
                    }
                }

                if (validTraitComponents.Count > 0)
                {
                    bool showScrollView = validTraitComponents.Count > previewObjectHeight / 22;
                    if (showScrollView)
                        m_PreviewScrollviewPosition = EditorGUILayout.BeginScrollView(m_PreviewScrollviewPosition, false, false, GUILayout.Height(previewObjectHeight));

                    foreach (var objectData in validTraitComponents)
                    {
                        var providerSerializedObject = new SerializedObject(objectData);
                        DrawTraitObjectData(providerSerializedObject.FindProperty("m_ObjectData"), true);
                    }

                    if (showScrollView)
                        EditorGUILayout.EndScrollView();
                }
                else
                {
                    EditorGUILayout.BeginVertical("Box");
                    EditorGUILayout.LabelField("None", EditorStyleHelper.italicGrayLabel);
                    EditorGUILayout.EndVertical();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawOperandSelectorField(Rect rect, SerializedProperty operand, List<ParameterDefinition> parameters, Type expectedType)
        {
            var content = TraitGUIUtility.GetOperandDisplayContent(operand, parameters, rect.size.x, EditorStyleHelper.listPopupStyle);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var allowParameterSelection = expectedType != null && (typeof(TraitBasedObjectId).IsAssignableFrom(expectedType) || typeof(ITraitBasedObjectData).IsAssignableFrom(expectedType));
                var allowTraitSelection = !allowParameterSelection && expectedType != null && typeof(ITrait).IsAssignableFrom(expectedType);

                var popup = new OperandSelectorPopup(operand, parameters, allowParameterSelection, allowTraitSelection, null, expectedType);
                PopupWindow.Show(rect, popup);
            }
        }

        void DrawQueryFilterElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_QueryFilterList.serializedProperty;
            var property = list.GetArrayElementAtIndex(index);

            var typeProperty = property.FindPropertyRelative("m_TypeName");

            var type = m_QueryTypes[typeProperty.stringValue];
            if (type == null)
            {
                return;
            }

            var attribute = type.GetCustomAttribute<QueryFilterAttribute>();
            var typeDisplayName =  attribute?.Name ?? type.Name;

            rect.height = EditorGUIUtility.singleLineHeight;
            rect.y += attribute != null && attribute.ParameterType != ParameterTypes.None?EditorGUIUtility.standardVerticalSpacing:0;

            property.FindPropertyRelative("m_Name").stringValue = typeDisplayName;

            if (attribute != null)
            {
                switch (attribute.ParameterType)
                {
                    case ParameterTypes.Int:
                        EditorGUI.PropertyField(rect, property.FindPropertyRelative("m_ParameterInt"),
                            new GUIContent(typeDisplayName));
                        break;
                    case ParameterTypes.Float:
                        EditorGUI.PropertyField(rect, property.FindPropertyRelative("m_ParameterFloat"),
                            new GUIContent(typeDisplayName));
                        break;
                    case ParameterTypes.String:
                        EditorGUI.PropertyField(rect, property.FindPropertyRelative("m_ParameterString"),
                            new GUIContent(typeDisplayName));
                        break;
                    case ParameterTypes.TraitsProhibited:
                    case ParameterTypes.TraitsRequired:
                        EditorGUI.LabelField(rect, typeDisplayName);
                        var traits = property.FindPropertyRelative("m_ParameterTraits");
                        var requiredTraits = attribute.ParameterType == ParameterTypes.TraitsRequired;
                        rect.x += EditorGUIUtility.labelWidth;
                        rect.width -= EditorGUIUtility.labelWidth;

                        TraitSelectorDrawer.DrawSelector(traits, rect, typeDisplayName,
                            requiredTraits ? EditorStyleHelper.requiredTraitLabel : EditorStyleHelper.prohibitedTraitLabel,
                            requiredTraits ? EditorStyleHelper.requiredTraitAdd : EditorStyleHelper.prohibitedTraitAdd,
                            requiredTraits ? EditorStyleHelper.requiredTraitMore : EditorStyleHelper.prohibitedTraitMore);
                        break;
                    case ParameterTypes.GameObject:
                        EditorGUI.PropertyField(rect, property.FindPropertyRelative("m_GameObject"),
                            new GUIContent(typeDisplayName));
                        break;
                    case ParameterTypes.None:
                        EditorGUI.LabelField(rect, typeDisplayName, EditorStyles.miniLabel);
                        break;
                }
            }
        }

        void ShowQueryFilterMenu(Rect rect, ReorderableList list)
        {
            var menu = new GenericMenu();
            AddQueryFilterItem(list, menu, typeof(LogicalOrFilter));

            menu.AddSeparator(string.Empty);
            AddQueryFilterItem(list, menu);

            menu.ShowAsContext();
        }

        void AddQueryFilterItem(ReorderableList list, GenericMenu menu, Type ignoreType = null)
        {
            foreach (var queryType in m_QueryTypes.Values)
            {
                var queryAttribute = queryType.GetCustomAttribute<QueryFilterAttribute>();
                if (ignoreType != null && queryType == ignoreType)
                    continue;

                menu.AddItem(new GUIContent(queryAttribute?.Name ?? queryType.Name), false, () =>
                {
                    serializedObject.Update();
                    var newFieldProperty = list.serializedProperty.InsertArrayElement();
                    var typeProperty = newFieldProperty.FindPropertyRelative("m_TypeName");
                    typeProperty.stringValue = queryType.FullName;
                    serializedObject.ApplyModifiedProperties();
                });
            }
        }

        void DrawElementBackground(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 )
                return;

            var list = m_QueryFilterList.serializedProperty;
            var property = list.GetArrayElementAtIndex(index);
            var typeProperty = property.FindPropertyRelative("m_TypeName");

            var type = m_QueryTypes[typeProperty.stringValue];
            if (type == null)
            {
                return;
            }

            var attribute = type.GetCustomAttribute<QueryFilterAttribute>();
            if (Event.current.type == EventType.Repaint)
            {
                if (attribute.ParameterType == ParameterTypes.None)
                {
                    rect.x = rect.x + 1;
                    rect.width = rect.width - 3;
                    EditorStyleHelper.listElementDarkBackground.Draw(rect, false, isActive, isActive, isFocused);
                }
                else
                {
                    ReorderableList.DefaultBehaviours.DrawElementBackground(rect, index, isActive, isFocused, true);
                }
            }
        }

        static void DrawNoElement(Rect rect)
        {
            EditorGUI.LabelField(rect, "All world objects");
        }

        float SetQueryFilterHeight(int index)
        {
            var list = m_QueryFilterList.serializedProperty;
            var property = list.GetArrayElementAtIndex(index);
            var typeProperty = property.FindPropertyRelative("m_TypeName");

            if (m_QueryTypes.ContainsKey(typeProperty.stringValue))
            {
                var attribute = m_QueryTypes[typeProperty.stringValue].GetCustomAttribute<QueryFilterAttribute>();
                if (attribute.ParameterType == ParameterTypes.None)
                    return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight * 1 + EditorGUIUtility.standardVerticalSpacing * 3;
        }

        void ReorderQueryList(ReorderableList list)
        {
            var queryProperty = serializedObject.FindProperty("m_WorldObjectQuery");
            var objectQuery = SerializedPropertyExtensions.GetValue<TraitBasedObjectQuery>(queryProperty);

            foreach (var filter in objectQuery.Filters)
            {
                filter.ResetCache();
            }
        }

        void CacheQueryTypes()
        {
            m_QueryTypes = new Dictionary<string, Type>();
            var queryTypes =  TypeCache.GetTypesDerivedFrom(typeof(BaseQueryFilter)).Where(t => !t.IsGenericType);

            foreach (var t in queryTypes)
            {
                  m_QueryTypes.Add(t.FullName, t);
            }
        }
    }
}
