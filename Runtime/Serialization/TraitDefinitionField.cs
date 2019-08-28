#if !UNITY_DOTSPLAYER
using System;
using Unity.AI.Planner.Utility;
using UnityEngine;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    internal class TraitDefinitionField
    {
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

        internal bool HiddenField
        {
            get => m_HiddenField;
            set => m_HiddenField = value;
        }

        public Type FieldType
        {
            get
            {
                if (m_FieldType == null)
                    m_FieldType = TypeResolver.GetType(Type);

                return m_FieldType;
            }
            set
            {
                m_FieldType = value;
                Type = value.FullName;
            }
        }

        [SerializeField]
        string m_Name;

        [SerializeField]
        string m_Type;

        [SerializeField]
        FieldValue m_DefaultValue;

        [SerializeField]
        bool m_HiddenField;

        Type m_FieldType;
    }
}
#endif
