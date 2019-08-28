#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [CreateAssetMenu(fileName = "New Termination", menuName = "AI/Planner/State Termination Definition")]
    class StateTerminationDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        public ParameterDefinition ObjectParameters
        {
            get => m_ObjectParameters;
            set => m_ObjectParameters = value;
        }

        public IEnumerable<Operation> Criteria
        {
            get => m_Criteria;
            set => m_Criteria = value.ToList();
        }

        public string Namespace
        {
            get => m_Namespace;
            set => m_Namespace = value;
        }

        [SerializeField]
        string m_Namespace;

        [SerializeField]
        ParameterDefinition m_ObjectParameters;

        [SerializeField]
        List<Operation> m_Criteria = new List<Operation>();
    }
}
#endif
