#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.Utility;
using Unity.Semantic.Traits;
using Unity.DynamicStructs;
using Unity.Semantic.Traits.Utility;
using UnityEditor;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class OldEnumDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        public IEnumerable<string> Values
        {
            get => m_Values;
            set => m_Values = value.ToList();
        }

        [SerializeField]
        List<string> m_Values = default;

#if UNITY_EDITOR
        internal EnumDefinition GetNewDefinition()
        {
            var oldPath = AssetDatabase.GetAssetPath(this);
            var newDir = Path.Combine(Path.GetDirectoryName(oldPath), "New");
            var newDefinition = AssetDatabase.LoadAssetAtPath<EnumDefinition>(Path.Combine(newDir, Path.GetFileName(oldPath)));
            if (newDefinition == null)
                Debug.LogError($"Missing new EnumDefinition for {name}");
            else
                return newDefinition;

            return null;
        }

        [ContextMenu("Convert")]
        void Convert()
        {
            var definition = DynamicStruct.Create<EnumDefinition>();

            // Have to save the asset before adding properties to it
            var assetPath = AssetDatabase.GetAssetPath(this);
            var directory = Path.Combine(Path.GetDirectoryName(assetPath), "New");
            var fileName = Path.GetFileName(assetPath);

            var newPath = Path.Combine(directory, fileName);
            Directory.CreateDirectory(directory);
            AssetDatabase.CreateAsset(definition, newPath);

            foreach (var field in m_Values)
            {
                definition.CreateProperty<string>(field);
            }
        }

        const string k_BuildMenuTitle = "AI/Planner/Upgrader/1. Convert Old Enums to New";
        [MenuItem(k_BuildMenuTitle, true)]
        public static bool ConvertAllValidate()
        {
            return !EditorApplication.isCompiling;
        }

        [MenuItem(k_BuildMenuTitle, priority = 1)]
        static void ConvertAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var oldDefinition in ConversionUtility.AllAssetsOfType<OldEnumDefinition>())
                {
                    oldDefinition.Convert();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
#endif
    }
}
#endif
