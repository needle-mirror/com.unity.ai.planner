using System;
using Unity.AI.Planner.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI.Planner;
using UnityEngine.Assertions;

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
        IPlanRequest IPlannerScheduler.CurrentPlanRequest => CurrentPlanRequest;
        PlanRequest<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> CurrentPlanRequest { get; set; }

        /// <inheritdoc />
        public JobHandle CurrentJobHandle
        {
            get => m_SearchContext?.m_PlanningJobHandle ?? default;
            private set => m_SearchContext.m_PlanningJobHandle = value;
        }

        internal SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> m_SearchContext;

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
        NativeList<TStateKey> m_NewStateList;
        NativeList<TStateKey> m_SelectionInputStates;
        NativeList<int> m_SelectionInputBudgets;
        NativeMultiHashMap<TStateKey, int> m_SelectionOutputStateBudgets;
        NativeMultiHashMap<int, TStateKey> m_SelectedStatesByHorizon;
        NativeHashMap<TStateKey, byte> m_PredecessorStates;
        NativeList<TStateKey> m_HorizonStateList;
        NativeQueue<TStateKey> m_NewStateQueue;
        NativeQueue<TStateKey>.ParallelWriter m_NewStateQueueParallelWriter;
        NativeQueue<TStateKey> m_StatesToDestroy;
        NativeQueue<TStateKey>.ParallelWriter m_StatesToDestroyParallelWriter;
        NativeQueue<StateHorizonPair<TStateKey>> m_GraphTraversalQueue;

        // Data for job scheduling
        int m_MaxDepth;

        /// <summary>
        /// Initialize a planner scheduler instance
        /// </summary>
        /// <param name="stateManager">StateManager instance</param>
        /// <param name="heuristic">Heuristic</param>
        /// <param name="terminationEvaluator">State termination evaluator</param>
        /// <param name="discountFactor">Multiplicative factor ([0 -> 1]) for discounting future rewards.</param>
        public void Initialize(TStateManager stateManager, THeuristic heuristic = default, TTerminationEvaluator terminationEvaluator = default, float discountFactor = 0.95f)
        {
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
            m_GraphTraversalQueue = new NativeQueue<StateHorizonPair<TStateKey>>(Allocator.Persistent);

            m_SelectedStatesByHorizon = new NativeMultiHashMap<int, TStateKey>(1, Allocator.Persistent);
            m_PredecessorStates = new NativeHashMap<TStateKey, byte>(1, Allocator.Persistent);
            m_HorizonStateList = new NativeList<TStateKey>(1, Allocator.Persistent);
        }

        public IPlanRequest RequestPlan(IStateKey rootState, Action<IPlan> onRequestComplete = null, PlannerSearchSettings searchSettings = null)
        {
            if (!(rootState is TStateKey stateKey))
                throw new ArgumentException($"Expected state key of type {typeof(TStateKey)}. Received key of type {rootState?.GetType()}");

            return RequestPlan(stateKey, onRequestComplete, searchSettings);
        }

        public void UpdatePlanRequestRootState(IStateKey newRootState)
        {
            if (!(newRootState is TStateKey stateKey))
                throw new ArgumentException($"Expected state key of type {typeof(TStateKey)}. Received key of type {newRootState?.GetType()}");

            UpdatePlanRequestRootState(stateKey);
        }

        public void UpdatePlanRequestRootState(TStateKey stateKey)
        {
            var oldSearchContext = m_SearchContext;
            var oldPolicyGraph = m_SearchContext.PolicyGraph;
            var newSearchContext = new SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>(m_StateManager,
                oldPolicyGraph.StateInfoLookup.Count(),
                oldPolicyGraph.ActionInfoLookup.Count(),
                oldPolicyGraph.StateTransitionInfoLookup.Count());

            // Replace old search context
            CurrentPlanRequest.m_Plan.m_SearchContext = newSearchContext;
            m_SearchContext = newSearchContext;

            // Check for existence of state
            var rootFoundInPlan = oldSearchContext.FindMatchingStateInPlan(stateKey, out var planStateKey);
            var newRoot =  rootFoundInPlan ? planStateKey : m_StateManager.CopyState(stateKey);

            // Check if we can skip copying the subgraph from the new root
            if (!rootFoundInPlan)
            {
                newSearchContext.UpdateRootState(newRoot); // Sets new state info
                oldSearchContext.Dispose(CurrentJobHandle); // no data to copy
                return;
            }

            // Set root
            newSearchContext.RootStateKey = newRoot;

            var statesToCopy = new NativeList<TStateKey>(oldPolicyGraph.Size, Allocator.TempJob);
            var statesToCopyLookup = new NativeHashMap<TStateKey, byte>(oldPolicyGraph.Size, Allocator.TempJob);
            var stateKeys = oldPolicyGraph.StateInfoLookup.GetKeyArray(Allocator.TempJob);
            var jobHandle = CurrentJobHandle;

            jobHandle = new CollectReachableStatesJob()
            {
                RootStateKey = newRoot,
                PolicyGraph = oldPolicyGraph,
                StateKeys = stateKeys,

                ReachableStateDepthMap = newSearchContext.StateDepthLookup,
                StatesToCopy = statesToCopy,
                StatesToCopyLookup = statesToCopyLookup,
                StatesToDestroy = m_StatesToDestroy,
                GraphTraversalQueue = m_GraphTraversalQueue,
            }.Schedule(jobHandle);

            jobHandle = new CopySearchDataJob()
            {
                StatesToCopy = statesToCopy.AsDeferredJobArray(),
                StatesToCopyLookup = statesToCopyLookup,

                SourcePolicyGraph = oldPolicyGraph,

                PolicyGraph = newSearchContext.PolicyGraph.AsParallelWriter(),
                BinnedStateKeyLookup = newSearchContext.BinnedStateKeyLookup.AsParallelWriter(),
            }.Schedule(statesToCopy, default, jobHandle);

            // Destroy states
            var destroyStatesScheduler = default(TDestroyStatesScheduler);
            destroyStatesScheduler.StateManager = m_StateManager;
            destroyStatesScheduler.StatesToDestroy = m_StatesToDestroy;
            jobHandle = destroyStatesScheduler.Schedule(jobHandle);

            // Dispose temp containers and old search context
            statesToCopy.Dispose(jobHandle);
            statesToCopyLookup.Dispose(jobHandle);
            stateKeys.Dispose(jobHandle);
            oldSearchContext.Dispose(jobHandle);

            // Update job handle
            CurrentJobHandle = JobHandle.CombineDependencies(jobHandle, CurrentJobHandle);
        }

        [BurstCompile]
        struct CollectReachableStatesJob : IJob
        {
            // Info for search
            public TStateKey RootStateKey;
            public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> PolicyGraph;
            public NativeArray<TStateKey> StateKeys;

            public NativeHashMap<TStateKey, int> ReachableStateDepthMap;
            public NativeQueue<TStateKey> StatesToDestroy;
            public NativeList<TStateKey> StatesToCopy;
            public NativeHashMap<TStateKey, byte> StatesToCopyLookup;
            public NativeQueue<StateHorizonPair<TStateKey>>  GraphTraversalQueue;

            public void Execute()
            {
                StatesToDestroy.Clear();
                StatesToCopy.Clear();
                ReachableStateDepthMap.Clear();

                PolicyGraph.GetReachableDepthMap(RootStateKey, ReachableStateDepthMap, GraphTraversalQueue);

                for (int i = 0; i < StateKeys.Length; i++)
                {
                    var stateKey = StateKeys[i];
                    if (ReachableStateDepthMap.ContainsKey(stateKey))
                    {
                        StatesToCopy.Add(stateKey);
                        StatesToCopyLookup.Add(stateKey, 0);
                    }
                    else
                        StatesToDestroy.Enqueue(stateKey);
                }

                Assert.AreEqual(PolicyGraph.StateInfoLookup.Count(), StatesToCopy.Length + StatesToDestroy.Count);
            }
        }

        [BurstCompile]
        struct CopySearchDataJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<TStateKey> StatesToCopy;
            [ReadOnly] public NativeHashMap<TStateKey, byte> StatesToCopyLookup;

            [ReadOnly] public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> SourcePolicyGraph;

            [WriteOnly] public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo>.ParallelWriter PolicyGraph;
            [WriteOnly] public NativeMultiHashMap<int, TStateKey>.ParallelWriter BinnedStateKeyLookup;

            public void Execute(int index)
            {
                var stateKey = StatesToCopy[index];

                // Search info
                BinnedStateKeyLookup.Add(stateKey.GetHashCode(), stateKey);

                // State info
                PolicyGraph.StateInfoLookup.TryAdd(stateKey, SourcePolicyGraph.StateInfoLookup[stateKey]);

                // Action info
                if (SourcePolicyGraph.ActionLookup.TryGetFirstValue(stateKey, out var actionKey, out var actionIterator))
                {
                    do
                    {
                        var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                        PolicyGraph.ActionLookup.Add(stateKey, actionKey);
                        PolicyGraph.ActionInfoLookup.TryAdd(stateActionPair, SourcePolicyGraph.ActionInfoLookup[stateActionPair]);

                        // Transition info
                        if (SourcePolicyGraph.ResultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingState, out var transitionIterator))
                        {
                            do
                            {
                                var transition = new StateTransition<TStateKey, TActionKey>(stateActionPair, resultingState);
                                PolicyGraph.ResultingStateLookup.Add(stateActionPair, resultingState);
                                PolicyGraph.StateTransitionInfoLookup.TryAdd(transition, SourcePolicyGraph.StateTransitionInfoLookup[transition]);
                            } while (SourcePolicyGraph.ResultingStateLookup.TryGetNextValue(out resultingState, ref transitionIterator));
                        }
                    } while (SourcePolicyGraph.ActionLookup.TryGetNextValue(out actionKey, ref actionIterator));
                }

                // Predecessor links
                if (SourcePolicyGraph.PredecessorGraph.TryGetFirstValue(stateKey, out var predecessor, out var predecessorIterator))
                {
                    do
                    {
                        if (StatesToCopyLookup.ContainsKey(predecessor))
                            PolicyGraph.PredecessorGraph.Add(stateKey, predecessor);
                    } while (SourcePolicyGraph.PredecessorGraph.TryGetNextValue(out predecessor, ref predecessorIterator));
                }
            }
        }

        public IPlanRequest RequestPlan(TStateKey rootState, Action<IPlan> onRequestComplete = null, PlannerSearchSettings searchSettings = null)
        {
            PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> plan;
            if (CurrentPlanRequest == null)
            {
                m_SearchContext = new SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>(m_StateManager, 10, 10);
                plan = new PlanWrapper<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>(m_SearchContext);
            }
            else
            {
                plan = CurrentPlanRequest.m_Plan; //todo reusing the plan can cause side effects if the graph is pruned for a new request while an agent is still enacting the previous plan
                CurrentPlanRequest.Dispose();
            }

            CurrentJobHandle.Complete();
            m_SearchContext.UpdateRootState(rootState);

            CurrentPlanRequest = new PlanRequest<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>(plan, searchSettings, onRequestComplete);

            return CurrentPlanRequest;
        }

        /// <summary>
        /// Schedule jobs for each of the planner stages
        /// </summary>
        /// <param name="inputDeps">Any job dependencies</param>
        /// <param name="forceComplete">Option to force the completion of the previously scheduled planning jobs.</param>
        /// <returns>JobHandle for the scheduled jobs</returns>
        public JobHandle Schedule(JobHandle inputDeps, bool forceComplete = false)
        {
            // Only schedule jobs if there is an active plan request
            if (CurrentPlanRequest?.m_Plan == null)
                return inputDeps;

            // FIXME: The job system currently doesn't allow long-running jobs, so warnings spew about permitted lifetime,
            // which is currently set at 4 frames. Force a complete if a job hasn't completed yet after 4 frames.
            const int kPermittedLifetime = 4;

            // Force complete every 4 frames (TempJob allocation constraint)
            var framesSinceComplete = m_SearchContext.m_FramesSinceComplete;
            framesSinceComplete = ++framesSinceComplete % kPermittedLifetime;
            m_SearchContext.m_FramesSinceComplete = framesSinceComplete;
            if (CurrentJobHandle.IsCompleted || framesSinceComplete == 0 || forceComplete)
            {
                CurrentJobHandle.Complete();
                m_SearchContext.m_FramesSinceComplete = 0;
            }

            var searchSettings = CurrentPlanRequest.m_SearchSettings;
            CurrentPlanRequest.m_FramesSinceLastUpdate++;

            // Check if any update should occur
            if (!CurrentJobHandle.IsCompleted || CurrentPlanRequest.Status != PlanRequestStatus.Running
                || (searchSettings.UseCustomSearchFrequency && CurrentPlanRequest.m_FramesSinceLastUpdate < searchSettings.MinFramesPerSearchUpdate))
                return inputDeps;

            // Check if plan request termination condition(s) have been met
            if ((m_SearchContext.PolicyGraph.StateInfoLookup.TryGetValue(m_SearchContext.RootStateKey, out var rootInfo) && rootInfo.SubgraphComplete) ||
                (searchSettings.CapPlanUpdates && CurrentPlanRequest.m_PlanningUpdates >= searchSettings.MaxUpdates) ||
                (searchSettings.CapPlanSize && m_SearchContext.PolicyGraph.Size > searchSettings.MaxStatesInPlan) ||
                (searchSettings.StopPlanningWhenToleranceAchieved && m_SearchContext.RootsConverged(searchSettings.RootPolicyValueTolerance)) )
            {
                CurrentPlanRequest.Status = PlanRequestStatus.Complete;
                CurrentPlanRequest.m_OnPlanRequestComplete?.Invoke(CurrentPlanRequest.Plan);

                CurrentJobHandle = default;
                return inputDeps;
            }

            // Increment update count
            CurrentPlanRequest.m_PlanningUpdates++;
            CurrentPlanRequest.m_FramesSinceLastUpdate = 0;
            CurrentJobHandle = ScheduleAllIterations(inputDeps, searchSettings);

            return CurrentJobHandle;
        }

        JobHandle ScheduleAllIterations(JobHandle inputDeps, PlannerSearchSettings searchSettings)
        {
            if (searchSettings.GraphSelectionJobMode == SelectionJobMode.Parallel ||
                searchSettings.GraphBackpropagationJobMode == BackpropagationJobMode.Parallel)
            {
                // Find upper bound on number of selection and backpropagation iterations
                m_MaxDepth = 0;
                using (var depths = m_SearchContext.StateDepthLookup.GetValueArray(Allocator.Temp))
                {
                    for (int i = 0; i < depths.Length; i++)
                    {
                        m_MaxDepth = math.max(m_MaxDepth, depths[i]);
                    }
                }
            }

            var jobHandle = inputDeps;
            for (int scheduleIteration = 0; scheduleIteration < searchSettings.SearchIterationsPerUpdate; scheduleIteration++)
            {
                jobHandle = ScheduleSingleIteration(jobHandle,
                    scheduleIteration,
                    searchSettings.StateExpansionBudgetPerIteration,
                    searchSettings.GraphSelectionJobMode,
                    searchSettings.GraphBackpropagationJobMode);
            }

            return jobHandle;
        }

        JobHandle ScheduleSingleIteration(JobHandle inputDeps, int scheduleIteration = 0, int budget = 1, SelectionJobMode selectionJobMode = SelectionJobMode.Sequential, BackpropagationJobMode backpropagationJobMode = BackpropagationJobMode.Sequential)
        {
            if (budget < 1)
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

        JobHandle ScheduleSelectionParallel(JobHandle inputDeps, int scheduleIteration, int budget)
        {
            // Setup input containers
            var jobHandle = new SetupParallelSelectionJob<TStateKey>
            {
                RootStateKey = m_SearchContext.RootStateKey,
                Budget = budget,
                StateInfoLookup = m_SearchContext.PolicyGraph.StateInfoLookup,

                SelectionInputBudgets = m_SelectionInputBudgets,
                SelectionInputStates = m_SelectionInputStates,
                OutputStateBudgets = m_SelectionOutputStateBudgets,
                SelectedUnexpandedStates = m_SelectedUnexpandedStates,
                AllSelectedStates = m_AllSelectedStates,
            }.Schedule(inputDeps);

            var policyGraph = m_SearchContext.PolicyGraph;
            for (int iteration = 0; iteration <= m_MaxDepth + scheduleIteration; iteration++)
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
                PolicyGraph = m_SearchContext.PolicyGraph,
                InputStateExpansionInfo = m_CreatedStateInfoQueue,
                OutputStateExpansionInfo = m_CreatedStateInfoList,
                BinnedStateKeys = m_SearchContext.BinnedStateKeyLookup,
            }.Schedule(actionsJobHandle);

            var policyGraph = m_SearchContext.PolicyGraph;
            var graphExpansionJobHandle = new GraphExpansionJob<TStateKey, TStateData, TStateDataContext, TActionKey>
            {
                NewStateTransitionInfoPairs = m_CreatedStateInfoList.AsDeferredJobArray(),
                StateDataContext = m_StateManager.GetStateDataContext(),

                ActionLookup = policyGraph.ActionLookup.AsParallelWriter(),
                ActionInfoLookup = policyGraph.ActionInfoLookup.AsParallelWriter(),
                NewStates = m_NewStateQueueParallelWriter,
                StateTransitionInfoLookup = policyGraph.StateTransitionInfoLookup.AsParallelWriter(),
                PredecessorGraph = policyGraph.PredecessorGraph.AsParallelWriter(),
                BinnedStateKeys = m_SearchContext.BinnedStateKeyLookup,
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
                DiscountFactor = m_DiscountFactor,
            }.Schedule(evaluateNewStatesJobHandle);
        }

        JobHandle ScheduleBackpropagationParallel(JobHandle evaluateNewStatesJobHandle, int scheduleIteration)
        {
            var jobHandle = new UpdateDepthMapAndResizeContainersJob<TStateKey>
            {
                SelectedStates = m_AllSelectedStates,
                MaxDepth = m_MaxDepth + scheduleIteration,

                DepthMap = m_SearchContext.StateDepthLookup,
                SelectedStatesByHorizon = m_SelectedStatesByHorizon,
                PredecessorStates = m_PredecessorStates,
                HorizonStateList = m_HorizonStateList,
            }.Schedule(evaluateNewStatesJobHandle);

            // Schedule maxDepth iterations of backpropagation
            var policyGraph = m_SearchContext.PolicyGraph;
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

            m_SearchContext?.Dispose();
            CurrentPlanRequest?.Dispose();

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
            m_GraphTraversalQueue.Dispose();
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
