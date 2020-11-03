using Unity.AI.Planner;
using UnityEngine;

namespace UnityEditor.AI.Planner
{
    [CustomPropertyDrawer(typeof(PlannerSettings))]
    class PlannerSearchSettingsDrawer : PropertyDrawer
    {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.serializedObject.Update();
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label, true );
            if (property.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    // Settings
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("PlanningIterationsPerUpdate"));
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("StateExpansionBudgetPerIteration"));

                    DrawEnabledProperty(property, "UseCustomPlanningFrequency", "MinFramesPerPlanningUpdate");
                    DrawEnabledProperty(property, "CapPlanSize", "MaxStatesInPlan");
                    DrawEnabledProperty(property, "StopPlanningWhenToleranceAchieved", "RootEstimatedRewardTolerance");
                    DrawEnabledProperty(property, "CapPlanUpdates", "MaxUpdates");

                    EditorGUILayout.PropertyField(property.FindPropertyRelative("GraphSelectionJobMode"));
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("GraphBackpropagationJobMode"));
                }
            }

            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty();
        }

        static void DrawEnabledProperty(SerializedProperty property, string enablePropertyName, string settingPropertyName)
        {
            var toggleProperty = property.FindPropertyRelative(enablePropertyName);
            EditorGUILayout.PropertyField(toggleProperty);

            GUI.enabled = toggleProperty.boolValue;
            EditorGUILayout.PropertyField(property.FindPropertyRelative(settingPropertyName));
            GUI.enabled = true;
        }

    }
}
