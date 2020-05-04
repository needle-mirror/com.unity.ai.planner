using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    interface ITraitBasedStateConverter
    {
        IStateKey CreateStateFromObjectData(IList<ITraitBasedObjectData> initialTraitBasedObjects);
    }
}
