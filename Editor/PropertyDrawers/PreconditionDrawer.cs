using System;
using System.Collections.Generic;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    static class PreconditionDrawer
    {
        static readonly string[] k_DefaultComparison = { "==", "!=" };
        static readonly string[] k_NumberComparison = { "==", "!=", "<", ">", "<=", ">=" };

        internal static void PropertyField(Rect rect, IList<ParameterDefinition> parameters, SerializedProperty precondition, Type[] types)
        {
            const int operatorSize = 50;
            const int spacer = 5;

            var w = rect.width;
            var buttonSize = (w - operatorSize - 3 * spacer) / 2;
            rect.x += spacer;
            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.width = buttonSize;

            var @operator = precondition.FindPropertyRelative("m_Operator");
            switch (@operator.stringValue)
            {
                case nameof(Operation.SpecialOperators.Custom):
                    rect.width = w - spacer;
                    var customType = precondition.FindPropertyRelative("m_CustomOperatorType").stringValue;
                    EditorStyleHelper.CustomMethodField(rect, customType, types);
                    break;
                default:
                {
                    var operandA = precondition.FindPropertyRelative("m_OperandA");
                    var operandB = precondition.FindPropertyRelative("m_OperandB");

                    TraitGUIUtility.DrawOperandSelectorField(rect, operandA, parameters, true, false, property =>
                    {
                        TraitGUIUtility.ClearOperandProperty(operandB);
                    });

                    var validLeftOperand = !string.IsNullOrEmpty(operandA.FindPropertyRelative("m_Parameter").stringValue);

                    rect.x += buttonSize + spacer;
                    rect.width = operatorSize;

                    if (validLeftOperand)
                    {
                        var operators = GetComparisonOperators(operandA);
                        var opIndex = EditorGUI.Popup(rect, Array.IndexOf(operators, @operator.stringValue),
                            operators, EditorStyleHelper.listPopupStyleBold);

                        @operator.stringValue = operators[Math.Max(0, opIndex)];
                    }
                    else
                    {
                        // No operand available
                        GUI.enabled = false;
                        GUI.Button(rect, string.Empty, EditorStyleHelper.listPopupStyle);
                        GUI.enabled = true;
                    }

                    rect.x += operatorSize + spacer;
                    rect.width = buttonSize;

                    if (validLeftOperand)
                    {
                        string unknownType = default;
                        TraitGUIUtility.DrawOperandSelectorField(rect, operandB, parameters, TraitGUIUtility.GetOperandValuePropertyType(operandA, ref unknownType), unknownType);
                    }
                    else
                    {
                        // No operand available
                        GUI.enabled = false;
                        GUI.Button(rect, string.Empty, EditorStyleHelper.listPopupStyle);
                        GUI.enabled = true;
                    }
                }
                    break;
            }
        }

        public static void ShowPreconditionMenu(SerializedObject serializedObject, SerializedProperty propertyList, Type[] customPreconditionTypes)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Trait condition"), false, () =>
            {
                serializedObject.Update();
                var newFieldProperty = propertyList.InsertArrayElement();
                newFieldProperty.FindPropertyRelative("m_Operator").stringValue = String.Empty;
                newFieldProperty.FindPropertyRelative("m_CustomOperatorType").stringValue = String.Empty;
                serializedObject.ApplyModifiedProperties();
            });

            foreach (var precondition in customPreconditionTypes)
            {
                menu.AddItem(new GUIContent($"Custom/{precondition.Name}"), false, () =>
                {
                    serializedObject.Update();
                    var newFieldProperty = propertyList.InsertArrayElement();
                    var operatorProperty = newFieldProperty.FindPropertyRelative("m_Operator");
                    operatorProperty.stringValue = nameof(Operation.SpecialOperators.Custom);

                    var operatorCustomProperty = newFieldProperty.FindPropertyRelative("m_CustomOperatorType");
                    operatorCustomProperty.stringValue = precondition.FullName;

                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        static string[] GetComparisonOperators(SerializedProperty operand)
        {
            return TraitGUIUtility.IsNumberOperand(operand)? k_NumberComparison : k_DefaultComparison;
        }
    }
}
