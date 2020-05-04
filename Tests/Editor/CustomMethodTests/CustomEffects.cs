using UnityEngine;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Generated.AI.Planner.StateRepresentation;
using Generated.AI.Planner.StateRepresentation.Enums;
using Generated.AI.Planner.StateRepresentation.PlanA;

struct CustomActionEffect : ICustomActionEffect<StateData>
{
    public void ApplyCustomActionEffectsToState(StateData originalState, ActionKey action, StateData newState)
    {
    }
}
