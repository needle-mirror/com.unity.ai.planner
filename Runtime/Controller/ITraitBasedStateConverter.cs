using System;
using System.Collections.Generic;
using Unity.Entities;

namespace Unity.AI.Planner.Traits
{
    /// <summary>
    /// An interface denoting the implementation of a state converter for trait-based planning states. A state converter
    /// creates planning state representations from game state data.
    /// </summary>
    public interface ITraitBasedStateConverter : IDisposable
    {
        /// <summary>
        /// Creates a planning state from a set of entities holding traits. Only traits covered by the converter's
        /// associated <see cref="ProblemDefinition"/> will be included.
        /// </summary>
        /// <param name="planningAgent">The entity representing the planning agent.</param>
        /// <param name="traitBasedObjects">The entities representing objects to be included in the planning state.</param>
        /// <returns>A planning state instance representing the given objects.</returns>
        IStateKey CreateState(Entity planningAgent, IEnumerable<Entity> traitBasedObjects = null);
    }
}
