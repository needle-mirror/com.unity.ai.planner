using UnityEngine;
using UnityEngine.AI.Planner;

namespace UnityEditor.AI.Planner
{
    [CustomPropertyDrawer(typeof(PlanExecutionSettings))]
    class PlannerExecutionSettingsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.serializedObject.Update();
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var executionModeProperty = property.FindPropertyRelative("ExecutionMode");
                    EditorGUILayout.PropertyField(executionModeProperty);

                    switch ((PlanExecutionSettings.PlanExecutionMode)executionModeProperty.enumValueIndex)
                    {
                        case PlanExecutionSettings.PlanExecutionMode.ActImmediately:
                        case PlanExecutionSettings.PlanExecutionMode.WaitForManualExecutionCall:
                        case PlanExecutionSettings.PlanExecutionMode.WaitForPlanCompletion:
                            break;

                        case PlanExecutionSettings.PlanExecutionMode.WaitForMinimumSearchTime:
                            EditorGUILayout.PropertyField(property.FindPropertyRelative("MinimumSearchTime"));
                            break;

                        case PlanExecutionSettings.PlanExecutionMode.WaitForMaximumDecisionTolerance:
                            EditorGUILayout.PropertyField(property.FindPropertyRelative("MaximumDecisionTolerance"));
                            break;
                        case PlanExecutionSettings.PlanExecutionMode.WaitForMinimumPlanSize:
                            EditorGUILayout.PropertyField(property.FindPropertyRelative("MinimumPlanSize"));
                            break;
                    }
                }
            }

            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty();
        }
    }
}
