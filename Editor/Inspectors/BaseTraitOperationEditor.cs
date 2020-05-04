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
        static readonly string[] k_ReservedParameterNames = { };

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

        protected static void RenameOperandParameterName(SerializedProperty property, string oldName, string newName, string operandName)
        {
            var operand = property.FindPropertyRelative(operandName);
            var parameterValue = operand.FindPropertyRelative("m_Parameter");

            if (parameterValue.stringValue == oldName)
            {
                parameterValue.stringValue = newName;
            }
        }

        protected Rect DrawNamedObjectElement(Rect rect, int index, SerializedProperty parameter, List<string> reservedNames, bool useProhibitedTraits = true)
        {
            reservedNames.AddRange(k_ReservedParameterNames);
            rect = DrawNamedObjectElement(rect, index, parameter.GetArrayElementAtIndex(index), reservedNames, parameter.name, useProhibitedTraits);
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            return rect;
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

        protected virtual void OnUniqueNameChanged(string oldName, string newName)
        {
        }
    }
}
