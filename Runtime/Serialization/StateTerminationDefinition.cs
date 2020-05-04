#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [HelpURL(Help.BaseURL + "/manual/TerminationDefinition.html")]
    [CreateAssetMenu(fileName = "New Termination", menuName = "AI/Planner/State Termination Definition")]
    class StateTerminationDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        public IEnumerable<ParameterDefinition> Parameters
        {
            get => m_Parameters;
            set => m_Parameters = value.ToList();
        }

        public IEnumerable<Operation> Criteria
        {
            get => m_Criteria;
            set => m_Criteria = value.ToList();
        }

        public IEnumerable<CustomRewardData> CustomRewards
        {
            get => m_CustomTerminalRewards;
            set => m_CustomTerminalRewards = value.ToList();
        }

        public float TerminalReward
        {
            get => m_TerminalReward;
        }

#pragma warning disable 0649
        [SerializeField]
        List<ParameterDefinition> m_Parameters = new List<ParameterDefinition>();

        [SerializeField]
        List<Operation> m_Criteria = new List<Operation>();

        [SerializeField]
        float m_TerminalReward;

        [SerializeField]
        List<CustomRewardData> m_CustomTerminalRewards = new List<CustomRewardData>();
#pragma warning restore 0649
    }
}
#endif
