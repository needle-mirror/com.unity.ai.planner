using System.Linq;
#if !UNITY_DOTSPLAYER
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;
using System;
using System.Collections.Generic;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class TraitBasedObjectData : ITraitBasedObjectData
    {
        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public object ParentObject => m_GameObject;

        public IEnumerable<ITraitData> TraitData
        {
            get => m_TraitData;
        }

#pragma warning disable 0649
        [SerializeField]
        string m_Name;

        [SerializeField]
        List<TraitData> m_TraitData = new List<TraitData>();
#pragma warning restore 0649

        Dictionary<Type, ITraitData> m_TraitDataByType = new Dictionary<Type, ITraitData>();
        GameObject m_GameObject;

        internal void Initialize(GameObject gameObject)
        {
            m_GameObject = gameObject;

            m_TraitDataByType.Clear();
            foreach (var traitObjectData in m_TraitData)
            {
                if (traitObjectData == null || traitObjectData.TraitDefinition == null)
                    continue;

                var traitType = TypeResolver.GetType(traitObjectData.TraitDefinition.FullyQualifiedName);
                if (traitType != null)
                {
                    traitObjectData.InitializeFieldValues();
                    m_TraitDataByType.Add(traitType, traitObjectData);
                }
            }
        }

        public ITraitData GetTraitData<TTrait>() where TTrait : ITrait
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return TraitData.FirstOrDefault(t => t.TraitDefinitionName == typeof(TTrait).Name);
#endif
            m_TraitDataByType.TryGetValue(typeof(TTrait), out var objectData);
            return objectData;
        }

#if UNITY_EDITOR
        internal void OnValidate()
        {
            m_TraitData.RemoveAll(td => td == null || td.TraitDefinition == null);
        }
#endif
    }
}
#endif
