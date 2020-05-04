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

        public IList<TraitData> TraitData => m_TraitData;

#pragma warning disable 0649
        [SerializeField]
        string m_Name;

        [SerializeField]
        List<TraitData> m_TraitData = new List<TraitData>();
#pragma warning restore 0649

        Dictionary<Type, TraitData> m_TraitDataByType = new Dictionary<Type, TraitData>();
        GameObject m_GameObject;

        internal void Initialize(GameObject gameObject)
        {
            m_GameObject = gameObject;

#if UNITY_EDITOR
            // Cached trait data is only used during play mode
            if (!Application.isPlaying)
                return;
#endif
            m_TraitDataByType.Clear();
            foreach (var traitObjectData in m_TraitData)
            {
                if (traitObjectData == null || traitObjectData.TraitDefinition == null)
                    continue;

                InitializeTraitData(traitObjectData);
            }
        }

        public void AddTraitData(TraitData data)
        {
            m_TraitData.Add(data);
            InitializeTraitData(data);
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

        public void RemoveTraitData<TTrait>() where TTrait : ITrait
        {
            m_TraitData.RemoveAll(t => t.TraitDefinitionName == typeof(TTrait).Name);
            m_TraitDataByType.Remove(typeof(TTrait));
        }

        public bool HasTraitData<TTrait>() where TTrait : ITrait
        {
            return m_TraitDataByType.ContainsKey(typeof(TTrait));
        }

        void InitializeTraitData(TraitData data)
        {
            if (TypeResolver.TryGetType(data.TraitDefinition.FullyQualifiedName, out var traitType))
            {
                data.InitializeFieldValues();
                m_TraitDataByType.Add(traitType, data);
            }
            else
            {
                Debug.LogWarning($"Trait type not found: {data.TraitDefinition.FullyQualifiedName}");
            }
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
