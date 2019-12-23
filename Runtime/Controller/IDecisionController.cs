using System;
using System.Collections.Generic;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace Unity.AI.Planner.Controller
{
    /// <summary>
    /// Interface that marks an implementation of a Decision Controller that update a Plan and execute action methods
    /// when a search is complete
    /// </summary>
    public interface IDecisionController
    {
        /// <summary>
        /// Define if the decision is updated during DecisionController update loop
        /// </summary>
        bool AutoUpdate { get; set; }

        /// <summary>
        /// Returns whether the controller is currently idle (i.e. not planning and not executing actions)
        /// </summary>
        bool IsIdle { get; }

        /// <summary>
        /// List of Local object data
        /// </summary>
        IEnumerable<ITraitBasedObjectData> LocalObjectData { get; }

        /// <summary>
        /// Called after the current Plan state has been updated
        /// </summary>
        event Action stateUpdated;

        /// <summary>
        /// Initialize and create the executor instance
        /// </summary>
        void Initialize();

        /// <summary>
        /// Update execution
        /// </summary>
        void UpdateExecutor();

        /// <summary>
        /// Update planner scheduler. If the previous planning job has not finished, the scheduler will not
        /// scheduler new planning jobs unless forceComplete is true.
        /// </summary>
        /// <param name="forceComplete">Force the scheduler to complete previous planning jobs before scheduling new
        /// iterations.</param>
        void UpdateScheduler(bool forceComplete = false);

        /// <summary>
        /// Force an update of the planner state using the world query
        /// </summary>
        void UpdateStateWithWorldQuery();

        /// <summary>
        /// Get the current state data
        /// </summary>
        /// <param name="readWrite">Whether the state needs write-back capabilities</param>
        /// <returns>State data</returns>
        IStateData GetPlannerState(bool readWrite = false);

        /// <summary>
        /// Get source data for a given object
        /// </summary>
        /// <param name="objectName">Trait-based object name</param>
        /// <returns>Source data object</returns>
        ITraitBasedObjectData GetLocalObjectData(string objectName);
    }
}
