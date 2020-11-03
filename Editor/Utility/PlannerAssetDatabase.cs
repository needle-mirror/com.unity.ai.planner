using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Planner.Traits;
using Unity.AI.Planner.Utility;
using Unity.Semantic.Traits;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using TraitDefinition = Unity.Semantic.Traits.TraitDefinition;

namespace UnityEditor.AI.Planner.Utility
{
    static class PlannerAssetDatabase
    {
        static string s_BuiltinModulePath = $"com.unity.ai.planner{Path.DirectorySeparatorChar}Runtime{Path.DirectorySeparatorChar}Modules{Path.DirectorySeparatorChar}";

        const string k_PackagePath = "Packages";
        public static readonly string plansPackagePath = Path.Combine(k_PackagePath, TypeHelper.PlansQualifier.ToLower());
        public static readonly string stateRepresentationPackagePath = Path.Combine(k_PackagePath, TypeHelper.StateRepresentationQualifier.ToLower());

        public static readonly string[] stateRepresentationAssetTypeNames = {
            nameof(TraitDefinition),
            nameof(EnumDefinition),
        };

        public static readonly string[] planAssetTypeNames = {
            nameof(ActionDefinition),
            nameof(StateTerminationDefinition),
            nameof(ProblemDefinition),
        };

        static IEnumerable<TraitDefinition> s_TraitDefinitions = null;
        static IEnumerable<ActionDefinition> s_ActionDefinitions = null;
        static IEnumerable<EnumDefinition> s_EnumDefinitions = null;
        static IEnumerable<ProblemDefinition> s_ProblemDefinitions = null;
        static IEnumerable<StateTerminationDefinition> s_StateTerminationDefinitions = null;

        static string s_LastPathUsedForNewAsset;

        public static IEnumerable<TraitDefinition> TraitDefinitions => s_TraitDefinitions ?? (s_TraitDefinitions = FindAndLoadAssets<TraitDefinition>());
        public static IEnumerable<ActionDefinition> ActionDefinitions => s_ActionDefinitions ?? (s_ActionDefinitions = FindAndLoadAssets<ActionDefinition>());
        public static IEnumerable<EnumDefinition> EnumDefinitions => s_EnumDefinitions ?? (s_EnumDefinitions = FindAndLoadAssets<EnumDefinition>());
        public static IEnumerable<StateTerminationDefinition> StateTerminationDefinitions => s_StateTerminationDefinitions ?? (s_StateTerminationDefinitions = FindAndLoadAssets<StateTerminationDefinition>());
        public static IEnumerable<ProblemDefinition> ProblemDefinitions => s_ProblemDefinitions ?? (s_ProblemDefinitions = FindAndLoadAssets<ProblemDefinition>());

        public static void Refresh(string[] restrictFolders = null)
        {
            s_EnumDefinitions = FindAndLoadAssets<EnumDefinition>(restrictFolders);
            s_ActionDefinitions = FindAndLoadAssets<ActionDefinition>(restrictFolders);
            s_TraitDefinitions = FindAndLoadAssets<TraitDefinition>(restrictFolders);
            s_ProblemDefinitions = FindAndLoadAssets<ProblemDefinition>(restrictFolders);
            s_StateTerminationDefinitions = FindAndLoadAssets<StateTerminationDefinition>(restrictFolders);
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

        static IEnumerable<T> FindAndLoadAssets<T>(string[] restrictFolders = null) where T : UnityObject
        {
            var assets = AssetDatabase.FindAssets($"t: {typeof(T).FullName}", restrictFolders);
            return assets.Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null);
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

        public static bool TryFindNewerAsset(string[] assetTypes, DateTime compareTime, ref string newerAssetPath)
        {
            var filter = string.Join(" ", assetTypes.Select(t => $"t:{t}"));
            var assets = AssetDatabase.FindAssets(filter);
            foreach (var a in assets)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(a);
                var assetLastWriteTime = File.GetLastWriteTimeUtc(assetPath);
                if (assetLastWriteTime.CompareTo(compareTime) > 0)
                {
                    newerAssetPath = assetPath;
                    return true;
                }
            }

            return false;
        }

        public static string GetBuiltinModuleName(string @namespace)
        {
            if (@namespace == null)
            {
                return null;
            }

            if (!@namespace.StartsWith(TypeHelper.PlannerAssemblyName))
            {
                return null;
            }

            return @namespace.Substring(TypeHelper.PlannerAssemblyName.Length + 1);
        }

        // PackageInfo.FindForAssetPath requires a forward-slashes relative path for every platforms
        public static bool StateRepresentationPackageExists() => PackageManager.PackageInfo.FindForAssetPath($"{k_PackagePath}/{TypeHelper.StateRepresentationQualifier.ToLower()}/package.json") != null;
        public static bool PlansPackageExists() => PackageManager.PackageInfo.FindForAssetPath($"{k_PackagePath}/{TypeHelper.PlansQualifier.ToLower()}/package.json") != null;

        public static bool HasValidProblemDefinition()
        {
            foreach (var problemDefinition in ProblemDefinitions)
            {
                if (problemDefinition.ActionDefinitions != null && problemDefinition.ActionDefinitions.Any())
                    return true;
            }

            return false;
        }
    }
}
