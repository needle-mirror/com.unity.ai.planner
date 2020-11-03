#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Planner;
using Unity.Semantic.Traits.Utility;
using UnityEditor;
using UnityEngine.Serialization;
using Unity.AI.Planner.Traits;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class OldActionDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        public IEnumerable<OldParameterDefinition> Parameters
        {
            get => m_Parameters;
            set => m_Parameters = value.ToList();
        }

        public IEnumerable<OldOperation> Preconditions
        {
            get => m_Preconditions;
        }

        public IEnumerable<OldParameterDefinition> CreatedObjects
        {
            get => m_CreatedObjects;
            set => m_CreatedObjects = value.ToList();
        }

        public IEnumerable<OldOperation> ObjectModifiers
        {
            get => m_ObjectModifiers;
        }

        public IEnumerable<string> RemovedObjects
        {
            get => m_RemovedObjects;
        }

        public float Reward
        {
            get => m_Reward;
        }

        public IEnumerable<CustomRewardData> CustomRewards
        {
            get => m_CustomRewards;
            set => m_CustomRewards = value.ToList();
        }

#pragma warning disable 0649
        [SerializeField]
        List<OldParameterDefinition> m_Parameters = new List<OldParameterDefinition>();

        [SerializeField]
        List<OldOperation> m_Preconditions = new List<OldOperation>();

        [SerializeField]
        List<OldParameterDefinition> m_CreatedObjects = new List<OldParameterDefinition>();

        [SerializeField]
        List<string> m_RemovedObjects = new List<string>();

        [FormerlySerializedAs("m_Effects")]
        [SerializeField]
        List<OldOperation> m_ObjectModifiers = new List<OldOperation>();

        [SerializeField]
        float m_Reward;

        [SerializeField]
        List<CustomRewardData> m_CustomRewards;
#pragma warning restore 0649

#if UNITY_EDITOR
        void OnValidate()
        {
            foreach (var param in m_Parameters)
            {
                param.OnValidate();
            }
        }

        internal ActionDefinition GetNewDefinition()
        {
            var oldPath = AssetDatabase.GetAssetPath(this);
            var newDir = Path.Combine(Path.GetDirectoryName(oldPath), "New");
            var newDefinition = AssetDatabase.LoadAssetAtPath<ActionDefinition>(Path.Combine(newDir, Path.GetFileName(oldPath)));
            if (newDefinition == null)
                Debug.LogError($"Missing new ActionDefinition for {name}");
            else
                return newDefinition;

            return null;
        }

        [ContextMenu("Convert")]
        void Convert()
        {
            var definition = CreateInstance<ActionDefinition>();
            definition.Parameters = m_Parameters.Select(p => p.GetNewDefinition());
            definition.Preconditions = m_Preconditions.Select(o => o.GetNewOperation());
            definition.CreatedObjects = m_CreatedObjects.Select(p => p.GetNewDefinition());
            definition.ObjectModifiers = m_ObjectModifiers.Select(o => o.GetNewOperation());
            definition.RemovedObjects = m_RemovedObjects;
            definition.Reward = m_Reward;
            definition.CustomRewards = m_CustomRewards;

            var assetPath = AssetDatabase.GetAssetPath(this);
            var directory = Path.Combine(Path.GetDirectoryName(assetPath), "New");
            var fileName = Path.GetFileName(assetPath);

            var newPath = Path.Combine(directory, fileName);
            Directory.CreateDirectory(directory);
            AssetDatabase.CreateAsset(definition, newPath);
        }

        const string k_BuildMenuTitle = "AI/Planner/Upgrader/3. Convert Old Action Definitions to New";
        [MenuItem(k_BuildMenuTitle, true)]
        public static bool ConvertAllValidate()
        {
            return !EditorApplication.isCompiling;
        }

        [MenuItem(k_BuildMenuTitle, priority = 3)]
        static void ConvertAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var oldDefinition in ConversionUtility.AllAssetsOfType<OldActionDefinition>())
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
