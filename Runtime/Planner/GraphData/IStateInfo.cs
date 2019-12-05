using System;

namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface for data representing information for a state in a plan.
    /// </summary>
    interface IStateInfo
    {
        /// <summary>
        /// The bounded value estimate of the state.
        /// </summary>
        BoundedValue PolicyValue { get; }

        /// <summary>
        /// Designates if the subgraph of the state is complete.
        /// </summary>
        bool SubgraphComplete { get; }
    }
}
