using System;

namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface for data representing information for an action in a plan.
    /// </summary>
    interface IActionInfo
    {
        /// <summary>
        /// The bounded value estimate of the action.
        /// </summary>
        BoundedValue ActionValue { get; }

        /// <summary>
        /// Designates if the subgraph of the action is complete.
        /// </summary>
        bool SubgraphComplete { get; }
    }
}
