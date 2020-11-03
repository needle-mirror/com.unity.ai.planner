using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.AI.Planner
{
    static class ConversionUtility
    {
        public static IEnumerable<T> AllAssetsOfType<T>() where T : UnityObject
        {
            var assets = new List<T>();
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).FullName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset)
                    assets.Add(asset);
            }

            return assets;
        }
    }
}
