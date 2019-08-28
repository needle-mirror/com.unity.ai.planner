#if !UNITY_DOTSPLAYER
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    class PlanningDomainDefinition : ScriptableObject
    {
        public string Name => TypeResolver.ToTypeNameCase(name);

        [SerializeField]
        List<ActionDefinition> m_ActionDefinitions;

        [SerializeField]
        List<StateTerminationDefinition> m_StateTerminationDefinitions;

        public IEnumerable<ActionDefinition> ActionDefinitions
        {
            get => m_ActionDefinitions;
            set => m_ActionDefinitions = value.ToList();
        }

        public IEnumerable<StateTerminationDefinition> StateTerminationDefinitions
        {
            get => m_StateTerminationDefinitions;
            set => m_StateTerminationDefinitions = value.ToList();
        }

        private Dictionary<string, TraitDefinition> m_traitDefinitions = null;

        public void InitializeTraits()
        {
            var traitList = new List<TraitDefinition>();
            foreach (var actionDefinition in ActionDefinitions)
            {
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
            }

            foreach (var stateTerminationDefinition in StateTerminationDefinitions)
            {
                traitList.AddRange(stateTerminationDefinition.ObjectParameters.RequiredTraits);
                traitList.AddRange(stateTerminationDefinition.ObjectParameters.ProhibitedTraits);
            }
            m_traitDefinitions = traitList.Distinct().ToDictionary(t => t.Name, t => t);
        }

        public TraitDefinition GetTrait(string traitName)
        {
            if (m_traitDefinitions == null)
            {
                InitializeTraits();
            }

            if (!m_traitDefinitions.ContainsKey(traitName))
            {
                Debug.LogError($"Unable to find the trait {traitName}");
                return null;
            }

            return m_traitDefinitions[traitName];
        }
    }
}
#endif
