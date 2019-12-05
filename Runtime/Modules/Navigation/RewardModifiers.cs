using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine;

namespace Unity.AI.Planner.Navigation
{
    struct LocationDistance : ICustomTraitReward<Location, Location>
    {
        public float RewardModifier(Location location1, Location location2)
        {
            return Vector3.Distance(location1.Position, location2.Position);
        }
    }
}
