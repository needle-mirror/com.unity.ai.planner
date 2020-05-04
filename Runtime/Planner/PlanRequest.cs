using System;
using Unity.AI.Planner.Jobs;
using UnityEngine.AI.Planner;

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
        /// <summary> Plan has met minimum search criteria. </summary>
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
        internal PlannerSearchSettings m_SearchSettings;
        internal Action<IPlan> m_OnPlanRequestComplete;
        internal int m_PlanningUpdates;
        internal int m_FramesSinceLastUpdate;

        internal PlanRequest(PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> plan, PlannerSearchSettings searchSettings = null, Action<IPlan> planRequestCompleteCallback = null)
        {
            m_Plan = plan;
            m_SearchSettings = searchSettings ?? new PlannerSearchSettings();
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
        public IPlanRequest SearchUntil(int? maximumUpdates = null, int? planSize = null, float? rootStateTolerance = null, Action<IPlan> requestCompleteCallback = null)
        {
            CheckAndThrowIfDisposed();

            if (maximumUpdates.HasValue)
            {
                m_SearchSettings.CapPlanUpdates = true;
                m_SearchSettings.MaxUpdates = maximumUpdates.Value;
            }

            if (planSize.HasValue)
            {
                m_SearchSettings.CapPlanSize = true;
                m_SearchSettings.MaxStatesInPlan = planSize.Value;
            }

            if (rootStateTolerance.HasValue)
            {
                m_SearchSettings.StopPlanningWhenToleranceAchieved = true;
                m_SearchSettings.RootPolicyValueTolerance = rootStateTolerance.Value;
            }

            m_OnPlanRequestComplete = requestCompleteCallback ?? m_OnPlanRequestComplete;

            return this;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public IPlanRequest WithBudget(int? searchIterationsPerUpdate = null, int? stateExpansionsPerIteration = null)
        {
            CheckAndThrowIfDisposed();

            if (searchIterationsPerUpdate.HasValue)
                m_SearchSettings.SearchIterationsPerUpdate = searchIterationsPerUpdate.Value;

            if (stateExpansionsPerIteration.HasValue)
                m_SearchSettings.StateExpansionBudgetPerIteration = stateExpansionsPerIteration.Value;

            return this;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public IPlanRequest SchedulingMode(int? framesPerUpdate = null, SelectionJobMode? selectionJobMode = null, BackpropagationJobMode? backpropagationJobMode = null)
        {
            CheckAndThrowIfDisposed();

            if (framesPerUpdate.HasValue)
            {
                m_SearchSettings.UseCustomSearchFrequency = true;
                m_SearchSettings.MinFramesPerSearchUpdate = framesPerUpdate.Value;
            }

            if (selectionJobMode.HasValue)
                m_SearchSettings.GraphSelectionJobMode = selectionJobMode.Value;

            if (backpropagationJobMode.HasValue)
                m_SearchSettings.GraphBackpropagationJobMode = backpropagationJobMode.Value;

            return this;
        }

        /// <inheritdoc cref="IPlanRequest"/>
        public IPlanRequest WithSettings(PlannerSearchSettings searchSettings)
        {
            CheckAndThrowIfDisposed();
            m_SearchSettings = searchSettings;
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
