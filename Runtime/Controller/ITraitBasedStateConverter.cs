using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.Traits;
using Unity.Semantic.Traits;
using Unity.Entities;

namespace Unity.AI.Planner.Traits
{
    interface ITraitBasedStateConverter
    {
        IStateKey CreateState(Entity planningAgent, IEnumerable<Entity> traitBasedObjects = null);
    }
}
