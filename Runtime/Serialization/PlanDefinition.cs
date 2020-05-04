#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [HelpURL(Help.BaseURL + "/manual/PlanDefinition.html")]
    [CreateAssetMenu(fileName = "New Plan", menuName = "AI/Planner/Plan Definition")]
    class PlanDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

#pragma warning disable 0649
        [SerializeField]
        List<ActionDefinition> m_ActionDefinitions;

        [SerializeField]
        List<StateTerminationDefinition> m_StateTerminationDefinitions;

        [SerializeField]
        string m_CustomHeuristic;

        [SerializeField]
        int m_DefaultHeuristicLower = -100;

        [SerializeField]
        int m_DefaultHeuristicAverage;

        [SerializeField]
        int m_DefaultHeuristicUpper = 100;

        [SerializeField]
        [Tooltip("Multiplicative factor ([0 -> 1]) for discounting future rewards")]
        [Range(0, 1)]
        public float DiscountFactor = 0.95f;
#pragma warning restore 0649

        public int DefaultHeuristicLower => m_DefaultHeuristicLower;
        public int DefaultHeuristicAverage => m_DefaultHeuristicAverage;
        public int DefaultHeuristicUpper => m_DefaultHeuristicUpper;

        internal IEnumerable<ActionDefinition> ActionDefinitions
        {
            get => m_ActionDefinitions;
            set => m_ActionDefinitions = value.ToList();
        }

        internal IEnumerable<StateTerminationDefinition> StateTerminationDefinitions
        {
            get => m_StateTerminationDefinitions;
            set => m_StateTerminationDefinitions = value.ToList();
        }

        public string CustomHeuristic
        {
            get { return m_CustomHeuristic; }
            set { m_CustomHeuristic = value; }
        }

        Dictionary<string, TraitDefinition> m_TraitDefinitions = null;

        void InitializeTraits()
        {
            m_TraitDefinitions = GetTraitsUsed().ToDictionary(t => t.Name, t => t);
        }

        internal IEnumerable<TraitDefinition> GetTraitsUsed()
        {
            var traitList = new List<TraitDefinition>();

            if (ActionDefinitions != null)
            {
                foreach (var actionDefinition in ActionDefinitions)
                {
                    if (!actionDefinition)
                        continue;

                    foreach (var param in actionDefinition.Parameters)
                    {
                        traitList.AddRange(param.RequiredTraits);
                        traitList.AddRange(param.ProhibitedTraits);
                    }

                    foreach (var param in actionDefinition.CreatedObjects)
                    {
                        traitList.AddRange(param.RequiredTraits);
                        traitList.AddRange(param.ProhibitedTraits);
                    }

                    foreach (var modifier in actionDefinition.ObjectModifiers)
                    {
                        if (modifier.IsSpecialOperator(Operation.SpecialOperators.Add)
                        || modifier.IsSpecialOperator(Operation.SpecialOperators.Remove))
                        {
                            traitList.Add(modifier.OperandB.Trait);
                        }
                    }
                }
            }

            if (StateTerminationDefinitions != null)
            {
                foreach (var stateTerminationDefinition in StateTerminationDefinitions)
                {
                    if (!stateTerminationDefinition)
                        continue;

                    foreach (var param in stateTerminationDefinition.Parameters)
                    {
                        traitList.AddRange(param.RequiredTraits);
                        traitList.AddRange(param.ProhibitedTraits);
                    }
                }
            }

            return traitList.Distinct();
        }

        internal TraitDefinition GetTrait(string traitName)
        {
            if (m_TraitDefinitions == null)
            {
                InitializeTraits();
            }

            return !m_TraitDefinitions.ContainsKey(traitName) ? null : m_TraitDefinitions[traitName];
        }
    }
}
#endif
