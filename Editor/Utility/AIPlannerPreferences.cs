using System;
using System.IO;
using UnityEditorInternal;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.AI.Planner
{
    class AIPlannerPreferences : ScriptableObject
    {
        public bool AutoCompile => m_AutoCompile;

        public bool AutoSaveAssets
        {
            get => m_AutoSaveAssets;
            set
            {
                m_AutoSaveAssets = value;
                SaveSettings();
            }
        }

        const string k_PreferencesPath = "Library/AIPlannerPreferences.asset";

        const string k_DisplayControllerAdvancedSettingsPrefs = "AI.Planner.DisplayControllerAdvancedSettings";
        const string k_DisplayActionDefinitionAdvancedSettingsPrefs =  "AI.Planner.DisplayActionDefinitionAdvancedSettings";
        const string k_DisplayPlanDefinitionAdvancedSettingsPrefs =  "AI.Planner.DisplayPlanDefinitionAdvancedSettings";

        static AIPlannerPreferences s_Preferences;

        [SerializeField]
        bool m_AutoCompile = true;

        [SerializeField]
        bool m_AutoSaveAssets = true;

        [SerializeField]
        bool m_ExpertMode = false;

        internal static AIPlannerPreferences GetOrCreatePreferences()
        {
            if (!s_Preferences)
            {
                if (File.Exists(k_PreferencesPath))
                    s_Preferences = (AIPlannerPreferences)InternalEditorUtility.LoadSerializedFileAndForget(k_PreferencesPath)[0];

                if (!s_Preferences)
                    s_Preferences = CreateInstance<AIPlannerPreferences>();
            }

            return s_Preferences;
        }

        public static bool displayControllerAdvancedSettings
        {
            get => EditorPrefs.GetBool(k_DisplayControllerAdvancedSettingsPrefs, GetOrCreatePreferences().m_ExpertMode);
            set => EditorPrefs.SetBool(k_DisplayControllerAdvancedSettingsPrefs, value);
        }

        public static bool displayActionDefinitionAdvancedSettings
        {
            get => EditorPrefs.GetBool(k_DisplayActionDefinitionAdvancedSettingsPrefs, GetOrCreatePreferences().m_ExpertMode);
            set => EditorPrefs.SetBool(k_DisplayActionDefinitionAdvancedSettingsPrefs, value);
        }

        public static bool displayPlanDefinitionAdvancedSettings
        {
            get => EditorPrefs.GetBool(k_DisplayPlanDefinitionAdvancedSettingsPrefs, GetOrCreatePreferences().m_ExpertMode);
            set => EditorPrefs.SetBool(k_DisplayPlanDefinitionAdvancedSettingsPrefs, value);
        }

        static void SaveSettings()
        {
            if (s_Preferences)
                InternalEditorUtility.SaveToSerializedFileAndForget( new UnityObject[] { s_Preferences }, k_PreferencesPath, true);
        }

        static SerializedObject GetSerializedPreferences()
        {
            return new SerializedObject(GetOrCreatePreferences());
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/AI Planner", SettingsScope.User)
            {
                deactivateHandler = SaveSettings,
                guiHandler = searchContext =>
                {
                    var settings = GetSerializedPreferences();

                    // Auto-compile
                    {
                        var label = new GUIContent("Auto-build (play mode)",
                            "If any planner assets have changed, then the generated assemblies for the AI Planner "
                            + "will be re-generated before entering into play mode.");

                        EditorGUILayout.PropertyField(settings.FindProperty(nameof(m_AutoCompile)), label);
                    }

                    // Auto-save assets
                    {
                        var label = new GUIContent("Auto-save assets",
                            "Any planner assets you change will be saved immediately.");

                        EditorGUILayout.PropertyField(settings.FindProperty(nameof(m_AutoSaveAssets)), label);
                    }

                    // Expert mode
                    {
                        var label = new GUIContent("Expert mode", "Advanced settings are displayed by default.");

                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(settings.FindProperty(nameof(m_ExpertMode)), label);

                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.DeleteKey(k_DisplayControllerAdvancedSettingsPrefs);
                            EditorPrefs.DeleteKey(k_DisplayActionDefinitionAdvancedSettingsPrefs);
                            EditorPrefs.DeleteKey(k_DisplayPlanDefinitionAdvancedSettingsPrefs);
                        }
                    }

                    settings.ApplyModifiedProperties();
                }
            };

            return provider;
        }
    }
}
