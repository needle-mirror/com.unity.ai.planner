using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Utility
{
    abstract class ConditionBasedDrawer : PropertyDrawer
    {
        class ParameterPopup : PopupWindowContent
        {
            Func<string, Type> m_GetType;
            List<ParameterDefinition> m_Parameters;
            List<string> m_ExtraParameters;
            List<string> m_ParameterNames;
            List<string> m_Operand;
            Action<List<string>> m_OnClose;
            bool m_FieldTraversal;

            public ParameterPopup(Func<string, Type> getType, List<ParameterDefinition> parameters, List<string> extraParameters,
                List<string> operand, Action<List<string>> onClose, bool fieldTraversal = true)
            {
                m_GetType = getType;
                m_Parameters = parameters;
                m_ExtraParameters = extraParameters;
                m_ParameterNames = new List<string>(extraParameters.Where(p => !IsNumberRange(p)));
                m_ParameterNames.AddRange(m_Parameters.Select(param => param.Name));
                m_Operand = operand;
                m_OnClose = onClose;
                m_FieldTraversal = fieldTraversal;
            }

            public override void OnGUI(Rect rect)
            {
                var displayNames = m_ParameterNames.ToArray();
                for (var i = 0; i < displayNames.Length; i++)
                {
                    var displayName = displayNames[i];
                    if (displayName == k_None || displayName == k_Default)
                        displayNames[i] = "None";
                }

                GUILayout.Label("Parameter");

                var operand = m_Operand.Count > 0 ? m_Operand[0] : string.Empty;
                EditorGUI.BeginChangeCheck();
                var index = EditorGUILayout.Popup(GUIContent.none, m_ParameterNames.IndexOf(operand), displayNames);
                if (EditorGUI.EndChangeCheck() && index >= 0)
                {
                    if (m_Operand.Count == 0 || m_Operand[0] != m_ParameterNames[index])
                    {
                        m_Operand.Clear();
                        m_Operand.Add(m_ParameterNames[index]);
                    }
                }

                if (m_FieldTraversal && m_Operand.Count > 0 && !m_ExtraParameters.Contains(m_Operand[0]))
                {
                    var subFieldIndex = 0;
                    IEnumerable<Type> types = null;
                    foreach (var currentField in m_Operand)
                    {
                        if (int.TryParse(currentField, out _) || float.TryParse(currentField, out _))
                            break;

                        if (string.IsNullOrEmpty(currentField))
                        {
                            m_Operand.RemoveRange(subFieldIndex, m_Operand.Count - subFieldIndex);
                            break;
                        }

                        if (types == null)
                        {
                            var parameterDefinition = m_Parameters.Find(p => p.Name == currentField);
                            if (parameterDefinition == null)
                                break;

                            var typeNames = parameterDefinition.IncludeTraitTypes;
                            types = typeNames.Select(t => m_GetType(t));
                        }
                        else
                        {
                            var split = currentField.Split('.');
                            var typeName = split[0];
                            var type = m_GetType(typeName);

                            var field = type.GetField(split[1], BindingFlags.Public | BindingFlags.Instance);
                            if (field != null)
                                types = new[] { field.FieldType };
                            else
                                types = new Type[] { };
                        }

                        var properties = types.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.Name != "FieldBag" && p.Name != "VersionStorage")
                            .Select(p => $"{t.Name}.{p.Name}")).ToList();
                        properties.Insert(0, "...");

                        var subsequentFieldIndex = subFieldIndex + 1;
                        var subsequentField = subsequentFieldIndex < m_Operand.Count ? m_Operand[subsequentFieldIndex] : currentField;

                        EditorGUI.BeginChangeCheck();
                        var fieldValueIndex = EditorGUILayout.Popup(GUIContent.none, properties.IndexOf(subsequentField), properties.ToArray());
                        if (EditorGUI.EndChangeCheck() && fieldValueIndex > 0)
                        {
                            var fieldValue = properties[fieldValueIndex];
                            if (subsequentFieldIndex == m_Operand.Count)
                            {
                                m_Operand.Add(fieldValue);
                                break;
                            }

                            if (m_Operand[subsequentFieldIndex] != fieldValue)
                            {
                                m_Operand[subsequentFieldIndex] = fieldValue;
                                var removeStart = subsequentFieldIndex + 1;
                                m_Operand.RemoveRange(removeStart, m_Operand.Count - removeStart);
                                break;
                            }
                        }

                        subFieldIndex++;
                    }
                }

                foreach (var p in m_ExtraParameters)
                {
                    if (IsNumberRange(p))
                    {
                        var split = p.Split('[', '-', ']');
                        var min = split[1];
                        var max = split[2];

                        if (min.Contains('f') || min.Contains('d'))
                        {
                            var value = 0f;
                            if (m_Operand.Count > 0)
                                float.TryParse(m_Operand[0], out value);

                            EditorGUI.BeginChangeCheck();
                            value = EditorGUILayout.FloatField(value);
                            if (EditorGUI.EndChangeCheck())
                            {
                                m_Operand.Clear();
                                m_Operand.Add(value.ToString());
                            }
                        }
                        else
                        {
                            var value = 0;
                            if (m_Operand.Count > 0)
                                int.TryParse(m_Operand[0], out value);

                            EditorGUI.BeginChangeCheck();
                            value = EditorGUILayout.IntField(value);
                            if (EditorGUI.EndChangeCheck())
                            {
                                m_Operand.Clear();
                                m_Operand.Add(value.ToString());
                            }
                        }
                    }
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close"))
                    editorWindow.Close();
            }

            bool IsNumberRange(string parameter)
            {
                return parameter.StartsWith("[") && parameter.EndsWith("]");
            }

            public override string ToString()
            {
                return string.Join(".", m_Operand);
            }

            public override void OnClose()
            {
                m_OnClose(m_Operand);
            }
        }

        const string k_Default = "default";
        const string k_None = "null";

        Dictionary<int, Rect> m_PopupRects = new Dictionary<int, Rect>();

        protected void OperandPropertyField(SerializedProperty operand, List<ParameterDefinition> parameters, List<string> extraParameters = null)
        {
            var operandList = new List<string>();
            var multiElement = operand.isArray && operand.arrayElementType != "char";
            if (multiElement) // strings are considered arrays, too
                operand.ForEachArrayElement(p => operandList.Add(p.stringValue));
            else
                operandList.Add(operand.stringValue);

            var operandString = string.Join(".", operandList.Select(e =>
            {
                var split = e.Split('.');
                var simplified = split[split.Length - 1];
                if (simplified == k_None || simplified == k_Default)
                    simplified = "None";

                return simplified;
            })); // Strip off traits for simplified display
            var content = new GUIContent(string.IsNullOrEmpty(operandString) ? "..." : operandString);

            var controlId = GUIUtility.GetControlID(content, FocusType.Passive);
            m_PopupRects.TryGetValue(controlId, out var popupRect);

            var planDefinition = (PlanDefinition)operand.serializedObject.targetObject;
            var domainDefinition = planDefinition.DomainDefinition;

            if (GUILayout.Button(content, EditorStyles.popup, GUILayout.MinWidth(160f)))
            {
                var popup = new ParameterPopup(domainDefinition.GetType, parameters, extraParameters ?? new List<string>(), operandList, value =>
                {
                    if (multiElement)
                    {
                        operand.ClearArray();

                        var i = 0;
                        foreach (var element in value)
                        {
                            operand.InsertArrayElementAtIndex(i);
                            operand.GetArrayElementAtIndex(i).stringValue = element;
                            i++;
                        }
                    }
                    else
                    {
                        operand.stringValue = value[0];
                    }
                }, multiElement);
                PopupWindow.Show(popupRect, popup);
            }

            if (Event.current.type == EventType.Repaint)
                m_PopupRects[controlId] = GUILayoutUtility.GetLastRect();
        }

        public List<string> GetPossibleValues(SerializedProperty operand)
        {
            var operandList = new List<string>();
            operand.ForEachArrayElement(p => operandList.Add(p.stringValue));

            if (operandList.Count == 0)
                return null;

            var finalElement = operandList[operandList.Count - 1].Split('.');

            if (finalElement.Length == 2)
            {
                var planDefinition = (PlanDefinition)operand.serializedObject.targetObject;
                var domainDefinition = planDefinition.DomainDefinition;
                var type = domainDefinition.GetType(finalElement[0]);
                if (type != null)
                {
                    var fieldInfo = type.GetField(finalElement[1], BindingFlags.Public | BindingFlags.Instance);
                    if (fieldInfo != null)
                    {
                        var fieldType = fieldInfo.FieldType;
                        if (fieldType.IsEnum)
                            return Enum.GetNames(fieldType).Select(e => $"{fieldType.Name}.{e}").ToList();

                        if (fieldType.IsClass)
                            return new List<string> { k_None };

                        if (fieldType.IsValueType)
                        {
                            if (!fieldType.IsPrimitive) // IsStruct
                                return new List<string> { k_Default };

                            switch (Type.GetTypeCode(fieldType))
                            {
                                case TypeCode.Boolean:
                                    return new List<string> { "true", "false" };

                                case TypeCode.Int32:
                                    return new List<string> { $"[{Int32.MinValue}-{Int32.MaxValue}]" };

                                case TypeCode.Int64:
                                    return new List<string> { $"[{Int64.MinValue}-{Int64.MaxValue}]" };

                                case TypeCode.UInt32:
                                    return new List<string> { $"[{UInt32.MinValue}-{UInt32.MaxValue}]" };

                                case TypeCode.UInt64:
                                    return new List<string> { $"[{UInt64.MinValue}-{UInt64.MaxValue}]" };

                                case TypeCode.Single:
                                    return new List<string> { $"[{Single.MinValue}f-{Single.MaxValue}f]" };

                                case TypeCode.Double:
                                    return new List<string> { $"[{Double.MinValue}d-{Double.MaxValue}d]" };

                            }
                        }
                    }
                }
            }

            return null;
        }

        public override float GetPropertyHeight(SerializedProperty field, GUIContent label)
        {
            return -2f;
        }
    }
}
