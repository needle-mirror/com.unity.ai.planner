using System;
using Unity.AI.Planner.Jobs;
using UnityEngine.AI.Planner;

namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of a plan request, a request to compute a plan
    /// </summary>
    interface IPlanRequest
    {
        /// <summary>
        /// The current status of the plan request
        /// </summary>
        PlanRequestStatus Status { get; }

        /// <summary>
        /// The plan constructed by the planning process
        /// </summary>
        IPlan Plan { get; }

        /// <summary>
        /// Halts the planning process until resumed
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes the paused planning process
        /// </summary>
        void Resume();

        /// <summary>
        /// Cancels the plan request
        /// </summary>
        void Cancel();

        /// <summary>
        /// Halts the planning process and disposes the plan for the query
        /// </summary>
        void Dispose();

        /// <summary>
        /// Sets the criteria for ending the planning process
        /// </summary>
        /// <param name="maximumUpdates">The maximum number of updates the planning process may schedule</param>
        /// <param name="planSize">The maximum plan size threshold </param>
        /// <param name="rootStateTolerance">A threshold of convergence between the upper and lower bound for the root state of the plan</param>
        /// <param name="requestCompleteCallback">A callback to be invoked once the plan request has completed</param>
        /// <returns>The updated plan request</returns>
        IPlanRequest SearchUntil(int? maximumUpdates = null, int? planSize = null, float? rootStateTolerance = null, Action<IPlan> requestCompleteCallback = null);

        /// <summary>
        /// Sets the frequency and job modes for the planning jobs
        /// </summary>
        /// <param name="framesPerUpdate">The number of frames to skip per planning iteration</param>
        /// <param name="selectionJobMode">The mode of the selection job to run, sequential or parallel</param>
        /// <param name="backpropagationJobMode">The mode of the backpropagation job to run, sequential or parallel</param>
        /// <returns>The updated plan request</returns>
        IPlanRequest SchedulingMode(int? framesPerUpdate = null, SelectionJobMode? selectionJobMode = null, BackpropagationJobMode? backpropagationJobMode = null);

        /// <summary>
        /// Sets the number of search iterations per update to perform as well as the number of states to expand per iteration.
        /// </summary>
        /// <param name="searchIterationsPerUpdate">The number of search iterations to perform at each update</param>
        /// <param name="stateExpansionsPerIteration">The number of states in the plan to expand during each search iteration</param>
        /// <returns>The updated plan request</returns>
        IPlanRequest WithBudget(int? searchIterationsPerUpdate = null, int? stateExpansionsPerIteration = null);

        /// <summary>
        /// Sets the search settings.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        IPlanRequest WithSettings(PlannerSearchSettings settings);
    }
}
