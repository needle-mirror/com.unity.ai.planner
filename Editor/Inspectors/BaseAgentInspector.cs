using System;
using System.Collections.Generic;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(BaseAgent<,,,,,,,,,>), true)]
    class BaseAgentInspector : Editor
    {
        void OnEnable()
        {
            DomainAssetDatabase.Refresh();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var definition = serializedObject.FindProperty("m_PlanningDefinition");
            EditorGUILayout.PropertyField(definition);

            var traitDataProperty = serializedObject.FindProperty("m_InitialStateTraitData");

            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField(EditorStyleHelper.initialState, EditorStyleHelper.WhiteLargeLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                traitDataProperty.ForEachArrayElement(domainObjectData =>
                {
                    EditorGUILayout.PropertyField(domainObjectData, true);
                }, false);
            }
            var lastRect = GUILayoutUtility.GetLastRect();
            lastRect.x += EditorStyleHelper.IndentPosition;
            GUILayout.BeginHorizontal();
            GUILayout.Space(lastRect.x);
            if (EditorGUILayout.DropdownButton(new GUIContent("Select Traits"), FocusType.Passive, GUILayout.Width(100)))
            {
                lastRect.y += EditorGUIUtility.singleLineHeight;
                PopupWindow.Show(lastRect, new FieldTraitSelectorPopup("Select Traits", traitDataProperty));
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // TODO Add Initial Query editor with filters
            DrawPropertiesExcluding(serializedObject, "m_InitialStateTraitData", "m_Script", "m_InitialObjectQuery", "m_PlanningDefinition");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
