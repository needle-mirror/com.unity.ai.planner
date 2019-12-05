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
    static class TraitUtility
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
            if (field.Type.StartsWith(TypeResolver.DomainEnumsNamespace))
            {
                unknownType = field.Type;
                return typeof(Enum);
            }

            return field.FieldType;
        }

        public static GUIContent GetOperandDisplayContent(SerializedProperty operand, List<ParameterDefinition> parameters, float width, GUIStyle style)
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
    }
}
