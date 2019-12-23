using System;
using UnityEngine;

namespace UnityEngine.AI.Planner
{
    [Serializable]
    class PlanExecutionSettings
    {
        public enum PlanExecutionMode
        {
            /// <summary>Execute the next action in the plan immediately.</summary>
            ActImmediately,
            /// <summary>Do not execute an action unless manually triggered.</summary>
            WaitForActMethodCall,
            /// <summary>Execute an action only once the plan has reached a terminal state for each possible outcome.</summary>
            WaitForPlanCompletion,
            /// <summary>Execute an action only once the value estimate has converged to a given tolerance.</summary>
            WaitForMaximumDecisionTolerance,
            /// <summary>Execute an action only once the plan has grown to a minimum size.</summary>
            WaitForMinimumPlanSize,
            /// <summary>Execute an action only once the search process has run for a minimum amount of time.</summary>
            WaitForMinimumSearchTime
        }

        [Tooltip("The criteria for an agent to begin executing an action.")]
        public PlanExecutionMode ExecutionMode;

        [Tooltip("The maximum range of the next value estimate required to act.")]
        public float MaximumDecisionTolerance;

        [Tooltip("The minimum size of a plan required to act.")]
        public int MinimumPlanSize;

        [Tooltip("The minimum time spent iterating on the plan required to act.")]
        public float MinimumSearchTime;
    }
}
