#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class DomainObjectData : IDomainObjectData
    {
        public string Name => m_Name;

        public GameObject SourceObject
        {
            get => m_SourceObject;
            set => m_SourceObject = value;
        }

        public IEnumerable<TraitObjectData> TraitData
        {
            get => m_TraitData;
            set => m_TraitData = value.ToList();
        }

#pragma warning disable 0649
        [SerializeField]
        string m_Name;

        [SerializeField]
        List<TraitObjectData> m_TraitData;
#pragma warning restore 0649

        GameObject m_SourceObject;

        public void InitializeTraitData()
        {
            foreach (var traitObjectData in m_TraitData)
            {
                traitObjectData.InitializeFieldValues();
            }
        }
    }
}
#endif
