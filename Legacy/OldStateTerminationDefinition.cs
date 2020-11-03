#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Planner;
using Unity.Semantic.Traits.Utility;
using UnityEditor;
using Unity.AI.Planner.Traits;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class OldStateTerminationDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        public IEnumerable<OldParameterDefinition> Parameters
        {
            get => m_Parameters;
            set => m_Parameters = value.ToList();
        }

        public IEnumerable<OldOperation> Criteria
        {
            get => m_Criteria;
            set => m_Criteria = value.ToList();
        }

        public IEnumerable<CustomRewardData> CustomRewards
        {
            get => m_CustomTerminalRewards;
            set => m_CustomTerminalRewards = value.ToList();
        }

        public float TerminalReward
        {
            get => m_TerminalReward;
        }

#pragma warning disable 0649
        [SerializeField]
        List<OldParameterDefinition> m_Parameters = new List<OldParameterDefinition>();

        [SerializeField]
        List<OldOperation> m_Criteria = new List<OldOperation>();

        [SerializeField]
        float m_TerminalReward;

        [SerializeField]
        List<CustomRewardData> m_CustomTerminalRewards = new List<CustomRewardData>();
#pragma warning restore 0649

        internal StateTerminationDefinition GetNewDefinition()
        {
            var oldPath = AssetDatabase.GetAssetPath(this);
            var newDir = Path.Combine(Path.GetDirectoryName(oldPath), "New");
            var newDefinition = AssetDatabase.LoadAssetAtPath<StateTerminationDefinition>(Path.Combine(newDir, Path.GetFileName(oldPath)));
            if (newDefinition == null)
                Debug.LogError($"Missing new StateTerminationDefinition for {name}");
            else
                return newDefinition;

            return null;
        }

        [ContextMenu("Convert")]
        void Convert()
        {
            var definition = CreateInstance<StateTerminationDefinition>();
            definition.Parameters = m_Parameters.Select(p => p.GetNewDefinition());
            definition.Criteria = m_Criteria.Select(c => c.GetNewOperation());
            definition.CustomRewards = m_CustomTerminalRewards;
            definition.TerminalReward = m_TerminalReward;

            var assetPath = AssetDatabase.GetAssetPath(this);
            var directory = Path.Combine(Path.GetDirectoryName(assetPath), "New");
            var fileName = Path.GetFileName(assetPath);

            var newPath = Path.Combine(directory, fileName);
            Directory.CreateDirectory(directory);
            AssetDatabase.CreateAsset(definition, newPath);
        }

        const string k_BuildMenuTitle = "AI/Planner/Upgrader/4. Convert Old State Termination Definitions to New";
        [MenuItem(k_BuildMenuTitle, true)]
        public static bool ConvertAllValidate()
        {
            return !EditorApplication.isCompiling;
        }

        [MenuItem(k_BuildMenuTitle, priority = 4)]
        static void ConvertAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var oldDefinition in ConversionUtility.AllAssetsOfType<OldStateTerminationDefinition>())
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
    }
}
#endif
