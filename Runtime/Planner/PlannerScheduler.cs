using System;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.AI.Planner;

namespace Unity.AI.Planner
{
    /// <summary>
    /// Schedules all planner jobs for one iteration of the planner
    /// </summary>
    /// <typeparam name="TStateKey">StateKey type</typeparam>
    /// <typeparam name="TActionKey">ActionKey type</typeparam>
    /// <typeparam name="TStateManager">StateManager type</typeparam>
    /// <typeparam name="TStateData">StateData type</typeparam>
    /// <typeparam name="TStateDataContext">StateDataContext type</typeparam>
    /// <typeparam name="TActionScheduler">ActionScheduler type</typeparam>
    /// <typeparam name="THeuristic">Heuristic type</typeparam>
    /// <typeparam name="TTerminationEvaluator">TerminationEvaluator type</typeparam>
    class PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator> : IPlannerScheduler, IDisposable
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where TActionScheduler : IActionScheduler<TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey, StateTransitionInfo>
        where THeuristic : struct, IHeuristic<TStateData>
        where TTerminationEvaluator : struct, ITerminationEvaluator<TStateData>
    {
        /// <inheritdoc />
        public PlannerSearchSettings SearchSettings { get; set; }

        internal SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> m_SearchContext;

        // Configuration data
        TStateManager m_StateManager;
        TActionScheduler m_ActionScheduler;
        THeuristic m_Heuristic;
        TTerminationEvaluator m_TerminationEvaluator;
        float k_DiscountFactor;

        // Containers for jobs
        NativeMultiHashMap<TStateKey, int> m_AllSelectedStates;
        NativeHashMap<TStateKey, byte> m_SelectedUnexpandedStates;
        NativeList<TStateKey> m_SelectedUnexpandedStatesList;
        NativeQueue<StateTransitionInfoPair<TStateKey, TActionKey, StateTransitionInfo>> m_CreatedStateInfoQueue;
        NativeList<StateTransitionInfoPair<TStateKey, TActionKey, StateTransitionInfo>> m_CreatedStateInfoList;
        NativeQueue<TStateKey> m_NewStateQueue;
        NativeList<TStateKey> m_NewStateList;
        NativeQueue<TStateKey> m_StatesToDestroy;
        NativeList<TStateKey> m_SelectionInputStates;
        NativeList<int> m_SelectionInputBudgets;
        NativeMultiHashMap<TStateKey, int> m_SelectionOutputStateBudgets;
        NativeMultiHashMap<int, TStateKey> m_SelectedStatesByHorizon;
        NativeHashMap<TStateKey, byte> m_PredecessorStates;
        NativeList<TStateKey> m_HorizonStateList;

        // Data for job scheduling
        int m_MaxDepth;
        int m_GraphSize;
        int m_FramesSinceLastIteration;

        /// <summary>
        /// Initialize a planner scheduler instance
        /// </summary>
        /// <param name="rootStateKey">Key for the root state</param>
        /// <param name="stateManager">StateManager instance</param>
        /// <param name="actionScheduler">ActionScheduler instance</param>
        /// <param name="heuristic">Heuristic</param>
        /// <param name="terminationEvaluator">State termination evaluator</param>
        /// <param name="stateCapacity">Initial state capacity</param>
        /// <param name="actionCapacity">Initial action capacity</param>
        /// <param name="discountFactor">Multiplicative factor ([0 -> 1]) for discount future rewards.</param>
        public void Initialize(TStateKey rootStateKey, TStateManager stateManager, TActionScheduler actionScheduler, THeuristic heuristic = default,
            TTerminationEvaluator terminationEvaluator = default, int stateCapacity = 1, int actionCapacity = 1, float discountFactor = 0.95f)
        {
            m_SearchContext = new SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>(rootStateKey, stateManager, stateCapacity, actionCapacity);
            m_StateManager = stateManager;
            m_ActionScheduler = actionScheduler;
            m_Heuristic = heuristic;
            m_TerminationEvaluator = terminationEvaluator;
            k_DiscountFactor = discountFactor;

            m_AllSelectedStates = new NativeMultiHashMap<TStateKey, int>(1, Allocator.Persistent);
            m_SelectedUnexpandedStates = new NativeHashMap<TStateKey,byte>(1, Allocator.Persistent);
            m_SelectedUnexpandedStatesList = new NativeList<TStateKey>(Allocator.Persistent);
            m_SelectionInputStates = new NativeList<TStateKey>(1, Allocator.Persistent);
            m_SelectionInputBudgets = new NativeList<int>(1, Allocator.Persistent);
            m_SelectionOutputStateBudgets = new NativeMultiHashMap<TStateKey, int>(1, Allocator.Persistent);

            m_CreatedStateInfoQueue = new NativeQueue<StateTransitionInfoPair<TStateKey, TActionKey, StateTransitionInfo>>(Allocator.Persistent);
            m_CreatedStateInfoList = new NativeList<StateTransitionInfoPair<TStateKey, TActionKey, StateTransitionInfo>>(Allocator.Persistent);

            m_NewStateQueue = new NativeQueue<TStateKey>(Allocator.Persistent);
            m_NewStateList = new NativeList<TStateKey>(Allocator.Persistent);
            m_StatesToDestroy = new NativeQueue<TStateKey>(Allocator.Persistent);

            m_SelectedStatesByHorizon = new NativeMultiHashMap<int, TStateKey>(1, Allocator.Persistent);
            m_PredecessorStates = new NativeHashMap<TStateKey, byte>(1, Allocator.Persistent);
            m_HorizonStateList = new NativeList<TStateKey>(1, Allocator.Persistent);
        }

        /// <summary>
        /// Schedule jobs for each of the planner stages
        /// </summary>
        /// <param name="inputDeps">Any job dependencies</param>
        /// <returns>JobHandle for the scheduled jobs</returns>
        public JobHandle Schedule(JobHandle inputDeps)
        {
            if (SearchSettings == null)
                return ScheduleSearchJobs(inputDeps);

            m_FramesSinceLastIteration++;

            if (SearchSettings.CapPlanSize && m_SearchContext.PolicyGraph.Size > SearchSettings.MaxStatesInPlan)
                return default;

            if (SearchSettings.StopPlanningWhenToleranceAchieved && m_SearchContext.RootsConverged(SearchSettings.RootPolicyValueTolerance))
                return default;

            if (SearchSettings.UseCustomSearchFrequency && m_FramesSinceLastIteration < SearchSettings.FramesPerSearchIteration)
                return default;

            m_FramesSinceLastIteration = 0;
            return ScheduleSearchJobs(inputDeps, SearchSettings.StateExpansionBudgetPerIteration, SearchSettings.GraphSelectionJobMode, SearchSettings.GraphBackpropagationJobMode);
        }

        JobHandle ScheduleSearchJobs(JobHandle inputDeps, int budget = 1, SelectionJobMode selectionJobMode = SelectionJobMode.Sequential, BackpropagationJobMode backpropagationJobMode = BackpropagationJobMode.Sequential)
        {
            // todo - better disposal of unneeded state entities
            while (m_StatesToDestroy.TryDequeue(out var stateKey))
            {
                m_StateManager.DestroyState(stateKey);
            }

            // todo - other conditions under which not to plan (graph size, convergence to a tolerance, frequency?)
            if (budget <= 0 || m_SearchContext.PolicyGraph.StateInfoLookup[m_SearchContext.RootStateKey].SubgraphComplete)
                return default;

            ClearContainers();

            if (backpropagationJobMode == BackpropagationJobMode.Parallel || selectionJobMode == SelectionJobMode.Parallel)
            {
                // Find upper bound on number of selection and backpropagation iterations
                m_MaxDepth = 0;
                m_GraphSize = m_SearchContext.PolicyGraph.StateInfoLookup.Length;
                using (var depths = m_SearchContext.StateDepthLookup.GetValueArray(Allocator.Temp))
                {
                    for (int i = 0; i < depths.Length; i++)
                        m_MaxDepth = math.max(m_MaxDepth, depths[i]);
                }
            }


            var selectionJobHandle = selectionJobMode == SelectionJobMode.Sequential ?
                ScheduleSelectionSequential(inputDeps, budget) :
                ScheduleSelectionParallel(inputDeps, budget);

            var actionsJobHandle = ScheduleActionsJobs(selectionJobHandle);
            var graphExpansionJobHandle = ScheduleExpansionJob(actionsJobHandle);
            var evaluateNewStatesJobHandle = ScheduleEvaluateNewStatesJob(graphExpansionJobHandle);

            var backupJobHandle = backpropagationJobMode == BackpropagationJobMode.Sequential ?
                ScheduleBackpropagationSequential(evaluateNewStatesJobHandle) :
                ScheduleBackpropagationParallel(evaluateNewStatesJobHandle);

            return backupJobHandle;
        }

        JobHandle ScheduleSelectionSequential(JobHandle inputDeps, int budget)
        {
            var policyGraph = m_SearchContext.PolicyGraph;

            var jobHandle = new SelectionJob<TStateKey, TActionKey>()
            {
                SearchBudget = budget,
                RootStateKey = m_SearchContext.RootStateKey,
                StateDepthLookup = m_SearchContext.StateDepthLookup,
                StateInfoLookup = policyGraph.StateInfoLookup,
                ActionLookup = policyGraph.ActionLookup,
                ActionInfoLookup = policyGraph.ActionInfoLookup,
                ResultingStateLookup = policyGraph.ResultingStateLookup,
                StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup,

                SelectedUnexpandedStates = m_SelectedUnexpandedStatesList,
                AllSelectedStates = m_AllSelectedStates,
            }.Schedule(inputDeps);

            return jobHandle;
        }

        JobHandle ScheduleSelectionParallel(JobHandle inputDeps, int budget)
        {
            // Setup input containers
            m_SelectionInputStates.Add(m_SearchContext.RootStateKey);
            m_SelectionInputBudgets.Add(budget);

            // Resize container to avoid full hash map
            int size = math.min(budget, m_SearchContext.PolicyGraph.StateInfoLookup.Length);
            m_SelectionOutputStateBudgets.Capacity = math.max(m_SelectionOutputStateBudgets.Capacity, size);
            m_SelectedUnexpandedStates.Capacity = math.max(m_SelectedUnexpandedStates.Capacity, size);
            m_AllSelectedStates.Capacity = math.max(m_AllSelectedStates.Capacity, size);

            var jobHandle = inputDeps;
            var policyGraph = m_SearchContext.PolicyGraph;
            for (int iteration = 0; iteration <= m_MaxDepth; iteration++)
            {
                // Selection job
                jobHandle = new ParallelSelectionJob<TStateKey, TActionKey>
                {
                    StateDepthLookup = m_SearchContext.StateDepthLookup,
                    StateInfoLookup = policyGraph.StateInfoLookup,
                    ActionInfoLookup = policyGraph.ActionInfoLookup,
                    ActionLookup = policyGraph.ActionLookup,
                    ResultingStateLookup = policyGraph.ResultingStateLookup,
                    StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup,

                    Horizon = iteration,
                    InputStates = m_SelectionInputStates.AsDeferredJobArray(),
                    InputBudgets = m_SelectionInputBudgets.AsDeferredJobArray(),

                    OutputStateBudgets = m_SelectionOutputStateBudgets.AsParallelWriter(),
                    SelectedStateHorizons = m_AllSelectedStates.AsParallelWriter(),
                    SelectedUnexpandedStates = m_SelectedUnexpandedStates.AsParallelWriter(),
                }.Schedule(m_SelectionInputStates, default, jobHandle);

                // Collect output job
                jobHandle = new CollectAndAssignSelectionBudgets<TStateKey>
                {
                    InputStateBudgets = m_SelectionOutputStateBudgets,
                    OutputStates = m_SelectionInputStates,
                    OutputBudgets = m_SelectionInputBudgets,
                }.Schedule(jobHandle);
            }

            jobHandle = new CollectUnexpandedStates<TStateKey>
            {
                SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                SelectedUnexpandedStatesList = m_SelectedUnexpandedStatesList,
            }.Schedule(jobHandle);

            return jobHandle;
        }

        JobHandle ScheduleActionsJobs(JobHandle inputDeps)
        {
            // FIXME: Currently DOTS requires a JobComponentSystem in order to get buffers off of, so unfortunately we
            // can't use a struct for this part yet; The goal is for this scheduler code to remain agnostic to implementation
            m_ActionScheduler.UnexpandedStates = m_SelectedUnexpandedStatesList;
            m_ActionScheduler.CreatedStateInfo = m_CreatedStateInfoQueue;
            m_ActionScheduler.StateManager = m_StateManager;
            return m_ActionScheduler.Schedule(inputDeps);
        }

        JobHandle ScheduleExpansionJob(JobHandle actionsJobHandle)
        {
            var prepareForExpansionJobHandle = new PrepareForExpansionJob<TStateKey, TActionKey>
            {
                PolicyGraph = m_SearchContext.PolicyGraph,
                InputStateExpansionInfo = m_CreatedStateInfoQueue,
                OutputStateExpansionInfo = m_CreatedStateInfoList,
                BinnedStateKeys = m_SearchContext.BinnedStateKeyLookup,
            }.Schedule(actionsJobHandle);

            var policyGraph = m_SearchContext.PolicyGraph;
            return new GraphExpansionJob<TStateKey, TStateData, TStateDataContext, TActionKey>
            {
                NewStateTransitionInfoPairs = m_CreatedStateInfoList.AsDeferredJobArray(),
                StateDataContext = m_StateManager.GetStateDataContext(),

                ActionLookup = policyGraph.ActionLookup.AsParallelWriter(),
                ActionInfoLookup = policyGraph.ActionInfoLookup.AsParallelWriter(),
                NewStates = m_NewStateQueue.AsParallelWriter(),
                StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup.AsParallelWriter(),
                PredecessorGraph = policyGraph.PredecessorGraph.AsParallelWriter(),
                BinnedStateKeys = m_SearchContext.BinnedStateKeyLookup,
                ResultingStateLookup = policyGraph.ResultingStateLookup.AsParallelWriter(),
                StatesToDestroy = m_StatesToDestroy.AsParallelWriter(),
            }.Schedule(m_CreatedStateInfoList, 0, prepareForExpansionJobHandle);
        }

        JobHandle ScheduleEvaluateNewStatesJob(JobHandle graphExpansionJob)
        {
            var newStateQueueToListJobHandle = new QueueToListJob<TStateKey>
            {
                InputQueue = m_NewStateQueue,
                OutputList = m_NewStateList,
            }.Schedule(graphExpansionJob);

            return new EvaluateNewStatesJob<TStateKey, TStateData, TStateDataContext, THeuristic, TTerminationEvaluator>
            {
                States = m_NewStateList.AsDeferredJobArray(),
                StateDataContext = m_StateManager.GetStateDataContext(),
                Heuristic = m_Heuristic,
                TerminationEvaluator = m_TerminationEvaluator,

                StateInfoLookup = m_SearchContext.PolicyGraph.StateInfoLookup.AsParallelWriter(),
                BinnedStateKeys = m_SearchContext.BinnedStateKeyLookup.AsParallelWriter(),
            }.Schedule(m_NewStateList, 0, newStateQueueToListJobHandle);
        }

        JobHandle ScheduleBackpropagationSequential(JobHandle evaluateNewStatesJobHandle)
        {
            return new BackpropagationJob<TStateKey, TActionKey>
            {
                DepthMap = m_SearchContext.StateDepthLookup,
                PolicyGraph = m_SearchContext.PolicyGraph,
                SelectedStates = m_AllSelectedStates,
                DiscountFactor = k_DiscountFactor,
            }.Schedule(evaluateNewStatesJobHandle);
        }

        JobHandle ScheduleBackpropagationParallel(JobHandle evaluateNewStatesJobHandle)
        {
            var jobHandle = evaluateNewStatesJobHandle;
            var policyGraph = m_SearchContext.PolicyGraph;

            // Resize containers
            m_SelectedStatesByHorizon.Capacity = math.max(m_SelectedStatesByHorizon.Capacity, m_MaxDepth + 1);
            m_PredecessorStates.Capacity = math.max(m_PredecessorStates.Capacity, m_GraphSize);
            m_HorizonStateList.Capacity = math.max(m_HorizonStateList.Capacity, m_GraphSize);

            jobHandle = new UpdateDepthMapJob<TStateKey>
            {
                SelectedStates = m_AllSelectedStates,
                DepthMap = m_SearchContext.StateDepthLookup,
                SelectedStatesByHorizon = m_SelectedStatesByHorizon,
            }.Schedule(jobHandle);

            // Schedule maxDepth iterations of backpropagation
            for (int horizon = m_MaxDepth + 1; horizon >= 0; horizon--)
            {
                // Prepare info
                jobHandle = new PrepareBackpropagationHorizon<TStateKey>
                {
                    Horizon = horizon,
                    SelectedStatesByHorizon = m_SelectedStatesByHorizon,
                    PredecessorInputStates = m_PredecessorStates,

                    OutputStates = m_HorizonStateList,
                }.Schedule(jobHandle);

                // Compute updated values
                jobHandle = new ParallelBackpropagationJob<TStateKey, TActionKey>
                {
                    // Params
                    DiscountFactor = k_DiscountFactor,

                    // Policy graph info
                    ActionLookup = policyGraph.ActionLookup,
                    PredecessorGraph = policyGraph.PredecessorGraph,
                    ResultingStateLookup = policyGraph.ResultingStateLookup,
                    StateInfoLookup = policyGraph.StateInfoLookup,
                    ActionInfoLookup = policyGraph.ActionInfoLookup,
                    StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup,

                    // Input
                    StatesToUpdate = m_HorizonStateList.AsDeferredJobArray(),

                    // Output
                    PredecessorStatesToUpdate = m_PredecessorStates.AsParallelWriter(),
                }.Schedule(m_HorizonStateList, default, jobHandle);
            }

            // Continue propagating node labels (but not value updates)
            jobHandle = new UpdateCompleteStatusJob<TStateKey, TActionKey>
            {
                StatesToUpdate = m_PredecessorStates,
                PolicyGraph = policyGraph,
            }.Schedule(jobHandle);

            return jobHandle;
        }

        void ClearContainers()
        {
            m_AllSelectedStates.Clear();
            m_SelectedUnexpandedStates.Clear();
            m_SelectedUnexpandedStatesList.Clear();
            m_SelectionInputStates.Clear();
            m_SelectionInputBudgets.Clear();
            m_SelectionOutputStateBudgets.Clear();
            m_CreatedStateInfoQueue.Clear();
            m_CreatedStateInfoList.Clear();
            m_NewStateQueue.Clear();
            m_NewStateList.Clear();
            m_StatesToDestroy.Clear();
            m_SelectedStatesByHorizon.Clear();
            m_PredecessorStates.Clear();
            m_HorizonStateList.Clear();
        }

        /// <summary>
        /// Dispose of planner scheduler instance
        /// </summary>
        public void Dispose()
        {
            m_SearchContext.Dispose();

            m_AllSelectedStates.Dispose();
            m_SelectedUnexpandedStates.Dispose();
            m_SelectedUnexpandedStatesList.Dispose();
            m_SelectionInputStates.Dispose();
            m_SelectionInputBudgets.Dispose();
            m_SelectionOutputStateBudgets.Dispose();
            m_CreatedStateInfoQueue.Dispose();
            m_CreatedStateInfoList.Dispose();
            m_NewStateQueue.Dispose();
            m_NewStateList.Dispose();
            m_StatesToDestroy.Dispose();
            m_SelectedStatesByHorizon.Dispose();
            m_PredecessorStates.Dispose();
            m_HorizonStateList.Dispose();
        }
    }

}
