using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Agent;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEditor.AI.Planner.Utility;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(ActionDefinition))]
    class ActionDefinitionInspector : BaseConditionalInspector
    {
        List<Type> m_OperationalActionTypes = new List<Type>();
        List<Type> m_CustomEffectTypes = new List<Type>();
        List<Type> m_CustomRewardTypes = new List<Type>();
        List<Type> m_CustomPreconditionTypes = new List<Type>();

        NoHeaderReorderableList m_PreconditionList;
        NoHeaderReorderableList m_ObjectChangeList;
        NoHeaderReorderableList m_ObjectRemovedList;
        NoHeaderReorderableList m_ObjectCreatedList;
        NoHeaderReorderableList m_ParameterList;

        void OnEnable()
        {
            InitializeReorderableLists();

            DomainAssetDatabase.Refresh();

            // Cache types
            m_OperationalActionTypes.Clear();
            typeof(IOperationalAction).GetImplementationsOfInterface(m_OperationalActionTypes);

            m_CustomEffectTypes.Clear();
            typeof(ICustomActionEffect<>).GetImplementationsOfInterface(m_CustomEffectTypes);

            m_CustomRewardTypes.Clear();
            typeof(ICustomReward<>).GetImplementationsOfInterface(m_CustomRewardTypes);

            m_CustomPreconditionTypes.Clear();
            typeof(ICustomPrecondition<>).GetImplementationsOfInterface(m_CustomPreconditionTypes);
        }

        void InitializeReorderableLists()
        {
            m_PreconditionList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Preconditions"), DrawPreconditionListElement, 1);
            m_ObjectChangeList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Effects"), DrawEffectListElement, 1);
            m_ObjectRemovedList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_RemovedObjects"), DrawRemovedObjectListElement, 1);
            m_ObjectCreatedList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_CreatedObjects"), DrawCreatedObjectListElement, 1);
            m_ObjectCreatedList.onAddCallback += InitializeCreatedObjectList;
            m_ParameterList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Parameters"), DrawParameterList, 3);
            m_ParameterList.onAddCallback += InitializeParameterList;
        }

        private void OnDisable()
        {
            m_ObjectCreatedList.onAddCallback -= InitializeCreatedObjectList;
            m_ParameterList.onAddCallback -= InitializeParameterList;
        }


        public override void OnInspectorGUI()
        {
            const string kNone = "None";

            var action = (ActionDefinition) target;
            var assetPath = AssetDatabase.GetAssetPath(action);
            var assetOnDisk = !string.IsNullOrEmpty(assetPath);
            var editable = !assetOnDisk || AssetDatabaseUtility.IsEditable(assetPath);

            if (!editable)
            {
                EditorGUILayout.HelpBox(
                    "This file is currently read-only. You probably need to check it out from version control.",
                    MessageType.Info);
            }

            GUI.enabled = editable;

            serializedObject.Update();

            var operationalActionType = serializedObject.FindProperty("m_OperationalActionType");
            var operationalActionNames = m_OperationalActionTypes.Where(t => !t.IsGenericType).Select(t => t.Name)
                .Prepend(kNone).ToArray();

            var operationalActionIndex = Array.IndexOf(operationalActionNames, operationalActionType.stringValue);
            EditorGUI.BeginChangeCheck();
            operationalActionIndex =
                EditorGUILayout.Popup("Operational Action", operationalActionIndex, operationalActionNames);
            if (EditorGUI.EndChangeCheck())
            {
                var operationalActionName = operationalActionNames[operationalActionIndex];
                operationalActionType.stringValue =
                    operationalActionName == kNone ? string.Empty : operationalActionName;
            }

            EditorGUILayout.Space();

            var parametersProperty = serializedObject.FindProperty("m_Parameters");
            parametersProperty.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(parametersProperty.isExpanded, EditorStyleHelper.parameters);

            if (parametersProperty.isExpanded)
            {
                if (m_ParameterList.count >= ActionKey.MaxLength)
                    EditorGUILayout.HelpBox(EditorStyleHelper.maxParametersReached);

                m_ParameterList.AdjustElementHeight(m_ParameterList.count == 0 ? 1 : 3);
                m_ParameterList.DoLayoutList();

                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            var customPrecondition = serializedObject.FindProperty("m_CustomPrecondition");
            customPrecondition.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(customPrecondition.isExpanded, EditorStyleHelper.preconditions);

            if (customPrecondition.isExpanded)
            {
                m_PreconditionList.DoLayoutList();

                var customPreconditionNames = m_CustomPreconditionTypes.Where(t => !t.IsGenericType).Select(t => t.Name).Prepend(kNone).ToArray();

                var customPreconditionIndex = Array.IndexOf(customPreconditionNames, customPrecondition.stringValue);
                EditorGUI.BeginChangeCheck();
                customPreconditionIndex = EditorGUILayout.Popup(EditorStyleHelper.customPrecondition, customPreconditionIndex, customPreconditionNames);
                if (EditorGUI.EndChangeCheck())
                {
                    var customPreconditionName = customPreconditionNames[customPreconditionIndex];
                    customPrecondition.stringValue = customPreconditionName == kNone ? string.Empty : customPreconditionName;
                }

                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            var customEffect = serializedObject.FindProperty("m_CustomEffect");
            customEffect.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(customEffect.isExpanded, "Effects");

            if (customEffect.isExpanded)
            {
                GUILayout.Label(EditorStyleHelper.objectsCreated);
                m_ObjectCreatedList.AdjustElementHeight(m_ObjectCreatedList.count == 0 ? 1 : 2);
                m_ObjectCreatedList.DoLayoutList();

                GUILayout.Label(EditorStyleHelper.objectsChanged);
                m_ObjectChangeList.DoLayoutList();

                GUILayout.Label(EditorStyleHelper.objectsRemoved);
                m_ObjectRemovedList.DoLayoutList();

                var customEffectNames = m_CustomEffectTypes.Where(t => !t.IsGenericType).Select(t => t.Name).Prepend(kNone).ToArray();

                var customEffectIndex = Array.IndexOf(customEffectNames, customEffect.stringValue);
                EditorGUI.BeginChangeCheck();
                customEffectIndex = EditorGUILayout.Popup(EditorStyleHelper.objectsCustom, customEffectIndex, customEffectNames);
                if (EditorGUI.EndChangeCheck())
                {
                    var customEffectName = customEffectNames[customEffectIndex];
                    customEffect.stringValue = customEffectName == kNone ? string.Empty : customEffectName;
                }

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            var customReward = serializedObject.FindProperty("m_CustomReward");
            customReward.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(customReward.isExpanded, EditorStyleHelper.rewards);
            if(customReward.isExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Reward"),
                    new GUIContent("Base Value"));


                var customRewardNames = m_CustomRewardTypes.Where(t => !t.IsGenericType).Select(t => t.Name).Prepend(kNone).ToArray();

                var customRewardIndex = Array.IndexOf(customRewardNames, customReward.stringValue);
                EditorGUI.BeginChangeCheck();
                customRewardIndex = EditorGUILayout.Popup("Custom Cost/Reward", customRewardIndex, customRewardNames);
                if (EditorGUI.EndChangeCheck())
                {
                    var customRewardName = customRewardNames[customRewardIndex];
                    customReward.stringValue = customRewardName == kNone ? string.Empty : customRewardName;
                }

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        private void DrawPreconditionListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            const int operatorSize = 50;
            const int spacer = 5;

            var actionParameters = (target as ActionDefinition).Parameters.ToList();

            var w = rect.width;
            var buttonSize = (w - operatorSize - 3 * spacer ) / 2;
            rect.x += spacer;
            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.width = buttonSize;

            var list = m_PreconditionList.serializedProperty;
            var precondition = list.GetArrayElementAtIndex(index);

            var operandA = precondition.FindPropertyRelative("m_OperandA");
            OperandPropertyField(rect, operandA, actionParameters);

            rect.x += buttonSize + spacer;
            rect.width = operatorSize;

            var @operator = precondition.FindPropertyRelative("m_Operator");

            var operators = GetComparisonOperators(operandA);
            var opIndex = EditorGUI.Popup(rect, Array.IndexOf(operators, @operator.stringValue),
                operators, EditorStyleHelper.PopupStyleBold);
            if (opIndex >= 0)
                @operator.stringValue = operators[opIndex];

            rect.x += operatorSize + spacer;
            rect.width = buttonSize;

            OperandPropertyField(rect,precondition.FindPropertyRelative("m_OperandB"), actionParameters,
                GetPossibleValues(operandA));
        }

        private void DrawCreatedObjectListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            DrawNamedObjectElement(rect, index, m_ObjectCreatedList.serializedProperty, GetReservedObjectNames(), false);
        }

        private void DrawParameterList(Rect rect, int index, bool isActive, bool isFocused)
        {
            DrawNamedObjectElement(rect, index, m_ParameterList.serializedProperty, GetReservedObjectNames());
        }

        private void InitializeParameterList(ReorderableList list)
        {
            if (list.count < ActionKey.MaxLength)
            {
                list.serializedProperty.InsertArrayElementAtIndex(list.count);
                InitializeNamedObject(list.serializedProperty.GetArrayElementAtIndex(list.count - 1), list.serializedProperty);
            }
        }

        private void InitializeCreatedObjectList(ReorderableList list)
        {
            list.serializedProperty.InsertArrayElementAtIndex(list.count);
            InitializeNamedObject(list.serializedProperty.GetArrayElementAtIndex(list.count - 1), list.serializedProperty);
        }

        private void DrawRemovedObjectListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var actionParameters = (target as ActionDefinition).Parameters.ToList();

            var list = m_ObjectRemovedList.serializedProperty;
            var removedObject = list.GetArrayElementAtIndex(index);

            OperandPropertyField(rect, removedObject, actionParameters);
        }

        private void DrawEffectListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var actionDefinition = (target as ActionDefinition);
            if (actionDefinition == null) return;

            const int operatorSize = 50;
            const int spacer = 5;

            var parameters = actionDefinition.Parameters.ToList();
            var effectsParameters = actionDefinition.CreatedObjects.ToList();
            parameters.AddRange(effectsParameters);

            var w = rect.width;
            var buttonSize = (w - operatorSize - 3 * spacer ) / 2;
            rect.x += spacer;
            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.width = buttonSize;

            var list = m_ObjectChangeList.serializedProperty;
            var effect = list.GetArrayElementAtIndex(index);

            var operandA = effect.FindPropertyRelative("m_OperandA");
            OperandPropertyField(rect, operandA, parameters);

            rect.x += buttonSize + spacer;
            rect.width = operatorSize;

            var @operator = effect.FindPropertyRelative("m_Operator");

            if (operandA.arraySize > 1)
            {
                var operators = GetAssignationOperators(operandA);
                var opIndex = EditorGUI.Popup(rect, Array.IndexOf(operators, @operator.stringValue),
                    operators, EditorStyleHelper.PopupStyleBold);
                if (opIndex >= 0)
                    @operator.stringValue = operators[opIndex];
            }
            else
            {
                // No operator available
                GUI.Button(rect, string.Empty, EditorStyleHelper.PopupStyle);
            }

            rect.x += operatorSize + spacer;
            rect.width = buttonSize;

            var operandB = effect.FindPropertyRelative("m_OperandB");

            if (operandA.arraySize > 1)
            {
                OperandPropertyField(rect, operandB, parameters,GetPossibleValues(operandA));
            }
            else
            {
                // No operand available
                GUI.Button(rect, string.Empty, EditorStyleHelper.PopupStyle);
            }
        }

        private List<string> GetReservedObjectNames()
        {
            var reservedNames = new List<string>();
            m_ParameterList.serializedProperty.ForEachArrayElement(n => reservedNames.Add(n.FindPropertyRelative("m_Name").stringValue));
            m_ObjectCreatedList.serializedProperty.ForEachArrayElement(n => reservedNames.Add(n.FindPropertyRelative("m_Name").stringValue));
            return reservedNames;
        }

        protected override void OnUniqueNameChanged(string oldName, string newName)
        {
            m_PreconditionList.serializedProperty.ForEachArrayElement(property =>
            {
                RenameOperandParameterName(property, oldName, newName, "m_OperandA");
                RenameOperandParameterName(property, oldName, newName, "m_OperandB");
            });

            m_ObjectChangeList.serializedProperty.ForEachArrayElement(property =>
            {
                RenameOperandParameterName(property, oldName, newName, "m_OperandA");
                RenameOperandParameterName(property, oldName, newName, "m_OperandB");
            });

            m_ObjectRemovedList.serializedProperty.ForEachArrayElement(property =>
            {
                if (property.stringValue == oldName)
                {
                    property.stringValue = newName;
                }
            });
        }
    }
}
