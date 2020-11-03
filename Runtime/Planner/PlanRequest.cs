using System;
using Unity.AI.Planner.Jobs;

namespace Unity.AI.Planner
{
    /// <summary>
    /// Status of a plan request.
    /// </summary>
    public enum PlanRequestStatus
    {
        /// <summary> Building the plan. </summary>
        Running,
        /// <summary> Planning jobs paused. </summary>
        Paused,
        /// <summary> Plan has met minimum planning criteria. </summary>
        Complete,
        /// <summary> Plan data deallocated. </summary>
        Disposed
    }

    class PlanRequest<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> : IPlanRequest, IDisposable
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
    {
        /// <inheritdoc cref="IPlanRequest"/>
        public PlanRequestStatus Status { get; internal set; }

        /// <inheritdoc cref="IPlanRequest"/>
        public IPlan Plan => m_Plan;

        internal PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> m_Plan;
        internal PlannerSettings m_PlannerSettings;
        internal Action<IPlan> m_OnPlanRequestComplete;
        internal int m_PlanningUpdates;
        internal int m_FramesSinceLastUpdate;

        internal PlanRequest(PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> plan, PlannerSettings settings = null, Action<IPlan> planRequestCompleteCallback = null)
        {
            m_Plan = plan;
            m_PlannerSettings = settings ?? new PlannerSettings();
            m_OnPlanRequestComplete = planRequestCompleteCallback;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public void Pause()
        {
            CheckAndThrowIfDisposed();

            if (Status != PlanRequestStatus.Complete)
                Status = PlanRequestStatus.Paused;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public void Resume()
        {
            CheckAndThrowIfDisposed();

            if (Status != PlanRequestStatus.Complete)
                Status = PlanRequestStatus.Running;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public void Cancel()
        {
            CheckAndThrowIfDisposed();

            Status = PlanRequestStatus.Complete;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public IPlanRequest PlanUntil(int? maximumUpdates = null, int? planSize = null, float? rootStateTolerance = null, Action<IPlan> requestCompleteCallback = null)
        {
            CheckAndThrowIfDisposed();

            if (maximumUpdates.HasValue)
            {
                m_PlannerSettings.CapPlanUpdates = true;
                m_PlannerSettings.MaxUpdates = maximumUpdates.Value;
            }

            if (planSize.HasValue)
            {
                m_PlannerSettings.CapPlanSize = true;
                m_PlannerSettings.MaxStatesInPlan = planSize.Value;
            }

            if (rootStateTolerance.HasValue)
            {
                m_PlannerSettings.StopPlanningWhenToleranceAchieved = true;
                m_PlannerSettings.RootEstimatedRewardTolerance = rootStateTolerance.Value;
            }

            m_OnPlanRequestComplete = requestCompleteCallback ?? m_OnPlanRequestComplete;

            return this;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public IPlanRequest WithBudget(int? planningIterationsPerUpdate = null, int? stateExpansionsPerIteration = null)
        {
            CheckAndThrowIfDisposed();

            if (planningIterationsPerUpdate.HasValue)
                m_PlannerSettings.PlanningIterationsPerUpdate = planningIterationsPerUpdate.Value;

            if (stateExpansionsPerIteration.HasValue)
                m_PlannerSettings.StateExpansionBudgetPerIteration = stateExpansionsPerIteration.Value;

            return this;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public IPlanRequest SchedulingMode(int? framesPerUpdate = null, SelectionJobMode? selectionJobMode = null, BackpropagationJobMode? backpropagationJobMode = null)
        {
            CheckAndThrowIfDisposed();

            if (framesPerUpdate.HasValue)
            {
                m_PlannerSettings.UseCustomPlanningFrequency = true;
                m_PlannerSettings.MinFramesPerPlanningUpdate = framesPerUpdate.Value;
            }

            if (selectionJobMode.HasValue)
                m_PlannerSettings.GraphSelectionJobMode = selectionJobMode.Value;

            if (backpropagationJobMode.HasValue)
                m_PlannerSettings.GraphBackpropagationJobMode = backpropagationJobMode.Value;

            return this;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public IPlanRequest WithSettings(PlannerSettings settings)
        {
            CheckAndThrowIfDisposed();
            this.m_PlannerSettings = settings;
            return this;
        }

        /// <inheritdoc cref="IDisposable"/>
        public void Dispose()
        {
            Status = PlanRequestStatus.Disposed;
            m_Plan = null;
        }

        void CheckAndThrowIfDisposed()
        {
            if (Status == PlanRequestStatus.Disposed)
                throw new InvalidOperationException($"PlanRequest has been disposed.");
        }
    }
}
