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
        private SerializedProperty m_TraitFields;
        private string m_Title;
        private float m_Height;

        public FieldTraitSelectorPopup(string title, SerializedProperty traitFields)
        {
            m_TraitFields = traitFields;
            m_Title = title;

            m_Height = DomainAssetDatabase.TraitDefinitions.Count() * EditorGUIUtility.singleLineHeight + 60;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(180, m_Height);
        }

        public override void OnGUI(Rect rect)
        {
            m_TraitFields.serializedObject.Update();

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

            foreach (var trait in DomainAssetDatabase.TraitDefinitions)
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
                        m_TraitFields.arraySize++;
                        var newFieldProperty = m_TraitFields.GetArrayElementAtIndex(m_TraitFields.arraySize - 1);
                        newFieldProperty.isExpanded = true;

                        var traitDefinitionProperty = newFieldProperty.FindPropertyRelative("m_TraitDefinition");
                        traitDefinitionProperty.objectReferenceValue = trait;
                    }
                }
            }

            m_TraitFields.serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(DomainObjectProvider), true)]
    class DomainObjectProviderInspector : Editor
    {
        private string m_FocusedControl;
        int deleteRequestIndex = -1;

        void OnEnable()
        {
            DomainAssetDatabase.Refresh();
        }

        public override void OnInspectorGUI()
        {
            if (Event.current.type != EventType.Layout)
            {
                m_FocusedControl = GUI.GetNameOfFocusedControl();
            }

            serializedObject.Update();

            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing * 4);

            int index = 0;
            var objectDataProperty = serializedObject.FindProperty("m_ObjectData");
            objectDataProperty.ForEachArrayElement(domainObjectData =>
            {
                EditorGUILayout.BeginVertical(EditorStyleHelper.DomainObjectBox);

                var titleRect = GUILayoutUtility.GetRect(1, 22);
                GUI.Box(titleRect, string.Empty);
                titleRect.y += EditorGUIUtility.standardVerticalSpacing;

                var nameProperty = domainObjectData.FindPropertyRelative("m_Name");
                var traitDataProperty = domainObjectData.FindPropertyRelative("m_TraitData");

                var foldoutRect = titleRect;
                foldoutRect.width = 20;
                foldoutRect.x += 15;
                foldoutRect.y += 2;
                domainObjectData.isExpanded = EditorGUI.Foldout(foldoutRect, domainObjectData.isExpanded, string.Empty);

                var textFieldRect = titleRect;
                textFieldRect.x += 15;
                textFieldRect.height -= 2;
                textFieldRect.width -= 36;
                var namedField = "NamedObjectField#" + index;
                GUI.SetNextControlName(namedField);
                var textFieldStyle = (namedField != m_FocusedControl)?EditorStyleHelper.WhiteLargeLabel:EditorStyles.textField;
                nameProperty.stringValue = EditorGUI.TextField(textFieldRect, nameProperty.stringValue, textFieldStyle);

                var iconRect = textFieldRect;
                iconRect.x += textFieldRect.width;
                iconRect.y = foldoutRect.y;

                if (EditorGUI.DropdownButton(iconRect, EditorStyleHelper.GearIconPopup, FocusType.Passive, EditorStyleHelper.IconButtonStyle))
                {
                    iconRect.width = 100;
                    EditorUtility.DisplayCustomMenu(iconRect, new[] {
                            EditorGUIUtility.TrTextContent("Remove Object")
                        }, -1,
                        (data, options, selected) => {
                        {
                            switch (selected)
                            {
                                case 0:
                                    deleteRequestIndex = (int)data;
                                    break;
                            }
                        }}, index);
                }

                if (domainObjectData.isExpanded)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        traitDataProperty.ForEachArrayElement(traitData =>
                        {
                            EditorGUILayout.PropertyField(traitData, true);
                        });

                        GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

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
                    }
                }

                index++;
                EditorGUILayout.EndVertical();
            }, false);

            if (GUILayout.Button("Add domain object"))
            {
                objectDataProperty.arraySize++;
                var newObject = objectDataProperty.GetArrayElementAtIndex(objectDataProperty.arraySize - 1);
                newObject.isExpanded = true;

                var newNameProperty = newObject.FindPropertyRelative("m_Name");
                newNameProperty.stringValue = "New Object";
                var newTaitDataProperty = newObject.FindPropertyRelative("m_TraitData");
                newTaitDataProperty.ClearArray();
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (deleteRequestIndex >= 0)
                {
                    objectDataProperty.DeleteArrayElementAtIndex(deleteRequestIndex);
                    deleteRequestIndex = -1;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
