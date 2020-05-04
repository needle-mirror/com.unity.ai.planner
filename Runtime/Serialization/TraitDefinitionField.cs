#if !UNITY_DOTSPLAYER
using System;
using Unity.AI.Planner.Utility;
using UnityEngine;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class TraitDefinitionField
    {
        internal enum FieldRestriction
        {
            None,
            NotInitializable,
            NotSelectable,
        }

        internal int UniqueId
        {
            get => m_UniqueId;
            set => m_UniqueId = value;
        }

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string Type
        {
            get => m_Type;
            set => m_Type = value;
        }

        internal FieldValue DefaultValue
        {
            get => m_DefaultValue;
            set => m_DefaultValue = value;
        }

        internal FieldRestriction Restriction
        {
            get => m_Restriction;
            set => m_Restriction = value;
        }

        public Type FieldType
        {
            get
            {
                if (m_FieldType == null)
                    TypeResolver.TryGetType(Type, out m_FieldType);

                return m_FieldType;
            }
            set
            {
                m_FieldType = value;
                Type = value.FullName;
            }
        }

        [SerializeField]
        int m_UniqueId;

        [SerializeField]
        string m_Name;

        [SerializeField]
        string m_Type;

        [SerializeField]
        FieldValue m_DefaultValue;

        [SerializeField]
        FieldRestriction m_Restriction;

        Type m_FieldType;
    }
}
#endif
