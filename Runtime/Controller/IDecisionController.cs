using System;
using System.Collections.Generic;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace Unity.AI.Planner.Controller
{
    /// <summary>
    /// Interface that marks an implementation of a Decision Controller that update a Plan and execute action methods when a search is complete
    /// </summary>
    public interface IDecisionController
    {
        /// <summary>
        /// Define if the decision is updated during DecisionController update loop
        /// </summary>
        bool AutoUpdate { get; set; }

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
        /// Update planner scheduler
        /// </summary>
        void UpdateScheduler();

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
