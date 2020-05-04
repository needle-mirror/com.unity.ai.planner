using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    class ParameterSelectorPopup : PopupWindowContent
    {
        SerializedProperty m_Property;
        List<string> m_ParameterNames;

        string m_ExpectedTrait;

        public ParameterSelectorPopup(SerializedProperty property, IList<ParameterDefinition> parameters, Type expectedType = null)
        {
            m_Property = property;

            if (expectedType != null && typeof(ITrait).IsAssignableFrom(expectedType))
            {
                m_ExpectedTrait = expectedType.Name;
                m_ParameterNames = parameters.Where(param => param.RequiredTraits.Any(t => t.Name == m_ExpectedTrait)).Select(param => param.Name).ToList();
            }
            else
            {
                m_ParameterNames = parameters.Select(param => param.Name).ToList();
            }
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.Label($"Parameter{(!string.IsNullOrEmpty(m_ExpectedTrait) ? $" ({m_ExpectedTrait})" : string.Empty)}", EditorStyles.boldLabel);

            if (m_ParameterNames.Count > 0)
            {
                var parameter = m_Property.stringValue;
                EditorGUI.BeginChangeCheck();
                var index = EditorGUILayout.Popup(GUIContent.none, m_ParameterNames.IndexOf(parameter), m_ParameterNames.ToArray());

                if (EditorGUI.EndChangeCheck() && index >= 0)
                {
                    if (index >= 0)
                    {
                        m_Property.stringValue = m_ParameterNames[index];
                    }
                    else
                    {
                        m_Property.stringValue = string.Empty;
                    }
                    m_Property.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                EditorGUILayout.LabelField("No parameter with this Trait", EditorStyleHelper.italicGrayLabel);
            }

        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(180, 80);
        }
    }
}
