#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;
using UnityEngine;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [HelpURL(Help.BaseURL + "/manual/DomainDefinition.html")]
    [CreateAssetMenu(fileName = "New Trait", menuName = "AI/Trait/Trait Definition")]
    class TraitDefinition : ScriptableObject
    {
        const int k_DefaultUniqueId = 100;

        public string Name
        {
            get
            {
                if (m_CachedName == null)
                {
                    m_CachedName = name;
                    m_CachedResolvedName = TypeResolver.ToTypeNameCase(m_CachedName);
                    return m_CachedResolvedName;
                }

                if (name.Equals(m_CachedName))
                    return m_CachedResolvedName ?? (m_CachedResolvedName = TypeResolver.ToTypeNameCase(name));

                m_CachedName = name;
                m_CachedResolvedName = TypeResolver.ToTypeNameCase(m_CachedName);
                return m_CachedResolvedName;
            }
        }

        string m_CachedName;
        string m_CachedResolvedName;

        public string FullyQualifiedName => string.IsNullOrEmpty(m_Namespace)
            ? $"{TypeResolver.DomainsNamespace}.{Name}"
            : $"{m_Namespace}.{Name}";

        public IEnumerable<TraitDefinitionField> Fields
        {
            get => m_Fields;
            set => m_Fields = value.ToList();
        }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        [SerializeField]
        string m_Namespace;
#pragma warning restore CS0649

        [SerializeField]
        List<TraitDefinitionField> m_Fields = new List<TraitDefinitionField>();

        [SerializeField]
        int m_NextFieldUniqueId = k_DefaultUniqueId;

        public override string ToString()
        {
            return Name;
        }

        public TraitDefinitionField GetField(int fieldId)
        {
            return Fields.FirstOrDefault(f => f.UniqueId == fieldId);
        }

        public string GetFieldName(int fieldId)
        {
            return Fields.FirstOrDefault(f => f.UniqueId == fieldId)?.Name;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Validate that field's unique Id are set correctly
            var usedId = new List<int>();
            foreach (var field in m_Fields)
            {
                if (field.UniqueId >= m_NextFieldUniqueId)
                {
                    m_NextFieldUniqueId = field.UniqueId + 1;
                }
                else if (field.UniqueId == 0)
                {
                    field.UniqueId = m_NextFieldUniqueId++;
                }

                if (usedId.Contains(field.UniqueId))
                {
                    field.UniqueId = m_NextFieldUniqueId++;
                }

                usedId.Add(field.UniqueId);
            }
        }
#endif
    }
}
#endif
