#if !UNITY_DOTSPLAYER
using Unity.Semantic.Traits;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Traits;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class OldOperandValue
    {
        [SerializeField]
        string m_Parameter;

        [SerializeField]
        OldTraitDefinition m_Trait;

        [SerializeField]
        int m_TraitFieldId;

        [SerializeField]
        OldEnumDefinition m_Enum;

        [SerializeField]
        string m_Value;

        public string Parameter
        {
            get => m_Parameter;
            set => m_Parameter = value;
        }

        public OldTraitDefinition Trait
        {
            get => m_Trait;
            set => m_Trait = value;
        }

        public string TraitFieldName => Trait == null ? string.Empty : m_Trait.GetFieldName(m_TraitFieldId);

        public int TraitFieldId
        {
            get => m_TraitFieldId;
            set => m_TraitFieldId = value;
        }

        public OldEnumDefinition Enum
        {
            get => m_Enum;
            set => m_Enum = value;
        }

        public string Value
        {
            get => m_Value;
            set => m_Value = value;
        }

        internal void Clear()
        {
            m_Parameter = String.Empty;
            m_Trait = null;
            m_Enum = null;
            m_Value = string.Empty;
            m_TraitFieldId = 0;
        }

        public override string ToString()
        {
            if (m_Enum != null)
                return $"{m_Enum.name}.{m_Value}";

            if (m_Trait != null)
                return TraitFieldName == null ?
                    $"{m_Parameter}.{m_Trait.name}" :
                    $"{m_Parameter}.{m_Trait.name}.{TraitFieldName}";

            if (!string.IsNullOrEmpty(m_Parameter))
                return m_Parameter;

            return Value;
        }

        internal OperandValue GetNewOperandValue()
        {
            return new OperandValue()
            {
                Enum = m_Enum ? m_Enum.GetNewDefinition() : null,
                Parameter = m_Parameter,
                Trait = m_Trait ? m_Trait.GetNewDefinition() : null,
                Value = m_Value,
                TraitPropertyId = m_Trait ? m_Trait.GetNewDefinition().Properties.FirstOrDefault(pd =>
                    pd.Name == m_Trait.GetFieldName(m_TraitFieldId)).Id : -1
            };
        }
    }

    [Serializable]
    class OldOperation
    {
        public const string AddTraitOperator = "ADD";
        public const string RemoveTraitOperator = "REMOVE";
        public const string CustomOperator = "CUSTOM";

        public OldOperandValue OperandA
        {
            get => m_OperandA;
        }

        public string Operator
        {
            get => m_Operator;
            set => m_Operator = value;
        }

        public OldOperandValue OperandB
        {
            get => m_OperandB;
        }

#pragma warning disable 0649

        [SerializeField]
        string m_Operator;

        [SerializeField]
        OldOperandValue m_OperandA;

        [SerializeField]
        OldOperandValue m_OperandB;
#pragma warning restore 0649

        internal Operation GetNewOperation()
        {
            return new Operation()
            {
                Operator = m_Operator,
                OperandA = m_OperandA.GetNewOperandValue(),
                OperandB = m_OperandB.GetNewOperandValue()
            };
        }
    }
}
#endif
