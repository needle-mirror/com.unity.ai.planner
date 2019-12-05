using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.AI.Planner.Editors
{
    abstract class BaseTraitOperationEditor : SaveableInspector
    {
        const string k_DefaultParameter = "parameter";

        static readonly string[] s_ReservedParameterNames = { };
        static readonly string[] s_DefaultOperators = { "=" };
        static readonly string[] s_NumberOperators = { "=", "+=", "-=" };
        protected static readonly string[] s_RewardOperators = { "+=", "-=", "*="};
        static readonly string[] s_DefaultComparison = { "==", "!=" };
        static readonly string[] s_NumberComparison = { "==", "!=", "<", ">", "<=", ">=" };

        string m_FocusedControl;

        public override void OnInspectorGUI()
        {
            UpdateFocusedControl();

            base.OnInspectorGUI();
        }

        void UpdateFocusedControl()
        {
            if (Event.current.type != EventType.Layout)
            {
                m_FocusedControl = GUI.GetNameOfFocusedControl();
            }
        }

        protected void InitializeNamedObject(SerializedProperty obj)
        {
            var newName = $"{k_DefaultParameter}1";
            var reservedNames = GetReservedObjectNames();

            var i = 2;
            while (reservedNames.Contains(newName))
            {
                newName = $"{k_DefaultParameter}{i}";
                i++;
            }

            obj.FindPropertyRelative("m_Name").stringValue = newName;
        }

        protected virtual List<string> GetReservedObjectNames()
        {
            return new List<string>();
        }

        protected void RenameOperandParameterName(SerializedProperty property, string oldName, string newName, string operandName)
        {
            var operand = property.FindPropertyRelative(operandName);
            var parameterValue = operand.FindPropertyRelative("m_Parameter");

            if (parameterValue.stringValue == oldName)
            {
                parameterValue.stringValue = newName;
            }
        }

        protected void DrawParameterSelectorField(Rect rect, SerializedProperty parameter, List<ParameterDefinition> parameters, Type expectedType = null)
        {
            var content = new GUIContent(string.IsNullOrEmpty(parameter.stringValue) ? "..." : parameter.stringValue);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var popup = new ParameterSelectorPopup(parameter, parameters, expectedType);
                PopupWindow.Show(rect, popup);
            }
        }

        protected void DrawOperandSelectorField(Rect rect, SerializedProperty operand, List<ParameterDefinition> parameters, bool allowParameter, bool allowTrait, Action<SerializedProperty> onExpectedTypeChanged)
        {
            var content = TraitUtility.GetOperandDisplayContent(operand, parameters, rect.size.x, EditorStyleHelper.listPopupStyle);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var popup = new OperandSelectorPopup(operand, parameters, allowParameter, allowTrait, onExpectedTypeChanged);
                PopupWindow.Show(rect, popup);
            }
        }

        protected void DrawOperandSelectorField(Rect rect, SerializedProperty operand, List<ParameterDefinition> parameters, Action<SerializedProperty> onExpectedTypeChanged)
        {
            DrawOperandSelectorField(rect, operand, parameters, null, default, onExpectedTypeChanged);
        }

        protected void DrawOperandSelectorField(Rect rect, SerializedProperty operand, List<ParameterDefinition> parameters, Type expectedType = null, string expectedUnknownType = default, Action<SerializedProperty> onExpectedTypeChanged = null)
        {
            var content = TraitUtility.GetOperandDisplayContent(operand, parameters, rect.size.x, EditorStyleHelper.listPopupStyle);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var allowParameterSelection = expectedType != null && (typeof(TraitBasedObjectId).IsAssignableFrom(expectedType) || typeof(ITraitBasedObjectData).IsAssignableFrom(expectedType));
                var allowTraitSelection = !allowParameterSelection && expectedType != null && typeof(ITrait).IsAssignableFrom(expectedType);

                var popup = new OperandSelectorPopup(operand, parameters, allowParameterSelection, allowTraitSelection, onExpectedTypeChanged, expectedType, expectedUnknownType);
                PopupWindow.Show(rect, popup);
            }
        }

        protected void DrawNamedObjectElement(Rect rect, int index, SerializedProperty parameter, List<string> reservedNames, bool useProhibitedTraits = true)
        {
            reservedNames.AddRange(s_ReservedParameterNames);
            var rectElement = DrawNamedObjectElement(rect, index, parameter.GetArrayElementAtIndex(index), reservedNames, parameter.name, useProhibitedTraits);

            if (index < parameter.arraySize - 1)
            {
                rectElement.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 1;
                rectElement.height = 1;
                rectElement.width -= 2;

                EditorGUI.DrawRect(rectElement, new Color(0.2f,0.2f,0.2f));
            }
        }

        Rect DrawNamedObjectElement(Rect rect, int index, SerializedProperty namedObject, List<string> reservedNames, string propertyName, bool useProhibitedTraits = true)
        {
            var rectElement = rect;
            rectElement.x += 2;

            var paramName = namedObject.FindPropertyRelative("m_Name");
            reservedNames.Remove(paramName.stringValue);

            rectElement.height = EditorGUIUtility.singleLineHeight;

             var namedFieldGUI = $"{propertyName}#{index}";
            var textFieldStyle = EditorStyleHelper.namedObjectLabel;

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

            TraitSelectorDrawer.DrawSelector(requiredTraits, rectElement,"Required traits", EditorStyleHelper.requiredTraitLabel, EditorStyleHelper.requiredTraitAdd, EditorStyleHelper.requiredTraitMore);

            if (useProhibitedTraits)
            {
                rectElement.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var prohibitedTraits = namedObject.FindPropertyRelative("m_ProhibitedTraits");

                var invalidTraits = new List<TraitDefinition>();
                requiredTraits.ForEachArrayElement(t => invalidTraits.Add(t.objectReferenceValue as TraitDefinition));

                TraitSelectorDrawer.DrawSelector(prohibitedTraits, rectElement,"Prohibited traits", EditorStyleHelper.prohibitedTraitLabel, EditorStyleHelper.prohibitedTraitAdd, EditorStyleHelper.prohibitedTraitMore, invalidTraits);
            }

            return rectElement;
        }

        protected string[] GetComparisonOperators(SerializedProperty operand)
        {
            return IsNumberOperand(operand)?s_NumberComparison:s_DefaultComparison;
        }

        protected string[] GetAssignationOperators(SerializedProperty operand)
        {
            return IsNumberOperand(operand)?s_NumberOperators:s_DefaultOperators;
        }

        static bool IsNumberOperand(SerializedProperty operand)
        {
            var trait = operand.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition;
            if (trait == null)
                return false;

            var fieldId = operand.FindPropertyRelative("m_TraitFieldId").intValue;

            var field = trait.GetField(fieldId);
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

        protected static void ClearOperandProperty(SerializedProperty operand)
        {
            operand.FindPropertyRelative("m_Parameter").stringValue = string.Empty;
            operand.FindPropertyRelative("m_Trait").objectReferenceValue = null;
            operand.FindPropertyRelative("m_TraitFieldId").intValue = 0;
            operand.FindPropertyRelative("m_Enum").objectReferenceValue =  null;
            operand.FindPropertyRelative("m_Value").stringValue = string.Empty;

            operand.serializedObject.ApplyModifiedProperties();
        }

        protected virtual void OnUniqueNameChanged(string oldName, string newName)
        {
        }
    }
}
