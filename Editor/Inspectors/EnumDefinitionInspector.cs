using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(EnumDefinition))]
    class EnumDefinitionInspector : SaveableInspector
    {
        NoHeaderReorderableList m_EnumList;

        void OnEnable()
        {
            m_EnumList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Values"), DrawEnumListElement, 1);
            DomainAssetDatabase.Refresh();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Label(EditorStyleHelper.values, EditorStyles.boldLabel);
            m_EnumList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        void DrawEnumListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            const int indexRectWidth = 20;

            var list = m_EnumList.serializedProperty;
            var value = list.GetArrayElementAtIndex(index);

            rect.height = EditorGUIUtility.singleLineHeight;

            var indexRect = rect;
            indexRect.width = indexRectWidth;
            EditorGUI.LabelField(indexRect, index.ToString(), EditorStyleHelper.smallIndex);

            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.x += indexRectWidth + 2;
            rect.width -= indexRectWidth + 2;
            value.stringValue = EditorGUI.TextField(rect, value.stringValue);
        }
    }
}
