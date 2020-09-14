#if !UNITY_DOTSPLAYER
using Unity.Semantic.Traits;
using System;
using Unity.DynamicStructs;
using UnityEngine;

namespace Unity.AI.Planner.Traits
{
    [Serializable]
    class OperandValue
    {
        [SerializeField]
        string m_Parameter;

        [SerializeField]
        TraitDefinition m_Trait;

        [SerializeField]
        PropertyDefinition m_TraitProperty;

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

        public string TraitPropertyName => m_TraitProperty ? m_TraitProperty.property_name : string.Empty;

        public PropertyDefinition TraitProperty
        {
            get => m_TraitProperty;
            set => m_TraitProperty = value;
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
            m_TraitProperty = null;
        }

        public override string ToString()
        {
            if (m_Enum != null)
                return $"{m_Enum.name}.{m_Value}";

            if (m_Trait != null)
                return TraitPropertyName == null ?
                    $"{m_Parameter}.{m_Trait.name}" :
                    $"{m_Parameter}.{m_Trait.name}.{TraitPropertyName}";

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
            set => m_OperandA = value;
        }

        public string Operator
        {
            get => m_Operator;
            set => m_Operator = value;
        }

        public OperandValue OperandB
        {
            get => m_OperandB;
            set => m_OperandB = value;
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
