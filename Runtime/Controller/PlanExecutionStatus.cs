using System;

namespace Unity.AI.Planner
{
    /// <summary>
    /// The current status of the execution of a plan
    /// </summary>
    public enum PlanExecutionStatus
    {
        /// <summary>
        /// A status indicating a plan has not been assigned to be executed.
        /// </summary>
        AwaitingPlan,

        /// <summary>
        /// A status indicating that the executor is waiting to execute the next step of the plan.
        /// </summary>
        AwaitingExecution,

        /// <summary>
        /// A status indicating the executor is currently executing an action.
        /// </summary>
        ExecutingAction,
    }
}
