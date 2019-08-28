using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.AI.Planner.Utility;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    abstract class SaveableInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save"))
            {
                // FIXME: Currently this is a hack since it isn't possible to save a single asset; This will at least touch
                // the file, so that the AI domain assemblies rebuild
                var assetPath = AssetDatabase.GetAssetPath(target);
                AssetDatabase.ForceReserializeAssets(new[] { assetPath });
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    abstract class BaseConditionalInspector : SaveableInspector
    {
        static readonly string[] s_ReservedParameterNames = { };

        static readonly string[] s_DefaultOperators = { "=" };
        static readonly string[] s_NumberOperators = { "=", "-=", "+=" };

        static readonly string[] s_DefaultComparison = { "==", "!=" };
        static readonly string[] s_NumberComparison = { "==", "!=", "<", ">", "<=", ">=" };

        const string k_DefaultParameter = "obj";
        const string k_Default = "default";
        const string k_None = "null";

        Dictionary<int, Rect> m_PopupRects = new Dictionary<int, Rect>();
        private string m_FocusedControl;

        protected class OperandParameterPopup : PopupWindowContent
        {
            List<ParameterDefinition> m_Parameters;
            List<string> m_ExtraParameters;
            List<string> m_ParameterNames;
            List<string> m_Operand;
            Action<List<string>> m_OnClose;
            bool m_PropertySelection;

            public OperandParameterPopup(List<ParameterDefinition> parameters, List<string> extraParameters,
                List<string> operand, Action<List<string>> onClose, bool propertySelection = true)
            {
                m_Parameters = parameters;
                m_ExtraParameters = extraParameters;
                m_ParameterNames = new List<string>(extraParameters.Where(p => !IsNumberRange(p)));
                m_ParameterNames.AddRange(m_Parameters.Select(param => param.Name));
                m_Operand = operand;
                m_OnClose = onClose;
                m_PropertySelection = propertySelection;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(180, 150);
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

                if (m_PropertySelection && m_Operand.Count > 0 && !m_ExtraParameters.Contains(m_Operand[0]))
                {
                    foreach (var currentProperty in m_Operand)
                    {
                        var parameterDefinition = m_Parameters.Find(p => p.Name == currentProperty);
                        if (parameterDefinition == null)
                            continue;

                        var traits = parameterDefinition.RequiredTraits;

                        List<string> properties = new List<string>();
                        foreach (var trait in traits)
                        {
                            if (trait == null)
                                continue;

                            properties.Add(trait.Name);
                            properties.AddRange(trait.Fields.Where(field => !field.HiddenField).Select(field => $"{trait.Name}.{field.Name}"));
                        }

                        if (properties.Count > 0)
                        {
                            properties.Insert(0, "-");

                            var propertyValueIndex = (m_Operand.Count > 1) ? properties.IndexOf(m_Operand[1]) : 0;

                            EditorGUI.BeginChangeCheck();
                            propertyValueIndex = EditorGUILayout.Popup(GUIContent.none, propertyValueIndex, properties.ToArray());
                            GUI.backgroundColor = Color.white;

                            if (EditorGUI.EndChangeCheck())
                            {
                                m_Operand.Clear();
                                m_Operand.Add(m_ParameterNames[index]);

                                if (propertyValueIndex > 0)
                                {
                                    var selectedProperty = properties[propertyValueIndex];
                                    m_Operand.Add(selectedProperty);
                                }

                                break;
                            }
                        }
                    }
                }

                foreach (var p in m_ExtraParameters)
                {
                    if (IsNumberRange(p))
                    {
                        var split = p.Split('[', '-', ']');
                        var min = split[1];
                        var max = split[2];

                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.LabelField("Fixed value");

                        if (min.Contains('f') || min.Contains('d'))
                        {
                            var value = 0f;
                            if (m_Operand.Count > 0 && float.TryParse(m_Operand[0], out value))
                            {
                                value = EditorGUILayout.FloatField(value);
                            }
                            else
                            {
                                var newValue = EditorGUILayout.TextField(string.Empty);
                                float.TryParse(newValue, out value);
                            }

                            if (EditorGUI.EndChangeCheck())
                            {
                                m_Operand.Clear();
                                m_Operand.Add(value.ToString());
                            }
                        }
                        else
                        {
                            var value = 0;
                            if (m_Operand.Count > 0 && int.TryParse(m_Operand[0], out value))
                            {
                                value = EditorGUILayout.IntField(value);
                            }
                            else
                            {
                                var newValue = EditorGUILayout.TextField(string.Empty);
                                int.TryParse(newValue, out value);
                            }

                            if (EditorGUI.EndChangeCheck())
                            {
                                m_Operand.Clear();
                                m_Operand.Add(value.ToString());
                            }
                        }
                    }
                }
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

        protected void OperandPropertyField(Rect rect, SerializedProperty operand, List<ParameterDefinition> parameters, List<string> extraParameters = null)
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

            if (GUI.Button(rect, content, EditorStyleHelper.PopupStyle))
            {
                var popup = new OperandParameterPopup(parameters, extraParameters ?? new List<string>(), operandList, value =>
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

                    operand.serializedObject.ApplyModifiedProperties();

                }, multiElement);
                PopupWindow.Show(rect, popup);
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
                var trait = DomainAssetDatabase.TraitDefinitions.FirstOrDefault(d => d.Name == finalElement[0]);
                if (trait == null)
                    return null;

                var field = trait.GetField(finalElement[1]);
                if (field == null)
                    return null;

                var enumNamespace = $"{TypeResolver.DomainsNamespace}.Enums.";
                if (field.Type.StartsWith(enumNamespace))
                {
                    var enumShortName =
                        field.Type.Substring(enumNamespace.Length, field.Type.Length - enumNamespace.Length);
                    var enumDefinition = DomainAssetDatabase.EnumDefinitions.FirstOrDefault(d => d.Name == enumShortName);
                    return enumDefinition == null ? null : enumDefinition.Values.Select(e => $"{enumShortName}.{e}").ToList();
                }

                var propertyType = field.FieldType;
                if (propertyType == null)
                    return null;

                if (propertyType.IsEnum)
                    return Enum.GetNames(propertyType).Select(e => $"{propertyType.Name}.{e}").ToList();

                if (propertyType.IsClass)
                    return new List<string> { k_None };

                if (propertyType.IsValueType)
                {
                    if (!propertyType.IsPrimitive) // IsStruct
                        return new List<string> {k_Default};

                    switch (Type.GetTypeCode(propertyType))
                    {
                        case TypeCode.Boolean:
                            return new List<string> {"true", "false"};

                        case TypeCode.Int32:
                            return new List<string> {$"[{Int32.MinValue}-{Int32.MaxValue}]"};

                        case TypeCode.Int64:
                            return new List<string> {$"[{Int64.MinValue}-{Int64.MaxValue}]"};

                        case TypeCode.UInt32:
                            return new List<string> {$"[{UInt32.MinValue}-{UInt32.MaxValue}]"};

                        case TypeCode.UInt64:
                            return new List<string> {$"[{UInt64.MinValue}-{UInt64.MaxValue}]"};

                        case TypeCode.Single:
                            return new List<string> {$"[{Single.MinValue}f-{Single.MaxValue}f]"};

                        case TypeCode.Double:
                            return new List<string> {$"[{Double.MinValue}d-{Double.MaxValue}d]"};

                    }
                }
            }

            return null;
        }

        protected string[] GetComparisonOperators(SerializedProperty operand)
        {
            return IsNumberOperand(operand)?s_NumberComparison:s_DefaultComparison;
        }

        protected string[] GetAssignationOperators(SerializedProperty operand)
        {
            return IsNumberOperand(operand)?s_NumberOperators:s_DefaultOperators;
        }

        private static bool IsNumberOperand(SerializedProperty operand)
        {
            var operandList = new List<string>();
            operand.ForEachArrayElement(p => operandList.Add(p.stringValue));

            if (operandList.Count == 0)
            {
                return false;
            }

            var finalElement = operandList[operandList.Count - 1].Split('.');

            if (finalElement.Length != 2) return false;

            var trait = DomainAssetDatabase.TraitDefinitions.FirstOrDefault(d => d.Name == finalElement[0]);
            if (trait == null) return false;
            var field = trait.GetField(finalElement[1]);
            if (field != null)
            {
                var propertyType = field.FieldType;
                if (propertyType != null && propertyType.IsPrimitive)
                {
                    switch (Type.GetTypeCode(propertyType))
                    {
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public override void OnInspectorGUI()
        {
            if (Event.current.type != EventType.Layout)
            {
                m_FocusedControl = GUI.GetNameOfFocusedControl();
            }

            base.OnInspectorGUI();
        }

        protected void InitializeNamedObject(SerializedProperty obj, SerializedProperty list)
        {
            var newName = k_DefaultParameter;
            var usedName = new List<string>();
            list.ForEachArrayElement(n => usedName.Add(n.FindPropertyRelative("m_Name").stringValue));
            var i = 2;
            while (usedName.Contains(newName))
            {
                newName = $"{k_DefaultParameter}{i}";
                i++;
            }

            obj.FindPropertyRelative("m_Name").stringValue = newName;
        }

        protected virtual void OnUniqueNameChanged(string oldName, string newName)
        {
        }

        protected void RenameOperandParameterName(SerializedProperty property, string oldName, string newName, string operandName)
        {
            var operand = property.FindPropertyRelative(operandName);
            if (operand.arraySize > 0)
            {
                var paramOperand = operand.GetArrayElementAtIndex(0);
                if (paramOperand.stringValue == oldName)
                {
                    paramOperand.stringValue = newName;
                }
            }
        }

        protected void DrawSingleNamedObject(Rect rect, SerializedProperty namedObject)
        {
            DrawNamedObjectElement(rect, 0, namedObject, s_ReservedParameterNames.ToList(), namedObject.name);
        }

        protected void DrawNamedObjectElement(Rect rect, int index, SerializedProperty parameter, List<string> reservedNames, bool useProhibitedTraits = true)
        {
            reservedNames.AddRange(s_ReservedParameterNames);
            var rectElement = DrawNamedObjectElement(rect, index, parameter.GetArrayElementAtIndex(index), reservedNames, parameter.name, useProhibitedTraits);

            if (index < parameter.arraySize - 1)
            {
                rectElement.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                rectElement.height = 1;
                rectElement.width -= 2;

                EditorGUI.DrawRect(rectElement, new Color(0.25f,0.25f,0.25f));
            }
        }

        private Rect DrawNamedObjectElement(Rect rect, int index, SerializedProperty namedObject, List<string> reservedNames, string propertyName, bool useProhibitedTraits = true)
        {
            var rectElement = rect;
            rectElement.x += 2;

            var paramName = namedObject.FindPropertyRelative("m_Name");
            reservedNames.Remove(paramName.stringValue);

            rectElement.height = EditorGUIUtility.singleLineHeight;

             var namedFieldGUI = $"{propertyName}#{index}";
            var textFieldStyle = EditorStyleHelper.WhiteLargeLabel;

            GUI.SetNextControlName(namedFieldGUI);
            if (namedFieldGUI == m_FocusedControl)
            {
                textFieldStyle = EditorStyles.textField;
                rectElement.y += EditorGUIUtility.standardVerticalSpacing;
            }
            else
            {
                rectElement.height += 10;
            }

            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUI.DelayedTextField(rectElement, paramName.stringValue, textFieldStyle);

            if (EditorGUI.EndChangeCheck())
            {
                // Remove characters not allowed in code generation
                newValue = Regex.Replace(newValue, @"(^\d+)|([^a-zA-Z0-9_])", string.Empty);

                // Avoid duplicate or reserved names
                if (reservedNames.Contains(newValue))
                {
                    var i = 2;
                    while (reservedNames.Contains($"{newValue}{i}")) { i++; }
                    newValue = $"{newValue}{i}";
                }

                if (newValue.Length > 0 && paramName.stringValue != newValue)
                {
                    OnUniqueNameChanged(paramName.stringValue, newValue);

                    paramName.stringValue = newValue;
                    GUI.FocusControl(string.Empty);
                }
            }

            rectElement.x += 2;
            rectElement.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2;

            var requiredTraits = namedObject.FindPropertyRelative("m_RequiredTraits");

            TraitSelectorDrawer.DrawSelector(requiredTraits, rectElement,"Required traits", EditorStyleHelper.RequiredTraitLabel, EditorStyleHelper.RequiredTraitAdd, EditorStyleHelper.RequiredTraitMore);

            if (useProhibitedTraits)
            {
                rectElement.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var prohibitedTraits = namedObject.FindPropertyRelative("m_ProhibitedTraits");

                var invalidTraits = new List<TraitDefinition>();
                requiredTraits.ForEachArrayElement(t => invalidTraits.Add(t.objectReferenceValue as TraitDefinition));

                TraitSelectorDrawer.DrawSelector(prohibitedTraits, rectElement,"Prohibited traits", EditorStyleHelper.ProhibitedTraitLabel, EditorStyleHelper.ProhibitedTraitAdd, EditorStyleHelper.ProhibitedTraitMore, invalidTraits);
            }

            return rectElement;
        }
    }
}
