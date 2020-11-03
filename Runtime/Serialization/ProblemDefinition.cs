#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;
using Unity.Semantic.Traits;
using Unity.Semantic.Traits.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AI.Planner.Traits
{
    /// <summary>
    /// A ScriptableObject holding serialized data defining a planning problem.
    /// </summary>
    [Serializable]
    [HelpURL(Help.BaseURL + "/manual/ProblemDefinition.html")]
    [CreateAssetMenu(fileName = "New Problem Definition", menuName = "AI/Planner/Problem Definition")]
    public class ProblemDefinition : ScriptableObject
    {
        /// <summary>
        /// The type corresponding to the class capable of initializing the planning systems for this problem definition.
        /// Returns null if the type cannot be found, which can occur if code has not yet been generated.
        /// </summary>
        public Type SystemsProviderType =>
            TypeResolver.TryGetType($"{TypeHelper.PlansQualifier}.{Name}.PlanningSystemsProvider", out var type) ? type : null;

        internal string Name => TypeResolver.ToTypeNameCase(name);

#pragma warning disable 0649
        [SerializeField]
        List<ActionDefinition> m_ActionDefinitions;

        [SerializeField]
        List<StateTerminationDefinition> m_StateTerminationDefinitions;

        [FormerlySerializedAs("m_CustomHeuristic")]
        [SerializeField]
        string m_CustomCumulativeRewardEstimator;

        [FormerlySerializedAs("m_DefaultHeuristicLower")]
        [SerializeField]
        int m_DefaultEstimateLower = -100;

        [FormerlySerializedAs("m_DefaultHeuristicAverage")]
        [SerializeField]
        int m_DefaultEstimateAverage;

        [FormerlySerializedAs("m_DefaultHeuristicUpper")]
        [SerializeField]
        int m_DefaultEstimateUpper = 100;

        [SerializeField]
        [Tooltip("Multiplicative factor ([0 -> 1]) for discounting future rewards")]
        [Range(0, 1)]
        internal float DiscountFactor = 0.95f;
#pragma warning restore 0649

        internal int DefaultEstimateLower
        {
            get => m_DefaultEstimateLower;
            set => m_DefaultEstimateLower = value;
        }

        internal int DefaultEstimateAverage
        {
            get => m_DefaultEstimateAverage;
            set => m_DefaultEstimateAverage = value;
        }

        internal int DefaultEstimateUpper
        {
            get => m_DefaultEstimateUpper;
            set => m_DefaultEstimateUpper = value;
        }

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

        internal string CustomCumulativeRewardEstimator
        {
            get => m_CustomCumulativeRewardEstimator;
            set => m_CustomCumulativeRewardEstimator = value;
        }

        Dictionary<string, TraitDefinition> m_TraitNameToDefinition;
        Dictionary<string, TraitDefinition> m_TraitDataNameToDefinition;

        void OnValidate()
        {
            var traitsUsed = GetTraitsUsed();
            m_TraitNameToDefinition = traitsUsed.ToDictionary(t => t.name, t => t);
            m_TraitDataNameToDefinition = traitsUsed.ToDictionary(t => $"{t.name}Data", t => t);
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
            if (m_TraitNameToDefinition == null)
            {
                OnValidate();
            }

            return !m_TraitNameToDefinition.ContainsKey(traitName) ? null : m_TraitNameToDefinition[traitName];
        }

        internal TraitDefinition GetTraitByDataName(string traitDataName)
        {
            if (m_TraitDataNameToDefinition == null)
            {
                OnValidate();
            }

            m_TraitDataNameToDefinition.TryGetValue(traitDataName, out var value);
            return value;
        }
    }
}
#endif
