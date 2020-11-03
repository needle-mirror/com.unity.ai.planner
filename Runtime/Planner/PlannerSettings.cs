using System;
using Unity.AI.Planner.Jobs;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AI.Planner
{
    /// <summary>
    /// Settings for the control of the planning algorithm iterating on the current plan
    /// </summary>
    [Serializable]
    public class PlannerSettings
    {
        /// <summary>
        /// The number of planning iterations to be completed by each update.
        /// </summary>
        [FormerlySerializedAs("SearchIterationsPerUpdate")]
        [Tooltip("The number of planning iterations to be completed by each update.")]
        public int PlanningIterationsPerUpdate = 1;

        /// <summary>
        /// The number of states to expand within each planning iteration.
        /// </summary>
        [Tooltip("The number of states to expand within each planning iteration.")]
        public int StateExpansionBudgetPerIteration = 1;

        /// <summary>
        /// Enables the delay of planning update by a fixed number of frames.
        /// </summary>
        [FormerlySerializedAs("UseCustomSearchFrequency")]
        [Tooltip("Enables the delay of planning update by a fixed number of frames.")]
        public bool UseCustomPlanningFrequency;

        /// <summary>
        /// The number of frames to delay between each planning update.
        /// </summary>
        [FormerlySerializedAs("MinFramesPerSearchUpdate")]
        [Tooltip("The number of frames to delay between each planning update.")]
        public int MinFramesPerPlanningUpdate;

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
        /// The maximum tolerance required before the planning process ceases.
        /// </summary>
        [FormerlySerializedAs("RootPolicyValueTolerance")]
        [Tooltip("The maximum tolerance required before the planning process ceases.")]
        public float RootEstimatedRewardTolerance;

        /// <summary>
        /// Enables setting a maximum number of planning updates per plan.
        /// </summary>
        [Tooltip("Enables setting a maximum number of planning updates per plan.")]
        public bool CapPlanUpdates;

        /// <summary>
        /// The maximum number of planning updates per plan.
        /// </summary>
        [Tooltip("The maximum number of planning updates per plan.")]
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
