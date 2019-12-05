#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [HelpURL(Help.BaseURL + "/manual/DomainDefinition.html")]
    [CreateAssetMenu(fileName = "New Enum", menuName = "AI/Trait/Enum Definition")]
    class EnumDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        public IEnumerable<string> Values
        {
            get => m_Values;
            set => m_Values = value.ToList();
        }

        [SerializeField]
        List<string> m_Values = default;
    }
}
#endif
