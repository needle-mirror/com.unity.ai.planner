using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    class TraitSelectorPopup : PopupWindowContent
    {
        SerializedProperty m_Property;
        IEnumerable<TraitDefinition> m_InvalidTraits;
        List<TraitDefinition> m_TraitsSelected = new List<TraitDefinition>();
        string m_Title;
        float m_Height;
        Vector2 m_ScrollPosition;

        public TraitSelectorPopup(string title, SerializedProperty property, IEnumerable<TraitDefinition> invalidTraits = null)
        {
            m_Property = property;
            m_Title = title;
            m_InvalidTraits = invalidTraits;

            m_Height = Math.Min(Screen.height, PlannerAssetDatabase.TraitDefinitions.Count() * 20 + 30);

            m_Property.ForEachArrayElement(t =>
            {
                var definition = t.objectReferenceValue as TraitDefinition;
                if (definition != null && !m_TraitsSelected.Contains(definition))
                {
                    m_TraitsSelected.Add(definition);
                }
            });
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(180, m_Height);
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.Label(m_Title, EditorStyles.boldLabel);
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, false, false, GUILayout.Height(rect.height));

            foreach (var trait in PlannerAssetDatabase.TraitDefinitions)
            {
                bool selected = m_TraitsSelected.Contains(trait);

                if (!IsValid(trait))
                {
                    GUI.enabled = false;
                }

                bool newSelected = EditorGUILayout.Toggle(trait.name, selected);

                if (!IsValid(trait))
                {
                    newSelected = false;
                }
                GUI.enabled = true;

                if (selected != newSelected)
                {
                    if (selected)
                    {
                        m_TraitsSelected.Remove(trait);
                    }
                    else
                    {
                        m_TraitsSelected.Add(trait);
                    }

                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.EndScrollView();
        }

        bool IsValid(TraitDefinition trait)
        {
            return m_InvalidTraits == null || !m_InvalidTraits.Contains(trait);
        }

        public override void OnClose()
        {
            bool modified = m_Property.arraySize != m_TraitsSelected.Count;
            if (!modified)
            {
                for (int i = 0; i < m_Property.arraySize; i++)
                {
                    if (m_Property.GetArrayElementAtIndex(i).objectReferenceValue != m_TraitsSelected[i])
                    {
                        modified = true;
                        break;
                    }
                }
            }

            if (modified)
            {
                m_Property.ClearArray();
                m_Property.arraySize = m_TraitsSelected.Count;
                for (var i = 0; i < m_TraitsSelected.Count; i++)
                {
                    m_Property.GetArrayElementAtIndex(i).objectReferenceValue = m_TraitsSelected[i];
                }

                m_Property.serializedObject.ApplyModifiedProperties();
            }
        }
    }

    static class TraitSelectorDrawer
    {
        public static void DrawSelector(SerializedProperty traits, Rect rect, string title, GUIStyle style, GUIStyle buttonStyle, GUIStyle altButtonStyle, IEnumerable<TraitDefinition> invalidTraits = null)
        {
            Rect labelRect = rect;
            labelRect.height = style.fixedHeight + 1;

            bool allTraitDisplayed = true;
            traits.ForEachArrayElement(e =>
            {
                var asset = e.objectReferenceValue as TraitDefinition;

                if (asset == null)
                    return;

                var size = style.CalcSize(new GUIContent(asset.Name));
                labelRect.width = size.x;

                if (labelRect.xMax + altButtonStyle.normal.background.width > rect.xMax)
                {
                    allTraitDisplayed = false;
                    return;
                }

                if (GUI.Button(labelRect, asset.Name, style))
                {
                    PopupWindow.Show(labelRect, new TraitSelectorPopup(title, traits, invalidTraits));
                }

                labelRect.x += size.x + 2;
            });

            var addButtonStyle = allTraitDisplayed ? buttonStyle : altButtonStyle;
            labelRect.width = addButtonStyle.normal.background.width;
            if (GUI.Button(labelRect, string.Empty, addButtonStyle))
            {
                PopupWindow.Show(labelRect, new TraitSelectorPopup(title, traits, invalidTraits));
            }
        }
    }
}
