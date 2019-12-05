using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.AI.Planner.Utility
{
    static class DomainAssetDatabase
    {
        static string s_BuiltinModulePath = $"com.unity.ai.planner{Path.DirectorySeparatorChar}Runtime{Path.DirectorySeparatorChar}Modules{Path.DirectorySeparatorChar}";

        static IEnumerable<TraitDefinition> s_TraitDefinitions = null;
        static IEnumerable<ActionDefinition> s_ActionDefinitions = null;
        static IEnumerable<EnumDefinition> s_EnumDefinitions = null;
        static IEnumerable<PlanDefinition> s_PlanDefinitions = null;
        static IEnumerable<StateTerminationDefinition> s_StateTerminationDefinitions = null;

        static string s_LastPathUsedForNewAsset;

        public static IEnumerable<TraitDefinition> TraitDefinitions
        {
            get
            {
                if (s_TraitDefinitions == null)
                {
                    UpdateTraitDefinitions();
                }

                return s_TraitDefinitions;
            }
        }

        public static IEnumerable<ActionDefinition> ActionDefinitions
        {
            get
            {
                if (s_ActionDefinitions == null)
                {
                    UpdateActionDefinitions();
                }

                return s_ActionDefinitions;
            }
        }

        public static IEnumerable<EnumDefinition> EnumDefinitions
        {
            get
            {
                if (s_EnumDefinitions == null)
                {
                    UpdateEnumDefinitions();
                }

                return s_EnumDefinitions;
            }
        }

        public static IEnumerable<StateTerminationDefinition> StateTerminationDefinitions
        {
            get
            {
                if (s_StateTerminationDefinitions == null)
                {
                    UpdateTerminationDefinitions();
                }

                return s_StateTerminationDefinitions;
            }
        }

        public static IEnumerable<PlanDefinition> PlanDefinitions
        {
            get
            {
                if (s_PlanDefinitions == null)
                {
                    UpdatePlanDefinitions();
                }

                return s_PlanDefinitions;
            }
        }

        public static void Refresh()
        {
            UpdateEnumDefinitions();
            UpdateTraitDefinitions();
            UpdateActionDefinitions();
            UpdatePlanDefinitions();
            UpdateTerminationDefinitions();
        }

        internal static UnityObject CreateNewPlannerAsset<T>(string assetName) where T : ScriptableObject
        {
            var defaultDirectory = $"{Application.dataPath}/AI.Planner";

            var directoryPath = Path.GetDirectoryName(s_LastPathUsedForNewAsset);
            if (directoryPath != null && Directory.Exists(directoryPath))
            {
                defaultDirectory = directoryPath;
            }
            var assetPath = EditorUtility.SaveFilePanelInProject($"{assetName} Definition", $"New {assetName}", "asset", $"Create a new {assetName} Definition", defaultDirectory);

            // If user canceled or save path is invalid return
            if (assetPath == "")
                return null;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            AssetDatabase.SaveAssets();

            Refresh();

            s_LastPathUsedForNewAsset = assetPath;
            return asset;
        }

        static void UpdateEnumDefinitions()
        {
            s_EnumDefinitions = AssetDatabase.FindAssets($"t: {nameof(EnumDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<EnumDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        static void UpdateActionDefinitions()
        {
            s_ActionDefinitions = AssetDatabase.FindAssets($"t: {nameof(ActionDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<ActionDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        static void UpdateTraitDefinitions()
        {
            s_TraitDefinitions = AssetDatabase.FindAssets($"t: {nameof(TraitDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<TraitDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        static void UpdatePlanDefinitions()
        {
            s_PlanDefinitions = AssetDatabase.FindAssets($"t: {nameof(PlanDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<PlanDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        static void UpdateTerminationDefinitions()
        {
            s_StateTerminationDefinitions = AssetDatabase.FindAssets($"t: {nameof(StateTerminationDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<StateTerminationDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        public static string GetBuiltinModuleName(UnityEngine.Object obj)
        {
            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
                return null;

            var directoryName = Path.GetDirectoryName(assetPath);
            if (directoryName != null && directoryName.Contains(s_BuiltinModulePath))
            {
                var index = directoryName.LastIndexOf(Path.DirectorySeparatorChar) + 1;
                return directoryName.Substring(index);
            }

            return null;
        }

        public static string GetBuiltinModuleName(string @namespace)
        {
            if (@namespace == null)
            {
                return null;
            }

            if (!@namespace.StartsWith(TypeResolver.PlannerAssemblyName))
            {
                return null;
            }

            return @namespace.Substring(TypeResolver.PlannerAssemblyName.Length + 1);
        }
    }
}
