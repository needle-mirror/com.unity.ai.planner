﻿using UnityEngine;
using Unity.AI.Planner.Traits;
using Generated.AI.Planner.StateRepresentation;
using Generated.AI.Planner.StateRepresentation.PlanA;

struct CustomActionPrecondition : ICustomActionPrecondition<StateData>
{
    public bool CheckCustomPrecondition(StateData state, ActionKey action)
    {
        return true;
    }
}

struct CustomTerminationPrecondition : ICustomTerminationPrecondition<StateData>
{
    public bool CheckCustomPrecondition(StateData state)
    {
        return true;
    }
}
