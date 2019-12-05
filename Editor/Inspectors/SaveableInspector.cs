using System;
using UnityEditor.AI.Planner.Utility;
using UnityEditor.Presets;
using UnityEngine;

namespace UnityEditor.AI.Planner.Editors
{
    abstract class SaveableInspector : Editor
    {
        Preset m_OriginalObject;

        bool CanSave => target && EditorUtility.IsPersistent(target) && !serializedObject.isEditingMultipleObjects;

        void Awake()
        {
            if (CanSave)
                m_OriginalObject = new Preset(target);
        }

        void OnDestroy()
        {
            if (CanSave && EditorUtility.IsDirty(target))
            {
                var preferences = AIPlannerPreferences.GetOrCreatePreferences();
                if (preferences.AutoSaveAssets)
                    ApplyChanges();
                else
                {
                    var choice = EditorUtility.DisplayDialog("Unapplied changes",
                        $"Would you like to apply the changes made to {AssetDatabase.GetAssetPath(serializedObject.targetObject)}?",
                        "Apply", "Revert");

                    switch (choice)
                    {
                        case true:
                            ApplyChanges();
                            break;

                        case false:
                            RevertChanges();
                            break;
                    }
                }
            }
        }

        void RevertChanges()
        {
            if (CanSave)
            {
                m_OriginalObject.ApplyTo(target);
                SaveAsset();
            }
        }

        void ApplyChanges(bool reselect = true)
        {
            if (CanSave)
                SaveAsset();
        }

        void SaveAsset()
        {
            EditorApplication.delayCall += () => AssetDatabaseUtility.SaveSingleAsset(target);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var preferences = AIPlannerPreferences.GetOrCreatePreferences();
            preferences.AutoSaveAssets = GUILayout.Toggle(preferences.AutoSaveAssets, "Auto-save assets");

            if (!preferences.AutoSaveAssets)
            {
                GUI.enabled = EditorUtility.IsDirty(target);
                if (GUILayout.Button("Revert"))
                {
                    RevertChanges();
                    GUI.FocusControl(null);
                }
                else if (GUILayout.Button("Apply"))
                {
                    ApplyChanges();
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
