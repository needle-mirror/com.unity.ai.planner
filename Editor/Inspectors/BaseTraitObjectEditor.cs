using System;
using System.Collections.Generic;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;

namespace UnityEditor.AI.Planner.Editors
{
    abstract class BaseTraitObjectEditor : Editor
    {
        string m_FocusedControl;
        HashSet<string> m_TraitObjectExpanded = new HashSet<string>();
        protected int m_DeleteItemRequest = -1;

        public override void OnInspectorGUI()
        {
            UpdateFocusedControl();
        }

        void UpdateFocusedControl()
        {
            if (Event.current.type != EventType.Layout)
            {
                m_FocusedControl = GUI.GetNameOfFocusedControl();
            }
        }

        protected void DrawTraitObjectData(SerializedProperty traitBasedObjectData, bool readOnly, int index = 0, bool allowMultiple = false)
        {
            EditorGUILayout.BeginVertical(EditorStyleHelper.traitBasedObjectBox);

            var titleRect = GUILayoutUtility.GetRect(1, (readOnly)?18:22);
            GUI.Box(titleRect, string.Empty, EditorStyleHelper.traitBasedObjectTitleBox);

            var nameProperty = traitBasedObjectData.FindPropertyRelative("m_Name");
            var traitDataProperty = traitBasedObjectData.FindPropertyRelative("m_TraitData");

            var foldoutRect = titleRect;
            foldoutRect.width = 12;
            foldoutRect.x += 14;

            bool newObjectExpanded;
            if (readOnly)
            {
                var objectDataId = traitBasedObjectData.serializedObject.targetObject.GetInstanceID() + traitBasedObjectData.propertyPath;
                var objectExpanded = m_TraitObjectExpanded.Contains(objectDataId);
                newObjectExpanded = EditorGUI.Foldout(foldoutRect, objectExpanded, string.Empty, true);

                if (objectExpanded && !newObjectExpanded)
                {
                    m_TraitObjectExpanded.Remove(objectDataId);
                }
                else if (!objectExpanded && newObjectExpanded)
                {
                    m_TraitObjectExpanded.Add(objectDataId);
                }
            }
            else
            {
                traitBasedObjectData.isExpanded = EditorGUI.Foldout(foldoutRect, traitBasedObjectData.isExpanded, string.Empty, true);
                newObjectExpanded = traitBasedObjectData.isExpanded;
            }

            var textFieldRect = titleRect;
            textFieldRect.x += 15;
            textFieldRect.height -= 2;
            textFieldRect.width -= 36;

            if (readOnly)
            {
                EditorGUI.LabelField(textFieldRect, nameProperty.stringValue, EditorGUIUtility.isProSkin ?
                    EditorStyleHelper.lightGrayLabel : EditorStyleHelper.grayLabel);
            }
            else
            {
                var namedField = "NamedObjectField#" + index;
                GUI.SetNextControlName(namedField);
                var textFieldStyle = (namedField != m_FocusedControl)?EditorStyleHelper.namedObjectLabel:EditorStyles.textField;
                nameProperty.stringValue = EditorGUI.TextField(textFieldRect, nameProperty.stringValue, textFieldStyle);
            }

            var iconRect = textFieldRect;
            iconRect.x += textFieldRect.width;
            iconRect.y = foldoutRect.y + 2;

            if (!readOnly && !Application.isPlaying && allowMultiple && EditorGUI.DropdownButton(iconRect, EditorStyleHelper.gearIconPopup, FocusType.Passive, EditorStyleHelper.iconButtonStyle))
            {
                iconRect.width = 100;
                EditorUtility.DisplayCustomMenu(iconRect, new[] { EditorGUIUtility.TrTextContent("Remove Object") }, -1, TraitBasedObjectMenu, index);
            }

            if (newObjectExpanded && (!readOnly || traitDataProperty.arraySize > 0))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space();
                    traitDataProperty.ForEachArrayElement(traitData =>
                    {
                        var rect = GUILayoutUtility.GetLastRect();
                        TraitObjectDataDrawer.PropertyField(rect, traitData, readOnly);
                    });

                    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

                    if (!readOnly && !Application.isPlaying)
                    {
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
            }

            EditorGUILayout.EndVertical();
        }

        void TraitBasedObjectMenu(object userdata, string[] options, int selected)
        {
            switch (selected)
            {
                case 0:
                    m_DeleteItemRequest = (int)userdata;
                    break;
            }
        }
    }
}
