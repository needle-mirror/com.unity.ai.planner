using System;
using Unity.Jobs;
using UnityEngine.AI.Planner;

namespace Unity.AI.Planner
{
    /// <summary>
    /// Interface marking an implementation of the planner scheduler.
    /// </summary>
    interface IPlannerScheduler
    {
        /// <summary>
        /// The current plan request for which the planning jobs are scheduled.
        /// </summary>
        IPlanRequest CurrentPlanRequest { get; }

        /// <summary>
        /// Initiates and returns a new plan request.
        /// </summary>
        /// <param name="rootState">The root or initial state of the plan</param>
        /// <param name="onRequestComplete">A callback to be invoked once the request has completed</param>
        /// <param name="searchSettings">Settings to configure the planning process</param>
        /// <returns>Returns the plan request to run</returns>
        IPlanRequest RequestPlan(IStateKey rootState, Action<IPlan> onRequestComplete = null, PlannerSearchSettings searchSettings = null);

        /// <summary>
        /// Sets the starting state of the current plan request to the specified state.
        /// </summary>
        /// <param name="newRootState">The key for the new root state of the plan.</param>
        void UpdatePlanRequestRootState(IStateKey newRootState);

        /// <summary>
        /// The job handle for the current planning jobs.
        /// </summary>
        JobHandle CurrentJobHandle { get; }

        /// <summary>
        /// Schedules a single iteration of the search process.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <param name="forceComplete">Option to force the completion of the previously scheduled planning jobs.</param>
        /// <returns></returns>
        JobHandle Schedule(JobHandle inputDeps, bool forceComplete);
    }
}
