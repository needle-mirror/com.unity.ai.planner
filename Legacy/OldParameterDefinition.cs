#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Semantic.Traits;
using Unity.AI.Planner.Traits;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class OldParameterDefinition
    {
        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        internal IEnumerable<OldTraitDefinition> RequiredTraits
        {
            get => m_RequiredTraits;
            set => m_RequiredTraits = value.ToList();
        }

        internal IEnumerable<OldTraitDefinition> ProhibitedTraits
        {
            get => m_ProhibitedTraits;
            set => m_ProhibitedTraits = value.ToList();
        }

        public int LimitCount
        {
            get { return m_LimitCount; }
            set { m_LimitCount = value; }
        }

        public string LimitComparerType
        {
            get { return m_LimitComparerType; }
            set { m_LimitComparerType = value; }
        }

        public string LimitComparerReference
        {
            get { return m_LimitComparerReference; }
            set { m_LimitComparerReference = value; }
        }

        [SerializeField]
        string m_Name = "parameter";

        [SerializeField]
        List<OldTraitDefinition> m_RequiredTraits = new List<OldTraitDefinition>();

        [SerializeField]
        List<OldTraitDefinition> m_ProhibitedTraits = new List<OldTraitDefinition>();

        [SerializeField]
        int m_LimitCount;

        [SerializeField]
        string m_LimitComparerType;

        [SerializeField]
        string m_LimitComparerReference;

        internal ParameterDefinition GetNewDefinition()
        {
            return new ParameterDefinition()
            {
                Name = m_Name,
                LimitCount = m_LimitCount,
                ProhibitedTraits = m_ProhibitedTraits.Select(t => t.GetNewDefinition()),
                RequiredTraits = m_RequiredTraits.Select(t => t.GetNewDefinition()),
                LimitComparerReference = m_LimitComparerReference,
                LimitComparerType = m_LimitComparerType
            };
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            // Remove references of traits that don't exist anymore
            for (var i = m_RequiredTraits.Count - 1; i >= 0; i--)
            {
                if (m_RequiredTraits[i] == null)
                {
                    m_RequiredTraits.RemoveAt(i);
                }
            }

            for (var i = m_ProhibitedTraits.Count - 1; i >= 0; i--)
            {
                if (m_ProhibitedTraits[i] == null)
                {
                    m_ProhibitedTraits.RemoveAt(i);
                }
            }
        }
#endif
    }
}
#endif
