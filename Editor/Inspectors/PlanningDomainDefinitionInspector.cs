using UnityEditor.AI.Planner.Utility;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(PlanningDomainDefinition), true)]
    class PlanningDomainDefinitionInspector : SaveableInspector
    {
        NoHeaderReorderableList m_ActionList;
        NoHeaderReorderableList m_TerminationList;

        void OnEnable()
        {
            m_ActionList = new NoHeaderReorderableList(serializedObject,
                serializedObject.FindProperty("m_ActionDefinitions"), DrawActionListElement, 1);

            m_TerminationList = new NoHeaderReorderableList(serializedObject,
                serializedObject.FindProperty("m_StateTerminationDefinitions"), DrawTerminationListElement, 1);

            DomainAssetDatabase.Refresh();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Label(EditorStyleHelper.actions, EditorStyles.boldLabel);
            m_ActionList.DoLayoutList();

            GUILayout.Label(EditorStyleHelper.terminations, EditorStyles.boldLabel);
            m_TerminationList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        void DrawActionListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_ActionList.serializedProperty;
            var value = list.GetArrayElementAtIndex(index);

            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.ObjectField(rect, value, EditorGUIUtility.TrTextContent(string.Empty));
        }

        void DrawTerminationListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_TerminationList.serializedProperty;
            var value = list.GetArrayElementAtIndex(index);

            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.ObjectField(rect, value, EditorGUIUtility.TrTextContent(string.Empty));
        }
    }
}
