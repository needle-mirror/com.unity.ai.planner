#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.Traits;
using Unity.Semantic.Traits.Utility;
using UnityEditor;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.AI.Planner.Traits;
using UnityEngine.Serialization;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class OldPlanDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

#pragma warning disable 0649
        [SerializeField]
        List<OldActionDefinition> m_ActionDefinitions;

        [SerializeField]
        List<OldStateTerminationDefinition> m_StateTerminationDefinitions;

        [SerializeField]
        string m_CustomCumulativeRewardEstimator;

        [FormerlySerializedAs("m_DefaultHeuristicLower")]
        [SerializeField]
        int m_DefaultEstimateLower = -100;

        [FormerlySerializedAs("m_DefaultHeuristicAverage")]
        [SerializeField]
        int m_DefaultEstimateAverage;

        [FormerlySerializedAs("m_DefaultHeuristicUpper")]
        [SerializeField]
        int m_DefaultEstimateUpper = 100;

        [SerializeField]
        [Tooltip("Multiplicative factor ([0 -> 1]) for discounting future rewards")]
        [Range(0, 1)]
        public float DiscountFactor = 0.95f;
#pragma warning restore 0649

        public int defaultEstimateLower => m_DefaultEstimateLower;
        public int defaultEstimateAverage => m_DefaultEstimateAverage;
        public int defaultEstimateUpper => m_DefaultEstimateUpper;

        public IEnumerable<OldActionDefinition> ActionDefinitions
        {
            get => m_ActionDefinitions;
            set => m_ActionDefinitions = value.ToList();
        }

        public IEnumerable<OldStateTerminationDefinition> StateTerminationDefinitions
        {
            get => m_StateTerminationDefinitions;
            set => m_StateTerminationDefinitions = value.ToList();
        }

        public string CustomCumulativeRewardEstimator
        {
            get { return m_CustomCumulativeRewardEstimator; }
            set { m_CustomCumulativeRewardEstimator = value; }
        }

        Dictionary<string, OldTraitDefinition> m_TraitDefinitions = null;

        void InitializeTraits()
        {
            m_TraitDefinitions = GetTraitsUsed().ToDictionary(t => t.name, t => t);
        }

        internal IEnumerable<OldTraitDefinition> GetTraitsUsed()
        {
            var traitList = new List<OldTraitDefinition>();

            if (ActionDefinitions != null)
            {
                foreach (var actionDefinition in ActionDefinitions)
                {
                    if (!actionDefinition)
                        continue;

                    foreach (var param in actionDefinition.Parameters)
                    {
                        traitList.AddRange(param.RequiredTraits);
                        traitList.AddRange(param.ProhibitedTraits);
                    }

                    foreach (var param in actionDefinition.CreatedObjects)
                    {
                        traitList.AddRange(param.RequiredTraits);
                        traitList.AddRange(param.ProhibitedTraits);
                    }
                }
            }

            if (StateTerminationDefinitions != null)
            {
                foreach (var stateTerminationDefinition in StateTerminationDefinitions)
                {
                    if (!stateTerminationDefinition)
                        continue;

                    foreach (var param in stateTerminationDefinition.Parameters)
                    {
                        traitList.AddRange(param.RequiredTraits);
                        traitList.AddRange(param.ProhibitedTraits);
                    }
                }
            }

            return traitList.Distinct();
        }

        public OldTraitDefinition GetTrait(string traitName)
        {
            if (m_TraitDefinitions == null)
            {
                InitializeTraits();
            }

            return !m_TraitDefinitions.ContainsKey(traitName) ? null : m_TraitDefinitions[traitName];
        }

        [ContextMenu("Convert")]
        void Convert()
        {
            var definition = CreateInstance<ProblemDefinition>();
            definition.ActionDefinitions = m_ActionDefinitions.Select(a => a.GetNewDefinition());
            definition.StateTerminationDefinitions = m_StateTerminationDefinitions.Select(sd => sd.GetNewDefinition());
            definition.CustomCumulativeRewardEstimator = m_CustomCumulativeRewardEstimator;
            definition.DefaultEstimateAverage = m_DefaultEstimateAverage;
            definition.DefaultEstimateLower = m_DefaultEstimateLower;
            definition.DefaultEstimateUpper = m_DefaultEstimateUpper;
            definition.DiscountFactor = DiscountFactor;

            var assetPath = AssetDatabase.GetAssetPath(this);
            var directory = Path.Combine(Path.GetDirectoryName(assetPath), "New");
            var fileName = Path.GetFileName(assetPath);

            var newPath = Path.Combine(directory, fileName);
            Directory.CreateDirectory(directory);
            AssetDatabase.CreateAsset(definition, newPath);
        }

        const string k_BuildMenuTitle = "AI/Planner/Upgrader/5. Convert Old Plan Definitions to New";
        [MenuItem(k_BuildMenuTitle, true)]
        public static bool ConvertAllValidate()
        {
            return !EditorApplication.isCompiling;
        }

        [MenuItem(k_BuildMenuTitle, priority = 5)]
        static void ConvertAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var oldDefinition in ConversionUtility.AllAssetsOfType<OldPlanDefinition>())
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
