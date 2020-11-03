using UnityEngine;
using Unity.AI.Planner.Traits;
using Generated.AI.Planner.StateRepresentation;
using Generated.AI.Planner.StateRepresentation.PlanA;

struct CustomActionReward : ICustomActionReward<StateData>
{
    public float RewardModifier(StateData originalState, ActionKey action, StateData newState)
    {
        return 0;
    }
}
