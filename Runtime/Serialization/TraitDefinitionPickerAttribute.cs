#if !UNITY_DOTSPLAYER
using System;
using UnityEngine;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    class TraitDefinitionPickerAttribute : PropertyAttribute
    {
        public bool ShowLabel => m_ShowLabel;

        bool m_ShowLabel;

        public TraitDefinitionPickerAttribute(bool showLabel = false)
        {
            m_ShowLabel = showLabel;
        }
    }
}
#endif
