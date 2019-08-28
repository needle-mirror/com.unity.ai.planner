#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class Operation
    {
        public IEnumerable<string> OperandA
        {
            get => m_OperandA;
            set => m_OperandA = value.ToList();
        }

        public string Operator
        {
            get => m_Operator;
            set => m_Operator = value;
        }

        public IEnumerable<string> OperandB
        {
            get => m_OperandB;
            set => m_OperandB = value.ToList();
        }

        [SerializeField]
        List<string> m_OperandA;

        [SerializeField]
        string m_Operator;

        [SerializeField]
        List<string> m_OperandB;
    }
}
#endif
