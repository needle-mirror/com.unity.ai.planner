#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;
using UnityEngine;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [CreateAssetMenu(fileName = "New Trait", menuName = "AI/Planner/Trait Definition")]
    class TraitDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        public IEnumerable<TraitDefinitionField> Fields
        {
            get => m_Fields;
            set => m_Fields = value.ToList();
        }

        [SerializeField]
        List<TraitDefinitionField> m_Fields = new List<TraitDefinitionField>();

        public override string ToString()
        {
            return Name;
        }

        public TraitDefinitionField GetField(string fieldName)
        {
            return m_Fields.FirstOrDefault((f) => f.Name == fieldName);
        }
    }
}
#endif
