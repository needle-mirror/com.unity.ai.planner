#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class ParameterDefinition
    {
        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        internal IEnumerable<TraitDefinition> RequiredTraits
        {
            get => m_RequiredTraits;
            set => m_RequiredTraits = value.ToList();
        }

        internal IEnumerable<TraitDefinition> ProhibitedTraits
        {
            get => m_ProhibitedTraits;
            set => m_ProhibitedTraits = value.ToList();
        }

        [SerializeField]
        string m_Name = "parameter";

        [SerializeField]
        List<TraitDefinition> m_RequiredTraits = new List<TraitDefinition>();

        [SerializeField]
        List<TraitDefinition> m_ProhibitedTraits = new List<TraitDefinition>();

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
