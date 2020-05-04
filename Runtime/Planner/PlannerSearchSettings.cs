using System;
using Unity.AI.Planner.Jobs;

namespace UnityEngine.AI.Planner
{
    /// <summary>
    /// Settings for the control of the search algorithm iterating on the current plan
    /// </summary>
    [Serializable]
    public class PlannerSearchSettings
    {
        /// <summary>
        /// The number of search iterations to be completed by each update.
        /// </summary>
        [Tooltip("The number of search iterations to be completed by each update.")]
        public int SearchIterationsPerUpdate = 1;

        /// <summary>
        /// The number of states to expand within each search iteration.
        /// </summary>
        [Tooltip("The number of states to expand within each search iteration.")]
        public int StateExpansionBudgetPerIteration = 1;

        /// <summary>
        /// Enables the delay of search update by a fixed number of frames.
        /// </summary>
        [Tooltip("Enables the delay of search update by a fixed number of frames.")]
        public bool UseCustomSearchFrequency;

        /// <summary>
        /// The number of frames to delay between each search update.
        /// </summary>
        [Tooltip("The number of frames to delay between each search update.")]
        public int MinFramesPerSearchUpdate;

        /// <summary>
        /// Enables setting a maximum plan size.
        /// </summary>
        [Tooltip("Enables setting a maximum plan size.")]
        public bool CapPlanSize;

        /// <summary>
        /// The maximum number of states in the plan.
        /// </summary>
        [Tooltip("The maximum number of states in the plan.")]
        public int MaxStatesInPlan;

        /// <summary>
        /// Enables setting a tolerance for the next immediate decision of the plan.
        /// </summary>
        [Tooltip("Enables setting a tolerance for the next immediate decision of the plan.")]
        public bool StopPlanningWhenToleranceAchieved;

        /// <summary>
        /// The maximum tolerance required before the search process ceases.
        /// </summary>
        [Tooltip("The maximum tolerance required before the search process ceases.")]
        public float RootPolicyValueTolerance;

        /// <summary>
        /// Enables setting a maximum number of search updates per plan.
        /// </summary>
        [Tooltip("Enables setting a maximum number of search updates per plan.")]
        public bool CapPlanUpdates;

        /// <summary>
        /// The maximum number of search updates per plan.
        /// </summary>
        [Tooltip("The maximum number of search updates per plan.")]
        public int MaxUpdates = int.MaxValue;

        /// <summary>
        /// Specifies the job type to run for selecting states to expand.
        /// </summary>
        [Tooltip("Specifies the job type to run for selecting states to expand. [Sequential or Parallel]")]
        public SelectionJobMode GraphSelectionJobMode = SelectionJobMode.Sequential;

        /// <summary>
        /// Specifies the job type to run for updating state values in the plan.
        /// </summary>
        [Tooltip("Specifies the job type to run for updating state values in the plan. [Sequential or Parallel]")]
        public BackpropagationJobMode GraphBackpropagationJobMode = BackpropagationJobMode.Sequential;
    }
}
