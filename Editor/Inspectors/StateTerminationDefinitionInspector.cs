using System;
using System.Collections.Generic;
using UnityEditor.AI.Planner.Utility;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(StateTerminationDefinition))]
    class StateTerminationDefinitionInspector : BaseConditionalInspector
    {
        private NoHeaderReorderableList m_CriteriaList;

        void OnEnable()
        {
            m_CriteriaList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Criteria"), DrawCriteriaListElement, 1);
            DomainAssetDatabase.Refresh();
        }

        public override void OnInspectorGUI()
        {
            var terminalStateDefinition = (StateTerminationDefinition) target;
            var assetPath = AssetDatabase.GetAssetPath(terminalStateDefinition);
            var assetOnDisk = !string.IsNullOrEmpty(assetPath);
            var editable = !assetOnDisk || AssetDatabaseUtility.IsEditable(assetPath);

            if (!editable)
            {
                EditorGUILayout.HelpBox(
                    "This file is currently read-only. You probably need to check it out from version control.",
                    MessageType.Info);
            }

            EditorGUILayout.Separator();

            GUI.enabled = editable;
            serializedObject.Update();

            GUILayout.Label(EditorStyleHelper.parameters, EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(GUI.skin.box);

            Rect r = GUILayoutUtility.GetRect(Screen.width , EditorGUIUtility.singleLineHeight * 4);
            DrawSingleNamedObject(r, serializedObject.FindProperty("m_ObjectParameters")); // TODO should Termination be a list too ?

            GUILayout.EndVertical();

            GUILayout.Label(EditorStyleHelper.criteria, EditorStyles.boldLabel);
            m_CriteriaList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        private void DrawCriteriaListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            const int operatorSize = 40;
            const int spacer = 5;

            var actionParameters = new List<ParameterDefinition>
            {
                (target as StateTerminationDefinition).ObjectParameters
            };

            var w = rect.width;
            var buttonSize = (w - operatorSize - 3 * spacer ) / 2;
            rect.x += spacer;
            rect.width = buttonSize;

            var list = m_CriteriaList.serializedProperty;
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

        protected override void OnUniqueNameChanged(string oldName, string newName)
        {
            m_CriteriaList.serializedProperty.ForEachArrayElement(property =>
            {
                RenameOperandParameterName(property, oldName, newName, "m_OperandA");
                RenameOperandParameterName(property, oldName, newName, "m_OperandB");
            });
        }
    }
}
