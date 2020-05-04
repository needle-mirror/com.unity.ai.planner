using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    class ComparerSelectorPopup : PopupWindowContent
    {
        SerializedProperty m_ComparerProperty;
        SerializedProperty m_ReferenceProperty;

        List<string> m_ComparerDisplayNames;
        List<string> m_ComparerFullNames;

        Type[] m_ComparerTypes;

        List<ParameterDefinition> m_AvailableParameters;

        public ComparerSelectorPopup(SerializedProperty comparerProperty, SerializedProperty referenceProperty, Type[] comparerTypes, List<ParameterDefinition> availableParameters)
        {
            m_ComparerProperty = comparerProperty;
            m_ReferenceProperty = referenceProperty;

            m_ComparerTypes = comparerTypes;
            m_AvailableParameters = availableParameters;

            m_ComparerDisplayNames = comparerTypes.Select(t => t.Name).ToList();
            m_ComparerFullNames = comparerTypes.Select(t => t.FullName).ToList();

            m_ComparerDisplayNames.Insert(0, "-");
            m_ComparerFullNames.Insert(0, string.Empty);
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.Label($"Comparer", EditorStyles.boldLabel);

            var parameter = m_ComparerProperty.stringValue;
            EditorGUI.BeginChangeCheck();
            var index = EditorGUILayout.Popup(GUIContent.none, m_ComparerFullNames.IndexOf(parameter), m_ComparerDisplayNames.ToArray());

            if (EditorGUI.EndChangeCheck() && index >= 0)
            {
                m_ComparerProperty.stringValue = index >= 0 ? m_ComparerFullNames[index] : string.Empty;
                m_ComparerProperty.serializedObject.ApplyModifiedProperties();
            }

            var currentType = m_ComparerTypes.FirstOrDefault(t => t.FullName == m_ComparerProperty.stringValue);
            if (currentType != null)
            {
                var parameterComparerWithReferenceType = currentType.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IParameterComparerWithReference<,>).Name);
                if (parameterComparerWithReferenceType != null)
                {
                    var traitTypeExpected = parameterComparerWithReferenceType.GenericTypeArguments[1].Name;
                    var referenceableParameters = m_AvailableParameters.Where(p => p.RequiredTraits.FirstOrDefault(t => t.Name == traitTypeExpected) != null).ToList();

                    GUILayout.Label($"Reference {traitTypeExpected}", EditorStyles.boldLabel);

                    var parameterNames = referenceableParameters.Select(p => p.Name).ToArray();
                    var referenceIndex = referenceableParameters.FindIndex(p => p.Name == m_ReferenceProperty.stringValue);

                    EditorGUI.BeginChangeCheck();
                    var newReferenceIndex = EditorGUILayout.Popup(GUIContent.none, referenceIndex, parameterNames);

                    if (EditorGUI.EndChangeCheck() || newReferenceIndex != referenceIndex)
                    {
                        m_ReferenceProperty.stringValue = parameterNames[newReferenceIndex];
                        m_ReferenceProperty.serializedObject.ApplyModifiedProperties();
                    }
                }
            }
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(200, 100);
        }
    }
}
