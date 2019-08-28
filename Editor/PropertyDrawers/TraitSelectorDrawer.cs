using System.Collections.Generic;
using System.Linq;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    class TraitSelectorPopup : PopupWindowContent
    {
        private SerializedProperty m_Traits;
        private IEnumerable<TraitDefinition> m_InvalidTraits;
        private string m_Title;
        private float m_Height;

        public TraitSelectorPopup(string title, SerializedProperty traits, IEnumerable<TraitDefinition> invalidTraits = null)
        {
            m_Traits = traits;
            m_Title = title;
            m_InvalidTraits = invalidTraits;

            m_Height = DomainAssetDatabase.TraitDefinitions.Count() * EditorGUIUtility.singleLineHeight + 60;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(180, m_Height);
        }

        public override void OnGUI(Rect rect)
        {
            m_Traits.serializedObject.Update();

            GUILayout.Label(m_Title, EditorStyles.boldLabel);

            var traitsSelected = new Dictionary<TraitDefinition, int>();
            int index = 0;
            m_Traits.ForEachArrayElement(t =>
            {
                //TODO field values versus traits list ?
                var definition = t.objectReferenceValue as TraitDefinition;
                if (definition != null && !traitsSelected.ContainsKey(definition))
                {
                    traitsSelected.Add(definition, index);
                }

                index++;
            });

            foreach (var trait in DomainAssetDatabase.TraitDefinitions)
            {
                bool selected = traitsSelected.ContainsKey(trait);

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
                        int removeIndex = traitsSelected[trait];

                        m_Traits.GetArrayElementAtIndex(removeIndex).objectReferenceValue = null;
                        m_Traits.DeleteArrayElementAtIndex(removeIndex);
                    }
                    else
                    {
                        m_Traits.InsertArrayElementAtIndex(0);
                        m_Traits.GetArrayElementAtIndex(0).objectReferenceValue = trait;
                    }

                }
            }

            m_Traits.serializedObject.ApplyModifiedProperties();
        }

        private bool IsValid(TraitDefinition trait)
        {
            return m_InvalidTraits == null || !m_InvalidTraits.Contains(trait);
        }
    }

    internal class TraitSelectorDrawer
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
