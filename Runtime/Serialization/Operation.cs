#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class OperandValue
    {
        [SerializeField]
        string m_Parameter;

        [SerializeField]
        TraitDefinition m_Trait;

        [SerializeField]
        int m_TraitFieldId;

        [SerializeField]
        EnumDefinition m_Enum;

        [SerializeField]
        string m_Value;

        public string Parameter
        {
            get => m_Parameter;
            set => m_Parameter = value;
        }

        public TraitDefinition Trait
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

        public EnumDefinition Enum
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
                return $"{m_Enum.Name}.{m_Value}";

            if (m_Trait != null)
                return TraitFieldName == null ?
                    $"{m_Parameter}.{m_Trait.Name}" :
                    $"{m_Parameter}.{m_Trait.Name}.{TraitFieldName}";

            if (!string.IsNullOrEmpty(m_Parameter))
                return m_Parameter;

            return Value;
        }
    }

    [Serializable]
    class Operation
    {
        internal enum SpecialOperators
        {
            Add,
            Remove,
            Custom
        }

        public OperandValue OperandA
        {
            get => m_OperandA;
        }

        public string Operator
        {
            get => m_Operator;
            set => m_Operator = value;
        }

        public OperandValue OperandB
        {
            get => m_OperandB;
        }

        public string CustomOperatorType
        {
            get => m_CustomOperatorType;
            set => m_CustomOperatorType = value;
        }

#pragma warning disable 0649
        [SerializeField]
        string m_Operator;

        [SerializeField]
        string m_CustomOperatorType;

        [SerializeField]
        OperandValue m_OperandA;

        [SerializeField]
        OperandValue m_OperandB;
#pragma warning restore 0649

        internal bool IsSpecialOperator(SpecialOperators op)
        {
            return m_Operator.Equals(op.ToString());
        }
    }
}
#endif
