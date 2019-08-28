#if !UNITY_DOTSPLAYER
using System;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    [CreateAssetMenu(fileName = "New Agent", menuName = "AI/Planner/Agent Definition")]
    class AgentDefinition : PlanningDomainDefinition
    {
    }
}
#endif
