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
        PlannerSearchSettings SearchSettings { set; }

        /// <summary>
        /// Schedules a single iteration of the search process.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns></returns>
        JobHandle Schedule(JobHandle inputDeps);
    }
}
