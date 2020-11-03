using System;

namespace Unity.AI.Planner.Traits
{
    /// <summary>
    /// An interface denoting the implementation of a planning systems provider, which initializes the planning modules
    /// for a given <see cref="T:Unity.AI.Planner.Traits.ProblemDefinition"/>.
    /// </summary>
    public interface IPlanningSystemsProvider
    {
        /// <summary>
        /// The state converter, used for creating planning state representations from game state data.
        /// </summary>
        ITraitBasedStateConverter StateConverter { get; }

        /// <summary>
        /// The planning job scheduler, used for requesting plans and scheduling planning.
        /// </summary>
        IPlannerScheduler PlannerScheduler { get; }

        /// <summary>
        /// The control module used to enact plans.
        /// </summary>
        ITraitBasedPlanExecutor PlanExecutor { get; }

        /// <summary>
        /// Initializes the planning system, given a <see cref="T:Unity.AI.Planner.Traits.ProblemDefinition"/>.
        /// </summary>
        /// <param name="problemDefinition">The problem definition asset containing data defining the planning problem.</param>
        /// <param name="planningSimulationWorldName">The name used for the planning simulation world, in which the state data is stored.</param>
        void Initialize(ProblemDefinition problemDefinition, string planningSimulationWorldName);
    }
}

