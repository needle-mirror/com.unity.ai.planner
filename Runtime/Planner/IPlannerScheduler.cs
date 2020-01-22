using System;
using Unity.Jobs;

namespace Unity.AI.Planner
{
    /// <summary>
    /// Interface marking an implementation of the planner scheduler.
    /// </summary>
    public interface IPlannerScheduler : IDisposable
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
        /// <param name="settings">Settings to configure the planning process</param>
        /// <returns>Returns the plan request to run</returns>
        IPlanRequest RequestPlan(IStateKey rootState, Action<IPlan> onRequestComplete = null, PlannerSettings settings = null);

        /// <summary>
        /// Sets the starting state of the current plan request to the specified state.
        /// </summary>
        /// <param name="newRootState">The key for the new root state of the plan.</param>
        void UpdatePlanRequestRootState(IStateKey newRootState);

        /// <summary>
        /// Assigns the state termination evaluator to be used by the planner. This procedure will reset the current plan.
        /// </summary>
        /// <param name="evaluator">The instance of the state termination evaluator to be used.</param>
        /// <typeparam name="TEvaluator">The type of termination evaluator. If an incorrect type is used, an error will be logged.</typeparam>
        void SetTerminationEvaluator<TEvaluator>(TEvaluator evaluator) where TEvaluator : struct, ITerminationEvaluator;

        /// <summary>
        /// Assigns the cumulative reward estimator  to be used by the planner. This procedure will reset the current plan.
        /// </summary>
        /// <param name="estimator">The instance of the cumulative reward estimator to be used.</param>
        /// <typeparam name="TEstimator">The type of cumulative reward estimator.  If an incorrect type is used, an error will be logged.</typeparam>
        void SetCumulativeRewardEstimator<TEstimator>(TEstimator estimator) where TEstimator : struct, ICumulativeRewardEstimator;

        /// <summary>
        /// The job handle for the current planning jobs.
        /// </summary>
        JobHandle CurrentJobHandle { get; }

        /// <summary>
        /// Schedules a single iteration of the planning process.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <param name="forceComplete">Option to force the completion of the previously scheduled planning jobs.</param>
        /// <returns></returns>
        JobHandle Schedule(JobHandle inputDeps, bool forceComplete);
    }
}
