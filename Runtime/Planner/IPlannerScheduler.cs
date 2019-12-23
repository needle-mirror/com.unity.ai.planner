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
        /// Settings governing the scheduling of the search jobs.
        /// </summary>
        PlannerSearchSettings SearchSettings { get; set; }

        /// <summary>
        /// The job handle for the current planning jobs.
        /// </summary>
        JobHandle CurrentJobHandle { get; }

        /// <summary>
        /// Schedules a single iteration of the search process.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// /// <param name="forceComplete">Option to force the completion of the previously scheduled planning jobs.</param>
        /// <returns></returns>
        JobHandle Schedule(JobHandle inputDeps, bool forceComplete);
    }
}
