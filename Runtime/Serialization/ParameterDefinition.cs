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
        string m_Name = "obj";

        [TraitDefinitionPicker]
        [SerializeField]
        List<TraitDefinition> m_RequiredTraits = new List<TraitDefinition>();

        [TraitDefinitionPicker]
        [SerializeField]
        List<TraitDefinition> m_ProhibitedTraits = new List<TraitDefinition>();
    }
}
#endif
