using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.AI.Planner.Utility
{
    static class SerializedPropertyExtensions
    {
        public static void ForEachArrayElement(this SerializedProperty property, Action<SerializedProperty> callback,
            bool showSizeField = false)
        {
            property = property.Copy();
            var endProperty = property.GetEndProperty();
            property.NextVisible(true); // Enter into the collection

            if (showSizeField)
                EditorGUILayout.PropertyField(property);

            property.NextVisible(false); // Step past the size field

            while (!SerializedProperty.EqualContents(property, endProperty))
            {
                callback(property);

                if (!property.NextVisible(false))
                    break;
            }
        }

        public static void DrawArrayProperty(this SerializedProperty property, bool showSizeField = true)
        {
            EditorGUILayout.PropertyField(property);

            if (!property.isExpanded)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                property.ForEachArrayElement(domainObjectData =>
                {
                    EditorGUILayout.PropertyField(domainObjectData, true);
                }, showSizeField);
            }
        }

        public static T FindObjectOfType<T>(this SerializedProperty property) where T : UnityObject
        {
            var found = property.serializedObject.targetObject as T;

            // It's possible that the object is located within a member field, so look for it there
            if (!found)
            {
                var searchProperty = property.serializedObject.GetIterator();
                while (searchProperty.NextVisible(true))
                {
                    if (searchProperty.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        found = searchProperty.objectReferenceValue as T;
                        if (found)
                            break;
                    }
                }
            }

            return found;
        }

        public static T GetValue<T>(this SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            var targetObject = (object)serializedObject.targetObject;

            var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var propertyPath = property.propertyPath;
            FieldInfo fieldInfo = null;
            while (!string.IsNullOrEmpty(propertyPath))
            {
                var dotIndex = propertyPath.IndexOf('.');
                var field = propertyPath;
                if (dotIndex >= 0)
                {
                    field = propertyPath.Substring(0, dotIndex);
                    propertyPath = propertyPath.Substring(dotIndex + 1);
                }
                else
                {
                    propertyPath = String.Empty;
                }

                if (field == "Array")
                {
                    if (targetObject is IList list)
                    {
                        var match = Regex.Match(propertyPath, @"\d+");
                        if (match.Success)
                        {
                            if (int.TryParse(match.Value, out var index))
                            {
                                targetObject = list[index];
                                dotIndex = propertyPath.IndexOf('.');
                                if (dotIndex >= 0)
                                    propertyPath = propertyPath.Substring(dotIndex + 1);
                                else
                                    propertyPath = string.Empty;
                                fieldInfo = null;
                            }
                        }
                    }

                }
                else
                {
                    var currentType = (fieldInfo == null) ? targetObject.GetType() : fieldInfo.FieldType;

                    fieldInfo = currentType.GetFieldRecursively(field, bindingFlags);

                    if (fieldInfo == null)
                    {
                        throw new ArgumentException($"FieldInfo {field} not found in {currentType.FullName}");
                    }
                    targetObject = fieldInfo.GetValue(targetObject);
                }
            }

            return (T)targetObject;
        }

        public static void SaveSceneObject(this SerializedProperty property)
        {
            var serializedObj = property.serializedObject;
            EditorUtility.SetDirty(serializedObj.targetObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            serializedObj.Update();
        }
    }
}
