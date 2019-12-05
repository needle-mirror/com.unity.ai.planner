#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;
using UnityEngine.Serialization;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [HelpURL(Help.BaseURL + "/manual/ActionDefinition.html")]
    [CreateAssetMenu(fileName = "New Action", menuName = "AI/Planner/Action Definition")]
    class ActionDefinition : ScriptableObject
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
        }

        public IEnumerable<ParameterDefinition> CreatedObjects
        {
            get => m_CreatedObjects;
        }

        public IEnumerable<Operation> ObjectModifiers
        {
            get => m_ObjectModifiers;
        }

        public IEnumerable<string> RemovedObjects
        {
            get => m_RemovedObjects;
        }

        public float Reward
        {
            get => m_Reward;
        }

        public IEnumerable<CustomRewardData> CustomRewards
        {
            get => m_CustomRewards;
            set => m_CustomRewards = value.ToList();
        }

#pragma warning disable 0649
        [SerializeField]
        List<ParameterDefinition> m_Parameters = new List<ParameterDefinition>();

        [SerializeField]
        List<Operation> m_Preconditions = new List<Operation>();

        [SerializeField]
        List<ParameterDefinition> m_CreatedObjects = new List<ParameterDefinition>();

        [SerializeField]
        List<string> m_RemovedObjects = new List<string>();

        [FormerlySerializedAs("m_Effects")]
        [SerializeField]
        List<Operation> m_ObjectModifiers = new List<Operation>();

        [SerializeField]
        float m_Reward;

        [SerializeField]
        List<CustomRewardData> m_CustomRewards;
#pragma warning restore 0649

#if UNITY_EDITOR
        void OnValidate()
        {
            foreach (var param in m_Parameters)
            {
                param.OnValidate();
            }
        }
#endif
    }

    [Serializable]
    class CustomRewardData
    {
        public string Operator => m_Operator;
        public string Typename => m_Typename;
        public string[] Parameters => m_Parameters;

#pragma warning disable 0649
        [SerializeField]
        string m_Operator;

        [SerializeField]
        string m_Typename;

        [SerializeField]
        string[] m_Parameters;
#pragma warning restore 0649
    }
}
#endif
