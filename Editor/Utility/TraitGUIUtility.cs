using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;
using UnityEditor.AI.Planner.Editors;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Utility
{
    static class TraitGUIUtility
    {
        public static readonly string emptyTraitBasedObjectId = "TraitBasedObjectId.None";

        internal static Type GetOperandValuePropertyType(SerializedProperty operand, ref string unknownType)
        {
            var trait = operand.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition;
            if (trait == null)
            {
                if (!string.IsNullOrEmpty(operand.FindPropertyRelative("m_Parameter").stringValue))
                {
                    return typeof(ITraitBasedObjectData);
                }

                return null;
            }

            var fieldId = operand.FindPropertyRelative("m_TraitFieldId").intValue;

            var field = trait.GetField(fieldId);
            if (field == null)
                return null;

            // Enum from Planner asset may be not known ahead of time
            if (field.Type.StartsWith(TypeResolver.TraitEnumsNamespace))
            {
                unknownType = field.Type;
                return typeof(Enum);
            }

            return field.FieldType;
        }

        public static GUIContent GetOperandDisplayContent(SerializedProperty operand, IList<ParameterDefinition> parameters, float width, GUIStyle style)
        {
            var normalColor = EditorGUIUtility.isProSkin ? "white" : "black";
            const string errorColor = "#EF2526";

            var parameterProperty = operand.FindPropertyRelative("m_Parameter").stringValue;
            var traitProperty = operand.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition;
            var enumProperty = operand.FindPropertyRelative("m_Enum").objectReferenceValue as EnumDefinition;
            var valueProperty = operand.FindPropertyRelative("m_Value").stringValue;

            var operandString = string.Empty;


            if (!string.IsNullOrEmpty(parameterProperty))
            {
                // Check if the specified parameter exist
                bool validParameter = parameters.Any(p => p.Name == parameterProperty);

                operandString += EditorStyleHelper.RichText(parameterProperty, validParameter ? normalColor: errorColor, true);
            }

            if (traitProperty != null)
            {
                bool validTrait = true;
                var parentParameter = parameters.FirstOrDefault(p => p.Name == parameterProperty);
                if (parentParameter != null)
                {
                    // Check if the selected Parameter contains this trait
                    validTrait = parentParameter.RequiredTraits.Any(t => t == traitProperty);
                }

                var traitName = traitProperty.Name;
                operandString = AppendOperand(operandString, EditorStyleHelper.RichText(traitName, validTrait ? normalColor : errorColor));

                var traidFieldId = operand.FindPropertyRelative("m_TraitFieldId").intValue;
                if (traidFieldId > 0)
                {
                    // Check if the value is a field of the selected Trait
                    bool validTraitValue = traitProperty.Fields.Any(f => f.UniqueId == traidFieldId);
                    var displayedTrait = validTraitValue?traitProperty.GetFieldName(traidFieldId):"undefined";

                    operandString = AppendOperand(operandString, EditorStyleHelper.RichText(displayedTrait, validTraitValue ? normalColor : errorColor));
                }
            }
            else
            {
                bool validValue = true;
                if (enumProperty != null)
                {
                    operandString = EditorStyleHelper.RichText(enumProperty.Name, normalColor, true);

                    validValue = enumProperty.Values.Contains(valueProperty);
                }

                if (!string.IsNullOrEmpty(valueProperty))
                {
                    foreach (var valuePart in valueProperty.Split('.'))
                    {
                        operandString = AppendOperand(operandString, EditorStyleHelper.RichText(valuePart, validValue ? normalColor : errorColor));
                    }
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                // Simplify display for parameter properties if the text doesn't fit
                Vector2 contentSize = style.CalcSize(new GUIContent(operandString));
                if (contentSize.x > width)
                {
                    var operandParts = operandString.Split('.');
                    if (operandParts.Length == 3)
                    {
                        operandString = $"{operandParts[0]}...{operandParts[2]}";

                        contentSize = style.CalcSize(new GUIContent(operandString));
                        if (contentSize.x > width)
                        {
                            operandString = $"...{operandParts[2]}";
                        }
                    }
                    else if (operandParts.Length == 2)
                    {
                        operandString = $"...{operandParts[1]}";
                    }
                }
            }

            return new GUIContent(string.IsNullOrEmpty(operandString) ? "..." : operandString);;
        }

        static string AppendOperand(string operand, string value)
        {
            return string.IsNullOrEmpty(operand) ? value : $"{operand}.{value}";
        }

        public static bool IsNumberOperand(SerializedProperty operand)
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

        public static void DrawOperandSelectorField(Rect rect, SerializedProperty operand, IList<ParameterDefinition> parameters, Action<SerializedProperty> onExpectedTypeChanged)
        {
            DrawOperandSelectorField(rect, operand, parameters, null, default, onExpectedTypeChanged);
        }

        public static void DrawOperandSelectorField(Rect rect, SerializedProperty operand, IList<ParameterDefinition> parameters, Type expectedType = null, string expectedUnknownType = default, Action<SerializedProperty> onExpectedTypeChanged = null)
        {
            var content = GetOperandDisplayContent(operand, parameters, rect.size.x, EditorStyleHelper.listPopupStyle);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var allowParameterSelection = expectedType != null && (typeof(TraitBasedObjectId).IsAssignableFrom(expectedType) || typeof(ITraitBasedObjectData).IsAssignableFrom(expectedType));
                var allowTraitSelection = !allowParameterSelection && expectedType != null && typeof(ITrait).IsAssignableFrom(expectedType);

                var popup = new OperandSelectorPopup(operand, parameters, allowParameterSelection, allowTraitSelection, onExpectedTypeChanged, expectedType, expectedUnknownType);
                PopupWindow.Show(rect, popup);
            }
        }

        public static void DrawOperandSelectorField(Rect rect, SerializedProperty operand, IList<ParameterDefinition> parameters, bool allowParameter, bool allowTrait, Action<SerializedProperty> onExpectedTypeChanged)
        {
            var content = GetOperandDisplayContent(operand, parameters, rect.size.x, EditorStyleHelper.listPopupStyle);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var popup = new OperandSelectorPopup(operand, parameters, allowParameter, allowTrait, onExpectedTypeChanged);
                PopupWindow.Show(rect, popup);
            }
        }

        public static void DrawParameterSelectorField(Rect rect, SerializedProperty parameter, IList<ParameterDefinition> parameters, Type expectedType = null)
        {
            var content = new GUIContent(string.IsNullOrEmpty(parameter.stringValue) ? "..." : parameter.stringValue);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var popup = new ParameterSelectorPopup(parameter, parameters, expectedType);
                PopupWindow.Show(rect, popup);
            }
        }

        public static void ClearOperandProperty(SerializedProperty operand)
        {
            operand.FindPropertyRelative("m_Parameter").stringValue = string.Empty;
            operand.FindPropertyRelative("m_Trait").objectReferenceValue = null;
            operand.FindPropertyRelative("m_TraitFieldId").intValue = 0;
            operand.FindPropertyRelative("m_Enum").objectReferenceValue =  null;
            operand.FindPropertyRelative("m_Value").stringValue = string.Empty;

            operand.serializedObject.ApplyModifiedProperties();
        }
    }
}
