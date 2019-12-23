using System;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.AI.Planner;

namespace Unity.AI.Planner
{
    class PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator, TDestroyStatesScheduler> : IPlannerScheduler, IDisposable
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where TActionScheduler : struct, IActionScheduler<TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey>
        where THeuristic : struct, IHeuristic<TStateData>
        where TTerminationEvaluator : struct, ITerminationEvaluator<TStateData>
        where TDestroyStatesScheduler : struct, IDestroyStatesScheduler<TStateKey, TStateData, TStateDataContext, TStateManager>
    {
        /// <inheritdoc />
        public PlannerSearchSettings SearchSettings { get; set; }

        /// <inheritdoc />
        public JobHandle CurrentJobHandle { get; private set; }

        internal SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> SearchContext;

        // Configuration data
        TStateManager m_StateManager;
        THeuristic m_Heuristic;
        TTerminationEvaluator m_TerminationEvaluator;
        float m_DiscountFactor;

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
        NativeQueue<TStateKey>.ParallelWriter m_NewStateQueueParallelWriter;
        NativeQueue<TStateKey>.ParallelWriter m_StatesToDestroyParallelWriter;

        // Data for job scheduling
        int m_MaxDepth;
        int m_FramesSinceLastUpdate;

        /// <summary>
        /// Initialize a planner scheduler instance
        /// </summary>
        /// <param name="rootStateKey">Key for the root state</param>
        /// <param name="stateManager">StateManager instance</param>
        /// <param name="heuristic">Heuristic</param>
        /// <param name="terminationEvaluator">State termination evaluator</param>
        /// <param name="stateCapacity">Initial state capacity</param>
        /// <param name="actionCapacity">Initial action capacity</param>
        /// <param name="discountFactor">Multiplicative factor ([0 -> 1]) for discounting future rewards.</param>
        public void Initialize(TStateKey rootStateKey, TStateManager stateManager, THeuristic heuristic = default,
            TTerminationEvaluator terminationEvaluator = default, int stateCapacity = 1, int actionCapacity = 1, float discountFactor = 0.95f)
        {
            SearchContext = new SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>(rootStateKey, stateManager, stateCapacity, actionCapacity);
            m_StateManager = stateManager;
            m_Heuristic = heuristic;
            m_TerminationEvaluator = terminationEvaluator;
            m_DiscountFactor = discountFactor;

            m_AllSelectedStates = new NativeMultiHashMap<TStateKey, int>(1, Allocator.Persistent);
            m_SelectedUnexpandedStates = new NativeHashMap<TStateKey,byte>(1, Allocator.Persistent);
            m_SelectedUnexpandedStatesList = new NativeList<TStateKey>(Allocator.Persistent);
            m_SelectionInputStates = new NativeList<TStateKey>(1, Allocator.Persistent);
            m_SelectionInputBudgets = new NativeList<int>(1, Allocator.Persistent);
            m_SelectionOutputStateBudgets = new NativeMultiHashMap<TStateKey, int>(1, Allocator.Persistent);

            m_CreatedStateInfoQueue = new NativeQueue<StateTransitionInfoPair<TStateKey, TActionKey, StateTransitionInfo>>(Allocator.Persistent);
            m_CreatedStateInfoList = new NativeList<StateTransitionInfoPair<TStateKey, TActionKey, StateTransitionInfo>>(Allocator.Persistent);

            m_NewStateQueue = new NativeQueue<TStateKey>(Allocator.Persistent);
            m_NewStateQueueParallelWriter = m_NewStateQueue.AsParallelWriter();
            m_NewStateList = new NativeList<TStateKey>(Allocator.Persistent);
            m_StatesToDestroy = new NativeQueue<TStateKey>(Allocator.Persistent);
            m_StatesToDestroyParallelWriter = m_StatesToDestroy.AsParallelWriter();

            m_SelectedStatesByHorizon = new NativeMultiHashMap<int, TStateKey>(1, Allocator.Persistent);
            m_PredecessorStates = new NativeHashMap<TStateKey, byte>(1, Allocator.Persistent);
            m_HorizonStateList = new NativeList<TStateKey>(1, Allocator.Persistent);
        }

        /// <summary>
        /// Schedule jobs for each of the planner stages
        /// </summary>
        /// <param name="inputDeps">Any job dependencies</param>
        /// <param name="forceComplete">Option to force the completion of the previously scheduled planning jobs.</param>
        /// <returns>JobHandle for the scheduled jobs</returns>
        public JobHandle Schedule(JobHandle inputDeps, bool forceComplete = false)
        {
            if (!CurrentJobHandle.IsCompleted && !forceComplete)
                return inputDeps;

            CurrentJobHandle.Complete();

            if (SearchSettings == null)
            {
                CurrentJobHandle = ScheduleSingleIteration(inputDeps);
                return CurrentJobHandle;
            }

            m_FramesSinceLastUpdate++;
            if ((SearchSettings.CapPlanSize && SearchContext.PolicyGraph.Size > SearchSettings.MaxStatesInPlan) ||
                (SearchSettings.StopPlanningWhenToleranceAchieved && SearchContext.RootsConverged(SearchSettings.RootPolicyValueTolerance)) ||
                (SearchSettings.UseCustomSearchFrequency && m_FramesSinceLastUpdate < SearchSettings.FramesPerSearchUpdate))
            {
                CurrentJobHandle = inputDeps;
                return CurrentJobHandle;
            }

            CurrentJobHandle = ScheduleAllIterations(inputDeps);
            return CurrentJobHandle;
        }

        JobHandle ScheduleAllIterations(JobHandle inputDeps)
        {
            m_FramesSinceLastUpdate = 0;

            if (SearchSettings.GraphSelectionJobMode == SelectionJobMode.Parallel ||
                SearchSettings.GraphBackpropagationJobMode == BackpropagationJobMode.Parallel)
            {
                // Find upper bound on number of selection and backpropagation iterations
                m_MaxDepth = 0;
                using (var depths = SearchContext.StateDepthLookup.GetValueArray(Allocator.Temp))
                {
                    for (int i = 0; i < depths.Length; i++)
                    {
                        m_MaxDepth = math.max(m_MaxDepth, depths[i]);
                    }
                }
            }

            var jobHandle = inputDeps;
            for (int scheduleIteration = 0; scheduleIteration < SearchSettings.SearchIterationsPerUpdate; scheduleIteration++)
            {
                jobHandle = ScheduleSingleIteration(jobHandle,
                    scheduleIteration,
                    SearchSettings.StateExpansionBudgetPerIteration,
                    SearchSettings.GraphSelectionJobMode,
                    SearchSettings.GraphBackpropagationJobMode);
            }

            return jobHandle;
        }

        JobHandle ScheduleSingleIteration(JobHandle inputDeps, int scheduleIteration = 0, int budget = 1, SelectionJobMode selectionJobMode = SelectionJobMode.Sequential, BackpropagationJobMode backpropagationJobMode = BackpropagationJobMode.Sequential)
        {
            // todo - other conditions under which not to plan (graph size, convergence to a tolerance, frequency?)
            if (budget <= 0 || SearchContext.PolicyGraph.StateInfoLookup[SearchContext.RootStateKey].SubgraphComplete)
                return inputDeps;

            var clearContainersJobHandle = ScheduleClearContainers(inputDeps);

            var selectionJobHandle = selectionJobMode == SelectionJobMode.Sequential ?
                ScheduleSelectionSequential(clearContainersJobHandle, budget) :
                ScheduleSelectionParallel(clearContainersJobHandle, scheduleIteration, budget);

            var actionsJobHandle = ScheduleActionsJobs(selectionJobHandle);
            var graphExpansionJobHandle = ScheduleExpansionJob(actionsJobHandle);
            var evaluateNewStatesJobHandle = ScheduleEvaluateNewStatesJob(graphExpansionJobHandle);

            var backupJobHandle = backpropagationJobMode == BackpropagationJobMode.Sequential ?
                ScheduleBackpropagationSequential(evaluateNewStatesJobHandle) :
                ScheduleBackpropagationParallel(evaluateNewStatesJobHandle,scheduleIteration);

            return backupJobHandle;
        }

        JobHandle ScheduleSelectionSequential(JobHandle inputDeps, int budget)
        {
            var policyGraph = SearchContext.PolicyGraph;

            var jobHandle = new SelectionJob<TStateKey, TActionKey>()
            {
                SearchBudget = budget,
                RootStateKey = SearchContext.RootStateKey,
                StateDepthLookup = SearchContext.StateDepthLookup,
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

        JobHandle ScheduleSelectionParallel(JobHandle inputDeps, int scheduleIteration, int budget)
        {
            // Setup input containers
            var jobHandle = new SetupParallelSelectionJob<TStateKey>
            {
                RootStateKey = SearchContext.RootStateKey,
                Budget = budget,
                StateInfoLookup = SearchContext.PolicyGraph.StateInfoLookup,

                SelectionInputBudgets = m_SelectionInputBudgets,
                SelectionInputStates = m_SelectionInputStates,
                OutputStateBudgets = m_SelectionOutputStateBudgets,
                SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                AllSelectedStates = m_AllSelectedStates,
            }.Schedule(inputDeps);

            var policyGraph = SearchContext.PolicyGraph;
            for (int iteration = 0; iteration <= m_MaxDepth + scheduleIteration; iteration++)
            {
                // Selection job
                jobHandle = new ParallelSelectionJob<TStateKey, TActionKey>
                {
                    StateDepthLookup = SearchContext.StateDepthLookup,
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
            var actionScheduler = default(TActionScheduler);
            actionScheduler.UnexpandedStates = m_SelectedUnexpandedStatesList;
            actionScheduler.CreatedStateInfo = m_CreatedStateInfoQueue;
            actionScheduler.StateManager = m_StateManager;
            return actionScheduler.Schedule(inputDeps);
        }

        JobHandle ScheduleExpansionJob(JobHandle actionsJobHandle)
        {
            var prepareForExpansionJobHandle = new PrepareForExpansionJob<TStateKey, TActionKey>
            {
                PolicyGraph = SearchContext.PolicyGraph,
                InputStateExpansionInfo = m_CreatedStateInfoQueue,
                OutputStateExpansionInfo = m_CreatedStateInfoList,
                BinnedStateKeys = SearchContext.BinnedStateKeyLookup,
            }.Schedule(actionsJobHandle);

            var policyGraph = SearchContext.PolicyGraph;
            var graphExpansionJobHandle = new GraphExpansionJob<TStateKey, TStateData, TStateDataContext, TActionKey>
            {
                NewStateTransitionInfoPairs = m_CreatedStateInfoList.AsDeferredJobArray(),
                StateDataContext = m_StateManager.GetStateDataContext(),

                ActionLookup = policyGraph.ActionLookup.AsParallelWriter(),
                ActionInfoLookup = policyGraph.ActionInfoLookup.AsParallelWriter(),
                NewStates = m_NewStateQueueParallelWriter,
                StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup.AsParallelWriter(),
                PredecessorGraph = policyGraph.PredecessorGraph.AsParallelWriter(),
                BinnedStateKeys = SearchContext.BinnedStateKeyLookup,
                ResultingStateLookup = policyGraph.ResultingStateLookup.AsParallelWriter(),
                StatesToDestroy = m_StatesToDestroyParallelWriter,
            }.Schedule(m_CreatedStateInfoList, 0, prepareForExpansionJobHandle);

            var destroyStatesScheduler = default(TDestroyStatesScheduler);
            destroyStatesScheduler.StateManager = m_StateManager;
            destroyStatesScheduler.StatesToDestroy = m_StatesToDestroy;
            return destroyStatesScheduler.Schedule(graphExpansionJobHandle);
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

                StateInfoLookup = SearchContext.PolicyGraph.StateInfoLookup.AsParallelWriter(),
                BinnedStateKeys = SearchContext.BinnedStateKeyLookup.AsParallelWriter(),
            }.Schedule(m_NewStateList, 0, newStateQueueToListJobHandle);
        }

        JobHandle ScheduleBackpropagationSequential(JobHandle evaluateNewStatesJobHandle)
        {
            return new BackpropagationJob<TStateKey, TActionKey>
            {
                DepthMap = SearchContext.StateDepthLookup,
                PolicyGraph = SearchContext.PolicyGraph,
                SelectedStates = m_AllSelectedStates,
                DiscountFactor = m_DiscountFactor,
            }.Schedule(evaluateNewStatesJobHandle);
        }

        JobHandle ScheduleBackpropagationParallel(JobHandle evaluateNewStatesJobHandle, int scheduleIteration)
        {
            var jobHandle = new UpdateDepthMapAndResizeContainersJob<TStateKey>
            {
                SelectedStates = m_AllSelectedStates,
                MaxDepth = m_MaxDepth + scheduleIteration,

                DepthMap = SearchContext.StateDepthLookup,
                SelectedStatesByHorizon = m_SelectedStatesByHorizon,
                PredecessorStates = m_PredecessorStates,
                HorizonStateList = m_HorizonStateList,
            }.Schedule(evaluateNewStatesJobHandle);

            // Schedule maxDepth iterations of backpropagation
            var policyGraph = SearchContext.PolicyGraph;
            for (int horizon = m_MaxDepth + scheduleIteration + 1; horizon >= 0; horizon--)
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
                    DiscountFactor = m_DiscountFactor,

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

        JobHandle ScheduleClearContainers(JobHandle inputDeps)
        {
            return new ClearContainersJob
            {
                AllSelectedStates = m_AllSelectedStates,
                SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                SelectedUnexpandedStatesList = m_SelectedUnexpandedStatesList,
                SelectionInputStates = m_SelectionInputStates,
                SelectionInputBudgets = m_SelectionInputBudgets,
                SelectionOutputStateBudgets = m_SelectionOutputStateBudgets,
                CreatedStateInfoList = m_CreatedStateInfoList,
                NewStateList = m_NewStateList,
                SelectedStatesByHorizon = m_SelectedStatesByHorizon,
                PredecessorStates = m_PredecessorStates,
                HorizonStateList = m_HorizonStateList,
            }.Schedule(inputDeps);
        }

        /// <summary>
        /// Dispose of planner scheduler instance
        /// </summary>
        public void Dispose()
        {
            CurrentJobHandle.Complete();

            SearchContext.Dispose();
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

        struct ClearContainersJob : IJob
        {
            public NativeMultiHashMap<TStateKey, int> AllSelectedStates;
            public NativeHashMap<TStateKey, byte> SelectedUnexpandedStates;
            public NativeList<TStateKey> SelectedUnexpandedStatesList;
            public NativeList<StateTransitionInfoPair<TStateKey, TActionKey, StateTransitionInfo>> CreatedStateInfoList;
            public NativeList<TStateKey> NewStateList;
            public NativeList<TStateKey> SelectionInputStates;
            public NativeList<int> SelectionInputBudgets;
            public NativeMultiHashMap<TStateKey, int> SelectionOutputStateBudgets;
            public NativeMultiHashMap<int, TStateKey> SelectedStatesByHorizon;
            public NativeHashMap<TStateKey, byte> PredecessorStates;
            public NativeList<TStateKey> HorizonStateList;

            public void Execute()
            {
                AllSelectedStates.Clear();
                SelectedUnexpandedStates.Clear();
                SelectedUnexpandedStatesList.Clear();
                SelectionInputStates.Clear();
                SelectionInputBudgets.Clear();
                SelectionOutputStateBudgets.Clear();
                CreatedStateInfoList.Clear();
                NewStateList.Clear();
                SelectedStatesByHorizon.Clear();
                PredecessorStates.Clear();
                HorizonStateList.Clear();
            }
        }
    }

}
