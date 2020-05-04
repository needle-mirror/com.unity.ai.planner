using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
     class FieldTraitSelectorPopup : PopupWindowContent
    {
        SerializedProperty m_TraitFields;
        string m_Title;
        float m_Height;

        public FieldTraitSelectorPopup(string title, SerializedProperty traitFields)
        {
            m_TraitFields = traitFields;
            m_Title = title;

            m_Height = PlannerAssetDatabase.TraitDefinitions.Count() * EditorGUIUtility.singleLineHeight + 60;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(180, m_Height);
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.Label(m_Title, EditorStyles.boldLabel);

            var traitsSelected = new Dictionary<TraitDefinition, int>();
            var indexToRemove = new List<int>();
            int index = 0;
            m_TraitFields.ForEachArrayElement(t =>
            {
                var traitDefinitionProperty = t.FindPropertyRelative("m_TraitDefinition");
                var definition = traitDefinitionProperty.objectReferenceValue as TraitDefinition;
                if (definition == null)
                {
                    indexToRemove.Add(index);
                }
                else if (!traitsSelected.ContainsKey(definition))
                {
                    traitsSelected.Add(definition, index);
                }

                index++;
            });

            foreach (var i in indexToRemove)
            {
                Debug.LogWarning("A trait definition is missing, a field has been removed.");
                m_TraitFields.DeleteArrayElementAtIndex(i);
            }

            foreach (var trait in PlannerAssetDatabase.TraitDefinitions)
            {
                bool selected = traitsSelected.ContainsKey(trait);
                bool newSelected = EditorGUILayout.Toggle(trait.name, selected);

                if (selected != newSelected)
                {
                    if (selected)
                    {
                        int removeIndex = traitsSelected[trait];
                        m_TraitFields.DeleteArrayElementAtIndex(removeIndex);
                    }
                    else
                    {
                        // Initialize new Trait fields
                        var newFieldProperty = m_TraitFields.InsertArrayElement();
                        newFieldProperty.isExpanded = true;

                        var traitDefinitionProperty = newFieldProperty.FindPropertyRelative("m_TraitDefinition");
                        traitDefinitionProperty.objectReferenceValue = trait;

                        foreach (var traitField in trait.Fields)
                        {
                            // Auto-create field value for a Transform field
                            if (traitField.FieldType == typeof(Transform))
                            {
                                var parentObject = m_TraitFields.serializedObject.targetObject as MonoBehaviour;

                                if (parentObject == null)
                                    continue;

                                var fieldValuesProperty = newFieldProperty.FindPropertyRelative("m_FieldValues");
                                var newFieldValue = fieldValuesProperty.InsertArrayElement();

                                newFieldValue.FindPropertyRelative("m_Name").stringValue = traitField.Name;
                                newFieldValue.FindPropertyRelative("m_ObjectValue").objectReferenceValue = parentObject.GetComponent<Transform>();
                            }
                        }
                    }
                }
            }
            m_TraitFields.serializedObject.ApplyModifiedProperties();
        }

        public override void OnClose()
        {
            m_TraitFields?.serializedObject?.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(TraitComponent), true)]
    class TraitComponentInspector : BaseTraitObjectEditor
    {
        void OnEnable()
        {
            PlannerAssetDatabase.Refresh();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();

            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing * 4);

            var objectDataProperty = serializedObject.FindProperty("m_ObjectData");
            DrawTraitObjectData(objectDataProperty, false);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
