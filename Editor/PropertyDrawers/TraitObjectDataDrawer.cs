using System;
using System.Linq;
using Unity.AI.Planner.Utility;
using UnityEditor.AI.Planner.Utility;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.SceneManagement;

namespace UnityEditor.AI.Planner.Editors
{
    static class TraitObjectDataDrawer
    {
       public static void PropertyField(Rect position, SerializedProperty property, bool readOnly = false)
       {
            var traitDefinitionProperty = property.FindPropertyRelative("m_TraitDefinition");

            var traitDefinition = (TraitDefinition)traitDefinitionProperty.objectReferenceValue;
            if (traitDefinition == null)
                return;

            if (traitDefinitionProperty.objectReferenceValue == null)
            {
                EditorGUILayout.PropertyField(property);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(position.x + EditorStyleHelper.IndentPosition);

                if (GUILayout.Button(new GUIContent($"   {traitDefinitionProperty.objectReferenceValue.name}"), EditorStyleHelper.requiredTraitLabel))
                {
                    property.isExpanded = !property.isExpanded;
                }
                GUILayout.EndHorizontal();
                var foldRect = GUILayoutUtility.GetLastRect();
                foldRect.x += 15f;

                if (traitDefinition.Fields.Any())
                    EditorGUI.Foldout(foldRect, property.isExpanded, string.Empty, true);
            }

            if (!property.isExpanded)
                return;

            using (new EditorGUI.IndentLevelScope((!readOnly && !Application.isPlaying)?2:1))
            {
                EditorGUI.BeginChangeCheck();

                var fieldValuesProperty = property.FindPropertyRelative("m_FieldValues");

                foreach (var field in traitDefinition.Fields)
                {
                    var fieldType = field.FieldType;
                    if (fieldType == null)
                        continue;

                    if (field.Restriction == TraitDefinitionField.FieldRestriction.NotInitializable)
                        continue;

                    int propertyIndex = -1;
                    for (int i = 0; i < fieldValuesProperty.arraySize; i++)
                    {
                        var fieldProperty = fieldValuesProperty.GetArrayElementAtIndex(i);
                        var fieldLabel = fieldProperty.FindPropertyRelative("m_Name").stringValue;
                        if (fieldLabel == field.Name)
                        {
                            propertyIndex = i;
                            break;
                        }
                    }

                    bool toggle = (propertyIndex != -1);

                    var rect = EditorGUILayout.BeginHorizontal();
                    if (!readOnly && !Application.isPlaying)
                    {
                        EditorGUI.BeginChangeCheck();
                        rect.x = position.x + EditorStyleHelper.IndentPosition - 16;
                        rect.width = 16;
                        toggle = GUI.Toggle(rect, toggle, string.Empty);

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (toggle)
                            {
                                var newFieldValue = fieldValuesProperty.InsertArrayElement();
                                newFieldValue.FindPropertyRelative("m_Name").stringValue = field.Name;

                                // Auto-assign current object in a Transform field as a default value
                                if (fieldType == typeof(Transform))
                                {
                                    var objectProperty = newFieldValue.FindPropertyRelative("m_ObjectValue");
                                    var parentObject = property.serializedObject.targetObject as MonoBehaviour;

                                    objectProperty.objectReferenceValue = parentObject.GetComponent<Transform>();
                                }
                            }
                            else
                            {
                                if (propertyIndex != -1)
                                {
                                    fieldValuesProperty.DeleteArrayElementAtIndex(propertyIndex);
                                }
                            }
                        }
                    }

                    if (toggle && propertyIndex != -1)
                    {
                        GUI.enabled = !readOnly;
                        var fieldProperty = fieldValuesProperty.GetArrayElementAtIndex(propertyIndex);

                        FieldValueDrawer.PropertyField(fieldProperty, fieldType, field.Name);
                        GUI.enabled = true;
                    }
                    else
                    {
                        GUI.enabled = false;

                        if (fieldType.IsEnum)
                        {
                            EditorGUILayout.LabelField(field.Name, fieldType.GetEnumName(field.DefaultValue.IntValue));
                        }
                        else
                        {
                            switch (Type.GetTypeCode(fieldType))
                            {
                                case TypeCode.Boolean:
                                    EditorGUILayout.Toggle(field.Name, field.DefaultValue.BoolValue);
                                    break;
                                case TypeCode.Single:
                                case TypeCode.Double:
                                    EditorGUILayout.FloatField(field.Name, field.DefaultValue.FloatValue);
                                    break;
                                case TypeCode.Int32:
                                case TypeCode.Int64:
                                case TypeCode.UInt32:
                                case TypeCode.UInt64:
                                    EditorGUILayout.IntField(field.Name, (int)field.DefaultValue.IntValue);
                                    break;
                                case TypeCode.String:
                                    EditorGUILayout.TextField(field.Name, field.DefaultValue.StringValue);
                                    break;
                                default:
                                    EditorGUILayout.LabelField(field.Name, "None");
                                    break;
                            }
                        }

                        GUI.enabled = true;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
       }
    }
}
