#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [CreateAssetMenu(fileName = "New Action", menuName = "AI/Planner/Action Definition")]
    internal class ActionDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        public IEnumerable<ParameterDefinition> Parameters
        {
            get => m_Parameters;
            set => m_Parameters = value.ToList();
        }

        public IEnumerable<Operation> Preconditions
        {
            get => m_Preconditions;
            set => m_Preconditions = value.ToList();
        }

        public IEnumerable<ParameterDefinition> CreatedObjects
        {
            get => m_CreatedObjects;
            set => m_CreatedObjects = value.ToList();
        }

        public IEnumerable<Operation> Effects
        {
            get => m_Effects;
            set => m_Effects = value.ToList();
        }

        public IEnumerable<string> RemovedObjects
        {
            get => m_RemovedObjects;
            set => m_RemovedObjects = value.ToList();
        }

        public float Reward
        {
            get => m_Reward;
            set => m_Reward = value;
        }

        public string OperationalActionType
        {
            get => m_OperationalActionType;
            set => m_OperationalActionType = value;
        }

        public string CustomEffect
        {
            get => m_CustomEffect;
            set => m_CustomEffect = value;
        }

        public string CustomReward
        {
            get => m_CustomReward;
            set => m_CustomReward = value;
        }

        public string CustomPrecondition
        {
            get => m_CustomPrecondition;
            set => m_CustomPrecondition = value;
        }

        [SerializeField]
        List<ParameterDefinition> m_Parameters = new List<ParameterDefinition>();

        [SerializeField]
        List<Operation> m_Preconditions = new List<Operation>();

        [SerializeField]
        List<ParameterDefinition> m_CreatedObjects = new List<ParameterDefinition>();

        [SerializeField]
        List<string> m_RemovedObjects = new List<string>();

        [SerializeField]
        List<Operation> m_Effects = new List<Operation>();

        [SerializeField]
        float m_Reward;

        [SerializeField]
        string m_OperationalActionType;

        [SerializeField]
        string m_CustomEffect;

        [SerializeField]
        string m_CustomReward;

        [SerializeField]
        string m_CustomPrecondition;
    }
}
#endif
