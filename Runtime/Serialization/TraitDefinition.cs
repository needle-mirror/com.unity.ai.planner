#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;
using UnityEngine;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Definition of a trait that can be used to specify the quality of an object
    /// </summary>
    [Serializable]
    [HelpURL(Help.BaseURL + "/manual/DomainDefinition.html")]
    [CreateAssetMenu(fileName = "New Trait", menuName = "AI/Trait/Trait Definition")]
    public class TraitDefinition : ScriptableObject
    {
        const int k_DefaultUniqueId = 100;

        internal string Name
        {
            get
            {
                if (string.IsNullOrEmpty(m_CachedName))
                {
                    m_CachedName = name;
                    m_CachedResolvedName = TypeResolver.ToTypeNameCase(m_CachedName);
                    return m_CachedResolvedName;
                }

                return string.IsNullOrEmpty(m_CachedResolvedName) ?
                    m_CachedResolvedName = TypeResolver.ToTypeNameCase(name)
                    : m_CachedResolvedName;
            }
        }

        string m_CachedName;
        string m_CachedResolvedName;

        /// <summary>
        /// Fully qualified name of the trait type
        /// </summary>
        public string FullyQualifiedName => string.IsNullOrEmpty(m_Namespace)
            ? $"{TypeResolver.StateRepresentationQualifier}.{Name}"
            : $"{m_Namespace}.{Name}";

        internal List<TraitDefinitionField> Fields
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

        /// <summary>
        /// Returns the name of the trait definition
        /// </summary>
        /// <returns>A string that represents the trait definition</returns>
        public override string ToString()
        {
            return Name;
        }

        internal TraitDefinitionField GetField(int fieldId)
        {
            return Fields.FirstOrDefault(f => f.UniqueId == fieldId);
        }

        internal string GetFieldName(int fieldId)
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
