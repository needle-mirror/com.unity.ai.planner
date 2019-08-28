using System;
using System.Linq;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomPropertyDrawer(typeof(TraitDefinitionPickerAttribute))]
    class TraitDefinitionPickerDrawer : PropertyDrawer
    {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var traitDefinitions = DomainAssetDatabase.TraitDefinitions.ToArray();
            var traits = traitDefinitions.Select(t => t?.Name).ToArray();

            if (!((TraitDefinitionPickerAttribute)attribute).ShowLabel)
                label.text = " ";

            var endProperty = property.GetEndProperty();
            while (!SerializedProperty.EqualContents(property, endProperty))
            {
                var index = Array.IndexOf(traitDefinitions, property.objectReferenceValue);
                EditorGUI.BeginChangeCheck();
                index = EditorGUI.Popup(position, label.text, index, traits);

                if (EditorGUI.EndChangeCheck())
                    property.objectReferenceInstanceIDValue = traitDefinitions[index].GetInstanceID();

                if (!property.NextVisible(false))
                    break;
            }
        }
    }
}
