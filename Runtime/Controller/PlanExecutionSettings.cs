using System;
using UnityEngine;

namespace UnityEngine.AI.Planner
{
    /// <summary>
    /// Settings for control of the execution of plans.
    /// </summary>
    [Serializable]
    public class PlanExecutionSettings
    {
        /// <summary>
        /// Modes for managing when actions are executed.
        /// </summary>
        public enum PlanExecutionMode
        {
            /// <summary>Execute the next action in the plan immediately.</summary>
            ActImmediately,
            /// <summary>Do not execute an action unless manually triggered.</summary>
            WaitForManualExecutionCall,
            /// <summary>Execute an action only once the plan has reached a terminal state for each possible outcome.</summary>
            WaitForPlanCompletion,
            /// <summary>Execute an action only once the value estimate has converged to a given tolerance.</summary>
            WaitForMaximumDecisionTolerance,
            /// <summary>Execute an action only once the plan has grown to a minimum size.</summary>
            WaitForMinimumPlanSize,
            /// <summary>Execute an action only once the search process has run for a minimum amount of time.</summary>
            WaitForMinimumSearchTime
        }

        /// <summary>
        /// The criteria for an agent to begin executing an action.
        /// </summary>
        [Tooltip("The criteria for an agent to begin executing an action.")]
        public PlanExecutionMode ExecutionMode = PlanExecutionMode.ActImmediately;

        /// <summary>
        /// The maximum range of the next value estimate required to act.
        /// </summary>
        [Tooltip("The maximum range of the next value estimate required to act.")]
        public float MaximumDecisionTolerance = float.MaxValue;

        /// <summary>
        /// The minimum size of a plan required to act.
        /// </summary>
        [Tooltip("The minimum size of a plan required to act.")]
        public int MinimumPlanSize = 0;

        /// <summary>
        /// The minimum time spent iterating on the plan required to act.
        /// </summary>
        [Tooltip("The minimum time spent iterating on the plan required to act.")]
        public float MinimumSearchTime = 0;
    }
}
