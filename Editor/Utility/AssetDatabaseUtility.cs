using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.AI.Planner.Utility
{
    static class AssetDatabaseUtility
    {
        class SingleAssetSaver : AssetModificationProcessor
        {
            public static string singleAssetPath;

            static string[] OnWillSaveAssets(string[] paths)
            {
                if (!string.IsNullOrEmpty(singleAssetPath))
                {
                    if (paths.Contains(singleAssetPath))
                        return new[] { singleAssetPath };
                }

                return paths;
            }
        }

        public static void SaveSingleAsset(UnityObject target)
        {
            SingleAssetSaver.singleAssetPath = AssetDatabase.GetAssetPath(target);
            AssetDatabase.SaveAssets();
            SingleAssetSaver.singleAssetPath = null;
        }

        public static bool IsEditable(string assetPath)
        {
            var attributes = File.GetAttributes(assetPath);

            return AssetDatabase.IsOpenForEdit(assetPath, StatusQueryOptions.ForceUpdate)
                && (attributes & FileAttributes.ReadOnly) == 0;
        }

        public static string GUIDFromObject(UnityObject @object)
        {
            var assetPath = AssetDatabase.GetAssetPath(@object);
            return AssetDatabase.AssetPathToGUID(assetPath);
        }
    }
}

