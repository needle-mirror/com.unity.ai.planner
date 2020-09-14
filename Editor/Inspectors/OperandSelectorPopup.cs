using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Traits;
using Unity.Semantic.Traits;
using Unity.DynamicStructs;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;

namespace UnityEditor.AI.Planner.Editors
{
    class OperandSelectorPopup : PopupWindowContent
    {
        const string k_SectionFixedValue = "Fixed value";
        const string k_SectionParameter = "Parameter";
        const string k_SectionNone = "None";

        IList<ParameterDefinition> m_Parameters;
        List<string> m_ParameterValues;
        SerializedProperty m_Property;

        OperandValue m_EditingOperand;

        Type m_ExpectedType;
        string m_ExpectedUnknownType;

        bool m_AllowParameter;
        bool m_AllowTrait;

        Action<SerializedProperty> m_OnExpectedTypeChanged;

        public OperandSelectorPopup(SerializedProperty property, IList<ParameterDefinition> parameters, bool allowParameter, bool allowTrait, Action<SerializedProperty> onExpectedTypeChanged = null, Type expectedType = null, string expectedUnknownType = default)
        {
            m_Property = property;

            m_EditingOperand = new OperandValue()
            {
                Parameter = property.FindPropertyRelative("m_Parameter").stringValue,
                Trait = property.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition,
                TraitProperty = property.FindPropertyRelative("m_TraitProperty").objectReferenceValue as PropertyDefinition,
                Enum = property.FindPropertyRelative("m_Enum").objectReferenceValue as EnumDefinition,
                Value = property.FindPropertyRelative("m_Value").stringValue
            };

            m_Parameters = parameters;
            m_OnExpectedTypeChanged = onExpectedTypeChanged;

            m_AllowParameter = allowParameter;
            m_AllowTrait = allowTrait;

            m_ExpectedType = expectedType;
            m_ExpectedUnknownType = expectedUnknownType;

            if (allowParameter || expectedType == null || expectedType == typeof(UnityEngine.GameObject))
            {
                m_ParameterValues = m_Parameters.Select(param => param.Name).ToList();
            }
            else if (m_AllowTrait)
            {
                m_ParameterValues = m_Parameters.Where(param => param.RequiredTraits.Any()).Select(param => param.Name).ToList();
            }
            else
            {
                m_ParameterValues = new List<string>();

                // Add only parameters that allow valid fields selection
                foreach (var parameter in parameters)
                {
                    if (parameter.RequiredTraits.Any(t => t.properties.Any(IsValidField)))
                    {
                        m_ParameterValues.Add(parameter.Name);
                    }
                }
            }
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(180, 120);
        }

        public override void OnGUI(Rect rect)
        {
            if (m_ParameterValues != null && m_ParameterValues.Count > 0)
            {
                var rootValue = m_EditingOperand.Parameter;

                BeginSection(k_SectionParameter, m_ParameterValues.Contains(rootValue));

                EditorGUI.BeginChangeCheck();
                var parameterIndex = EditorGUILayout.Popup(GUIContent.none, m_ParameterValues.IndexOf(rootValue), m_ParameterValues.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    m_EditingOperand.Clear();
                    m_EditingOperand.Parameter = parameterIndex >= 0 ? m_ParameterValues[parameterIndex] : string.Empty;
                }

                if (parameterIndex >= 0)
                {
                    var parameterDefinition = m_Parameters.FirstOrDefault(p => p.Name == m_ParameterValues[parameterIndex]);
                    if (parameterDefinition != null)
                    {
                        var traits = parameterDefinition.RequiredTraits;
                        var propertyValues = new List<(TraitDefinition, PropertyDefinition)>();
                        foreach (var trait in traits)
                        {
                            if (trait == null)
                                continue;

                            if (m_AllowTrait)
                                propertyValues.Add((trait, null));

                            if (m_ExpectedType != null)
                            {
                                foreach (var field in trait.properties)
                                {
                                    if (IsValidField(field))
                                        propertyValues.Add((trait, field));
                                }
                            }
                            else
                            {
                                propertyValues.AddRange(trait.properties
                                    .Select(field => (trait, field)));
                            }
                        }

                        if (propertyValues.Count > 0)
                        {
                            if (m_AllowParameter)
                                propertyValues.Insert(0, (null, null));

                            var propertyValue = (m_EditingOperand.Trait, TraitProperty: m_EditingOperand.TraitProperty);
                            var displayedOptions = propertyValues.Select(p =>
                                p.Item1 == null ? "-" : p.Item2==null ? p.Item1.name : $"{p.Item1.name}.{p.Item2.property_name}").ToArray();

                            EditorGUI.BeginChangeCheck();
                            var propertyIndex = EditorGUILayout.Popup(GUIContent.none, propertyValues.IndexOf(propertyValue), displayedOptions);

                            var forceFirstValue = !m_AllowParameter && propertyIndex == -1 && propertyValues.Count > 0;
                            if (EditorGUI.EndChangeCheck() || forceFirstValue)
                            {
                                var newValue = propertyValues[Math.Max(0, propertyIndex)];
                                m_EditingOperand.Clear();
                                m_EditingOperand.Parameter = parameterDefinition.Name;
                                m_EditingOperand.Trait = newValue.Item1;
                                m_EditingOperand.TraitProperty = newValue.Item2;
                            }
                        }
                    }
                }

                EndSection();
            }

            if (m_ExpectedType != null)
            {
                DrawFixedValue();
            }
        }

        void DrawFixedValue()
        {
            bool isFixedValue = m_EditingOperand.Trait == null || m_EditingOperand.Parameter == null;
            var fixedValue = (isFixedValue)?m_EditingOperand.Value:string.Empty;

            if (m_ExpectedType == typeof(Enum))
            {
                if (isFixedValue && m_EditingOperand.Enum)
                {
                    fixedValue = $"{m_EditingOperand.Enum.name}.{fixedValue}";
                }

                var enumShortName = m_ExpectedUnknownType;
                if (enumShortName.Contains("."))
                {
                    var enumsNamespaceLength = Unity.Semantic.Traits.Utility.TypeResolver.EnumsQualifier.Length;
                    m_ExpectedUnknownType.Substring(enumsNamespaceLength, m_ExpectedUnknownType.Length - enumsNamespaceLength);
                }
                var enumDefinition = PlannerAssetDatabase.EnumDefinitions.FirstOrDefault(d => d.name == enumShortName);
                if (enumDefinition != null)
                {
                    var values = enumDefinition.properties.Select(e => $"{enumShortName}.{e.property_name}").ToList();

                    BeginSection(k_SectionFixedValue, values.Contains(fixedValue));
                    EditorGUI.BeginChangeCheck();
                    var enumIndex = EditorGUILayout.Popup(GUIContent.none, values.IndexOf(fixedValue), values.ToArray());

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_EditingOperand.Clear();
                        m_EditingOperand.Enum = enumDefinition;
                        m_EditingOperand.Value = enumDefinition.properties.ToArray()[enumIndex].property_name;
                    }

                    EndSection();
                }
                return;
            }
            else if (m_ExpectedType == typeof(GameObject))
            {
                EditorGUI.BeginChangeCheck();

                var value = fixedValue == TraitGUIUtility.emptyTraitBasedObjectId;
                BeginSection(k_SectionNone, value);
                value = EditorGUILayout.Toggle(value);

                if (EditorGUI.EndChangeCheck())
                {
                    m_EditingOperand.Clear();

                    if (value)
                    {
                        m_EditingOperand.Value = TraitGUIUtility.emptyTraitBasedObjectId;
                    }
                    else
                    {
                        m_EditingOperand.Value = string.Empty;
                    }
                }
                EndSection();
            }

            switch (Type.GetTypeCode(m_ExpectedType))
            {
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                {
                    EditorGUI.BeginChangeCheck();

                    var value = 0;
                    if (Int32.TryParse(fixedValue, out value))
                    {
                        BeginSection(k_SectionFixedValue, true);
                        value = EditorGUILayout.IntField(value);
                    }
                    else
                    {
                        BeginSection(k_SectionFixedValue, false);
                        var newValue = EditorGUILayout.TextField(String.Empty);
                        Int32.TryParse(newValue, out value);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_EditingOperand.Clear();
                        m_EditingOperand.Value = value.ToString();
                    }
                    EndSection();
                }
                    break;
                case TypeCode.Single:
                case TypeCode.Double:
                {
                    EditorGUI.BeginChangeCheck();

                    float value = 0;
                    fixedValue = fixedValue.Replace("f", String.Empty);
                    if (Single.TryParse(fixedValue, out value))
                    {
                        BeginSection(k_SectionFixedValue, true);
                        value = EditorGUILayout.FloatField(value);
                    }
                    else
                    {
                        BeginSection("Fixed value", false);
                        var newValue = EditorGUILayout.TextField(String.Empty);
                        Single.TryParse(newValue, out value);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_EditingOperand.Clear();
                        m_EditingOperand.Value = $"{value}f";
                    }
                    EndSection();
                }
                    break;
                case TypeCode.Boolean:
                {
                    EditorGUI.BeginChangeCheck();

                    var useFixedValue = Boolean.TryParse(fixedValue, out var value);

                    BeginSection(k_SectionFixedValue, useFixedValue);

                    if (useFixedValue)
                        value = EditorGUILayout.ToggleLeft(value.ToString(), value);
                    else
                        value = EditorGUILayout.Toggle(value);

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_EditingOperand.Clear();
                        m_EditingOperand.Value = value?"true":"false";
                    }
                    EndSection();
                }
                    break;
            }
        }

        bool IsValidField(PropertyDefinition field)
        {
            if (field.property_type == m_ExpectedType)
            {
                return true;
            }

            return m_ExpectedType == typeof(Enum) && field.property_type.FullName.StartsWith(Unity.Semantic.Traits.Utility.TypeResolver.EnumsQualifier);
        }

        static void BeginSection(string title, bool sectionInUse)
        {
            GUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField(title, sectionInUse?EditorStyles.boldLabel:EditorStyleHelper.grayLabel);
        }

        void EndSection()
        {
            GUILayout.EndVertical();
        }

        public override void OnClose()
        {
            string oldUnknownType = default;
            var oldType = TraitGUIUtility.GetOperandValuePropertyType(m_Property, null, ref oldUnknownType);

            m_Property.FindPropertyRelative("m_Parameter").stringValue = m_EditingOperand.Parameter;
            m_Property.FindPropertyRelative("m_Trait").objectReferenceValue = m_EditingOperand.Trait;
            m_Property.FindPropertyRelative("m_TraitProperty").objectReferenceValue = m_EditingOperand.TraitProperty;
            m_Property.FindPropertyRelative("m_Enum").objectReferenceValue = m_EditingOperand.Enum;
            m_Property.FindPropertyRelative("m_Value").stringValue = m_EditingOperand.Value;
            m_Property.serializedObject.ApplyModifiedProperties();

            string newUnknownType = default;
            var newType = TraitGUIUtility.GetOperandValuePropertyType(m_Property, null, ref newUnknownType);

            if (newType != oldType || newUnknownType != oldUnknownType)
            {
                m_OnExpectedTypeChanged?.Invoke(m_Property);
            }
        }
    }
}
