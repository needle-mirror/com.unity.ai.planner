using System;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine;

namespace UnityEditor.AI.Planner.Editors
{
    // We're not actually creating a property drawer for FieldValue, but this is used to draw a FieldValue
    static class FieldValueDrawer
    {
        public static SerializedProperty GetSerializedProperty(SerializedProperty property, Type fieldType, bool allowObject = false)
        {
            switch (Type.GetTypeCode(fieldType))
            {
                case TypeCode.Boolean:
                    return property.FindPropertyRelative("m_BoolValue");
                case TypeCode.Single:
                case TypeCode.Double:
                    return property.FindPropertyRelative("m_FloatValue");
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return property.FindPropertyRelative("m_IntValue");
                case TypeCode.String:
                    return property.FindPropertyRelative("m_StringValue");
                case TypeCode.Object:
                    return (allowObject)?property.FindPropertyRelative(typeof(TraitBasedObjectId).IsAssignableFrom(fieldType) ? "m_StringValue" : "m_ObjectValue"):null;
            }

            return allowObject?property.FindPropertyRelative("m_ObjectValue"):null;
        }

        public static void PropertyField(SerializedProperty property, Type fieldType, string fieldLabel)
        {
            if (fieldType == null)
                return;

            var valueProperty = GetSerializedProperty(property, fieldType, true);
            if (fieldType.IsEnum)
            {
                valueProperty.intValue = EditorGUILayout.Popup(fieldLabel, valueProperty.intValue,
                    Enum.GetNames(fieldType).Select(e => $"{fieldType.Name}.{e}").ToArray());
            }
            else
            {
                if (valueProperty.name == "m_ObjectValue" && !typeof(TraitBasedObjectId).IsAssignableFrom(fieldType))
                    EditorGUILayout.ObjectField(valueProperty, fieldType, new GUIContent(fieldLabel));
                else
                    EditorGUILayout.PropertyField(valueProperty, new GUIContent(fieldLabel));
            }
        }
    }
}
