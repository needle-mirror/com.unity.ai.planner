using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Traits;
using Unity.Semantic.Traits;
using Unity.Entities;
using UnityEditor.AI.Planner.Editors;
using UnityEngine;
using ITrait = Unity.Semantic.Traits.ITrait;

namespace UnityEditor.AI.Planner.Utility
{
    static class TraitGUIUtility
    {
        public static readonly string emptyTraitBasedObjectId = "TraitBasedObjectId.None";

        internal static Type GetOperandValuePropertyType(SerializedProperty operand, SerializedProperty @operator, ref string unknownType)
        {
            var trait = operand.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition;
            if (trait == null)
            {
                return null;
            }

            var field = operand.GetValue<OperandValue>().TraitProperty;
            if (field == default || field.PropertyId == -1 || field.Type == null)
                return null;

            var propertyType = field.Type;
            if (propertyType == typeof(Enum))
            {
                var propertyDefinition = field.GetPropertyDefinition();
                if (propertyDefinition is EnumReferenceProperty enumReference)
                {
                    unknownType = enumReference.Reference.name;
                    return field.Type;
                }
            }

            if (propertyType.IsGenericType && typeof(List<>).IsAssignableFrom(propertyType.GetGenericTypeDefinition()))
            {
                if (IsListComparisonOperator(@operator))
                {
                    var type = typeof(int);
                    unknownType = type.Name;
                    return type;
                }

                if (IsListAssignment(@operator))
                    return propertyType;

                var elementType = propertyType.GetGenericArguments()[0];
                unknownType = elementType.Name;
                return elementType;
            }

            return field.Type;
        }

        public static GUIContent GetOperandDisplayContent(SerializedProperty operand, SerializedProperty @operator,
            IList<ParameterDefinition> parameters, float width, GUIStyle style)
        {
            var normalColor = EditorGUIUtility.isProSkin ? "white" : "black";
            const string errorColor = "#EF2526";

            var parameterProperty = operand.FindPropertyRelative("m_Parameter").stringValue;
            var traitDefinitionProperty = operand.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition;
            var enumProperty = operand.FindPropertyRelative("m_Enum").objectReferenceValue as EnumDefinition;
            var valueProperty = operand.FindPropertyRelative("m_Value").stringValue;

            var operandString = string.Empty;


            if (!string.IsNullOrEmpty(parameterProperty))
            {
                // Check if the specified parameter exist
                bool validParameter = parameters.Any(p => p.Name == parameterProperty);

                operandString += EditorStyleHelper.RichText(parameterProperty, validParameter ? normalColor: errorColor, true);
            }

            if (traitDefinitionProperty != null)
            {
                bool validTrait = true;
                var parentParameter = parameters.FirstOrDefault(p => p.Name == parameterProperty);
                if (parentParameter != null)
                {
                    // Check if the selected Parameter contains this trait
                    validTrait = parentParameter.RequiredTraits.Any(t => t == traitDefinitionProperty);
                }

                var traitName = traitDefinitionProperty.name;
                operandString = AppendOperand(operandString, EditorStyleHelper.RichText(traitName, validTrait ? normalColor : errorColor));

                var traitProperty = operand.GetValue<OperandValue>().TraitProperty;
                // Check if the value is a field of the selected Trait
                bool validTraitValue = traitProperty != default && traitProperty.PropertyId != -1;
                var displayedTrait = validTraitValue ? traitProperty.Name : "undefined";

                operandString = AppendOperand(operandString, EditorStyleHelper.RichText(displayedTrait, validTraitValue ? normalColor : errorColor));

                if (IsListOperand(operand) && IsListComparisonOperator(@operator) && !IsListAssignment(@operator))
                    operandString = AppendOperand(operandString, "Length");
            }
            else
            {
                bool validValue = true;
                if (enumProperty != null)
                {
                    operandString = EditorStyleHelper.RichText(enumProperty.name, normalColor, true);

                    validValue = enumProperty.Elements.Any(p => p.Name == valueProperty);
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

        public static bool IsListOperand(SerializedProperty operand)
        {
            var trait = operand.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition;
            if (trait == null)
                return false;

            var traitProperty = operand.GetValue<OperandValue>().TraitProperty;

            if (traitProperty != default)
            {
                var propertyType = traitProperty.Type;
                if (propertyType != null && propertyType.IsGenericType &&
                    typeof(List<>).IsAssignableFrom(propertyType.GetGenericTypeDefinition()))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsListComparisonOperator(SerializedProperty @operator)
        {
            if (@operator == null)
                return false;

            switch (@operator.stringValue)
            {
                case "==":
                case "!=":
                case "<":
                case ">":
                case "<=":
                case ">=":
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsListUnaryOperator(SerializedProperty @operator)
        {
            if (@operator == null)
                return false;

            switch (@operator.stringValue)
            {
                case "clear":
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsListAssignment(SerializedProperty @operator)
        {
            if (@operator == null)
                return false;

            return @operator.stringValue == "=";
        }


        public static bool IsNumberOperand(SerializedProperty operand)
        {
            var trait = operand.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition;
            if (trait == null)
                return false;

            var traitProperty = operand.GetValue<OperandValue>().TraitProperty;

            if (traitProperty != default && traitProperty.PropertyId != -1)
            {
                var propertyType = traitProperty.Type;
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

        public static void DrawOperandSelectorField(Rect rect, SerializedProperty operand, SerializedProperty @operator,
            IList<ParameterDefinition> parameters, Action<SerializedProperty> onExpectedTypeChanged)
        {
            DrawOperandSelectorField(rect, operand, @operator, parameters, null, default, onExpectedTypeChanged);
        }

        public static void DrawOperandSelectorField(Rect rect, SerializedProperty operand, SerializedProperty @operator,
            IList<ParameterDefinition> parameters, Type expectedType = null, string expectedUnknownType = default,
            Action<SerializedProperty> onExpectedTypeChanged = null)
        {
            var content = GetOperandDisplayContent(operand, @operator, parameters, rect.size.x, EditorStyleHelper.listPopupStyle);

            if (GUI.Button(rect, content, EditorStyleHelper.listPopupStyle))
            {
                var allowParameterSelection = expectedType != null &&
                    (typeof(TraitBasedObjectId).IsAssignableFrom(expectedType)
                        || expectedType == typeof(GameObject) || expectedType == typeof(Entity));
                var allowTraitSelection = !allowParameterSelection && expectedType != null && typeof(ITrait).IsAssignableFrom(expectedType);

                var popup = new OperandSelectorPopup(operand, parameters, allowParameterSelection, allowTraitSelection, onExpectedTypeChanged, expectedType, expectedUnknownType);
                PopupWindow.Show(rect, popup);
            }
        }

        public static void DrawOperandSelectorField(Rect rect, SerializedProperty operand, SerializedProperty @operator,
            IList<ParameterDefinition> parameters, bool allowParameter, bool allowTrait, Action<SerializedProperty> onExpectedTypeChanged)
        {
            var content = GetOperandDisplayContent(operand, @operator, parameters, rect.size.x, EditorStyleHelper.listPopupStyle);

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
            operand.FindPropertyRelative("m_TraitPropertyId").intValue = -1;
            operand.FindPropertyRelative("m_Enum").objectReferenceValue =  null;
            operand.FindPropertyRelative("m_Value").stringValue = string.Empty;

            operand.serializedObject.ApplyModifiedProperties();
        }
    }
}
