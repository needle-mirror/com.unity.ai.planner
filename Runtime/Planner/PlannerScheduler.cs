using System;
using System.Collections.Generic;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Jobs;


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
    public class PlannerScheduler<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext, TActionScheduler, THeuristic, TTerminationEvaluator> : IDisposable
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where TActionScheduler : IActionScheduler<TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey, ActionResult>
        where THeuristic : struct, IHeuristic<TStateData>
        where TTerminationEvaluator : struct, ITerminationEvaluator<TStateData>
    {
        TStateManager m_StateManager;
        TActionScheduler m_ActionScheduler;
        THeuristic m_Heuristic;
        TTerminationEvaluator m_TerminationEvaluator;

        // FIXME: Make private and/or replace with interface property (i.e. IPlan) -- see CoffeeRobot project
        internal SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext> SearchContext;

        // Containers for jobs
        NativeList<TStateKey> m_AllSelectedStates;
        NativeList<TStateKey> m_SelectedUnexpandedStates;
        NativeArray<TStateKey> m_StateKeyArray;

        NativeQueue<(TStateKey, TActionKey, ActionResult, TStateKey)> m_CreatedStateInfoQueue;
        NativeList<(TStateKey, TActionKey, ActionResult, TStateKey)> m_CreatedStateInfoList;

        NativeQueue<TStateKey> m_NewStateQueue;
        NativeList<TStateKey> m_NewStateList;
        NativeQueue<TStateKey> m_StatesToDestroy;

        internal IEnumerator<JobHandle> ScheduleWithYield(JobHandle inputDeps)
        {
            ClearContainers();
            var policyGraph = SearchContext.PolicyGraph;
            var actionResultLookup = policyGraph.ActionResultLookup;
            var stateInfoLookup = policyGraph.StateInfoLookup;

            if (m_StateKeyArray.IsCreated)
                m_StateKeyArray.Dispose();

            m_StateKeyArray = stateInfoLookup.GetKeyArray(Allocator.Persistent); //fixme dispose?

            var selectionJobHandle = ScheduleSelectionJob(inputDeps);
            yield return selectionJobHandle;

            var actionsJobHandle = ScheduleActionsJobs(selectionJobHandle);
            yield return actionsJobHandle;

            var graphExpansionJobHandle = ScheduleExpansionJob(policyGraph, actionsJobHandle, actionResultLookup, m_StateKeyArray);
            yield return graphExpansionJobHandle;
            // todo after (or during) the stateLookupJob, we need to dispose of statekeys/data for duplicate states we are discarding (on state data context)

            var evaluateNewStatesJobHandle = ScheduleEvaluateNewStatesJob(graphExpansionJobHandle, stateInfoLookup);
            yield return evaluateNewStatesJobHandle;

            var backupJobHandle = ScheduleBackpropagationJob(policyGraph, evaluateNewStatesJobHandle);
            yield return backupJobHandle;
        }

        /// <summary>
        /// Schedule jobs for each of the planner stages
        /// </summary>
        /// <param name="inputDeps">Any job dependencies</param>
        /// <returns>JobHandle for the scheduled jobs</returns>
        public JobHandle Schedule(JobHandle inputDeps)
        {
            // todo - better disposal of unneeded state entities
            while (m_StatesToDestroy.TryDequeue(out var stateKey))
            {
                m_StateManager.DestroyState(stateKey);
            }

            ClearContainers();
            var policyGraph = SearchContext.PolicyGraph;
            var actionResultLookup = policyGraph.ActionResultLookup;
            var stateInfoLookup = policyGraph.StateInfoLookup;

            if (m_StateKeyArray.IsCreated)
                m_StateKeyArray.Dispose();

            m_StateKeyArray = stateInfoLookup.GetKeyArray(Allocator.Persistent); //fixme dispose?

            var selectionJobHandle = ScheduleSelectionJob(inputDeps);
            var actionsJobHandle = ScheduleActionsJobs(selectionJobHandle);

            var graphExpansionJobHandle = ScheduleExpansionJob(policyGraph, actionsJobHandle, actionResultLookup, m_StateKeyArray);
            // todo after (or during) the stateLookupJob, we need to dispose of statekeys/data for duplicate states we are discarding (on state data context)

            var evaluateNewStatesJobHandle = ScheduleEvaluateNewStatesJob(graphExpansionJobHandle, stateInfoLookup);
            var backupJobHandle = ScheduleBackpropagationJob(policyGraph, evaluateNewStatesJobHandle);

            return backupJobHandle;
        }

        internal void Run()
        {
            ClearContainers();
            var policyGraph = SearchContext.PolicyGraph;
            var actionResultLookup = policyGraph.ActionResultLookup;
            var stateInfoLookup = policyGraph.StateInfoLookup;

            if (m_StateKeyArray.IsCreated)
                m_StateKeyArray.Dispose();

            m_StateKeyArray = stateInfoLookup.GetKeyArray(Allocator.Persistent); //fixme dispose?

            CreateSelectionJob().Run();
            CreateActionJobs().Schedule(default).Complete();

            CreatePrepareForExpansionJob(policyGraph).Run();
            CreateExpansionJob(policyGraph, actionResultLookup, m_StateKeyArray).Schedule(m_CreatedStateInfoList, m_CreatedStateInfoList.Length).Complete();
            // todo after (or during) the stateLookupJob, we need to dispose of statekeys/data for duplicate states we are discarding (on state data context)

            CreatePrepareForEvaluateNewStatesJob().Run();
            CreateEvaluateNewStatesJob(stateInfoLookup).Schedule(m_NewStateList, m_NewStateList.Length).Complete();
            CreateBackpropagationJob(policyGraph).Run();
        }

        SelectionJob<TStateKey, TActionKey> CreateSelectionJob()
        {
            var policyGraph = SearchContext.PolicyGraph;
            return new SelectionJob<TStateKey, TActionKey>()
            {
                RootStateKey = SearchContext.RootStateKey,
                StateDepthLookup = SearchContext.StateDepthLookup,
                StateInfoLookup = policyGraph.StateInfoLookup,
                StateActionLookup = policyGraph.StateActionLookup,
                ActionInfoLookup = policyGraph.ActionInfoLookup,
                ResultingStateLookup = policyGraph.ResultingStateLookup,
                ActionResultLookup = policyGraph.ActionResultLookup,

                AllSelectedStates = m_AllSelectedStates,
                SelectedUnexpandedStates = m_SelectedUnexpandedStates,
            };
        }

        BackpropagationJob<TStateKey, TActionKey> CreateBackpropagationJob(PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> policyGraph)
        {
            return new BackpropagationJob<TStateKey, TActionKey>()
            {
                PolicyGraph = policyGraph,
                DepthMap = SearchContext.StateDepthLookup,
                SelectedStates = m_AllSelectedStates
            };
        }

        TActionScheduler CreateActionJobs()
        {
            // FIXME: Currently DOTS requires a JobComponentSystem in order to get buffers off of, so unfortunately we
            // can't use a struct for this part yet; The goal is for this scheduler code to remain agnostic to implementation
            m_ActionScheduler.UnexpandedStates = m_SelectedUnexpandedStates;
            m_ActionScheduler.CreatedStateInfo = m_CreatedStateInfoQueue;
            m_ActionScheduler.StateManager = m_StateManager;
            return m_ActionScheduler;
        }

        PrepareForExpansionJob<TStateKey, TActionKey> CreatePrepareForExpansionJob(
            PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> policyGraph)
        {
            return new PrepareForExpansionJob<TStateKey, TActionKey>()
            {
                PolicyGraph = policyGraph,
                InputStateExpansionInfo = m_CreatedStateInfoQueue,
                OutputStateExpansionInfo = m_CreatedStateInfoList,
            };
        }

        GraphExpansionJob<TStateKey, TStateData, TStateDataContext, TActionKey> CreateExpansionJob(
            PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> policyGraph,
            NativeHashMap<(TStateKey, TActionKey, TStateKey), ActionResult> actionResultLookup,
            NativeArray<TStateKey> stateKeyArray)
        {
            return new GraphExpansionJob<TStateKey, TStateData, TStateDataContext, TActionKey>
            {
                NewStateTransitionInfo = m_CreatedStateInfoList.AsDeferredJobArray(),
                StateDataContext = m_StateManager.GetStateDataContext(),

                StateActionLookup = policyGraph.StateActionLookup.AsParallelWriter(),
                ActionInfoLookup = policyGraph.ActionInfoLookup.AsParallelWriter(),
                NewStates = m_NewStateQueue.AsParallelWriter(),
                ActionResultLookup = actionResultLookup.AsParallelWriter(),
                PredecessorGraph = policyGraph.PredecessorGraph.AsParallelWriter(),
                ExistingStateKeys = stateKeyArray,
                ActionToStateLookup = policyGraph.ResultingStateLookup.AsParallelWriter(),
                StatesToDestroy = m_StatesToDestroy.AsParallelWriter(),
            };
        }

        QueueToListJob<TStateKey> CreatePrepareForEvaluateNewStatesJob()
        {
            return new QueueToListJob<TStateKey>()
            {
                InputQueue = m_NewStateQueue,
                OutputList = m_NewStateList,
            };
        }

        EvaluateNewStatesJob<TStateKey, TStateData, TStateDataContext, THeuristic, TTerminationEvaluator> CreateEvaluateNewStatesJob(NativeHashMap<TStateKey, StateInfo> stateInfoLookup)
        {
            return new EvaluateNewStatesJob<TStateKey, TStateData, TStateDataContext, THeuristic, TTerminationEvaluator>
            {
                States = m_NewStateList.AsDeferredJobArray(),
                StateDataContext = m_StateManager.GetStateDataContext(), // todo this can be the same data context as used in the lookup job
                Heuristic = m_Heuristic,
                TerminationEvaluator = m_TerminationEvaluator,

                StateInfoLookup = stateInfoLookup.AsParallelWriter(),
            };
        }

        JobHandle ScheduleBackpropagationJob(PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> policyGraph,
            JobHandle evaluateNewStatesJobHandle)
        {
            JobHandle backupJobHandle = CreateBackpropagationJob(policyGraph).Schedule(evaluateNewStatesJobHandle);
            return backupJobHandle;
        }

        JobHandle ScheduleSelectionJob(JobHandle inputDeps)
        {
            var selectionJob = CreateSelectionJob();
            return selectionJob.Schedule(inputDeps);
        }

        JobHandle ScheduleActionsJobs(JobHandle inputDeps)
        {
            var actionJobsHandle = CreateActionJobs();
            return actionJobsHandle.Schedule(inputDeps);
        }

        JobHandle ScheduleExpansionJob(PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> policyGraph,
            JobHandle actionsJobHandle, NativeHashMap<(TStateKey, TActionKey, TStateKey), ActionResult> actionResultLookup,
            NativeArray<TStateKey> stateKeyArray)
        {
            var prepareForExpansionJob = CreatePrepareForExpansionJob(policyGraph);
            var prepareForExpansionJobHandle = prepareForExpansionJob.Schedule(actionsJobHandle);

            var graphExpansionJob = CreateExpansionJob(policyGraph, actionResultLookup, stateKeyArray);
            return graphExpansionJob.Schedule(m_CreatedStateInfoList, 0, prepareForExpansionJobHandle);
        }

        JobHandle ScheduleEvaluateNewStatesJob(JobHandle graphExpansionJob, NativeHashMap<TStateKey, StateInfo> stateInfoLookup)
        {
            var newStateQueueToListJob = CreatePrepareForEvaluateNewStatesJob();
            var newStateQueueToListJobHandle = newStateQueueToListJob.Schedule(graphExpansionJob);

            var evaluateNewStatesJobHandle = CreateEvaluateNewStatesJob(stateInfoLookup).Schedule(m_NewStateList, 0, newStateQueueToListJobHandle);
            return evaluateNewStatesJobHandle;
        }

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
        public void Initialize(TStateKey rootStateKey, TStateManager stateManager, TActionScheduler actionScheduler, THeuristic heuristic,
            TTerminationEvaluator terminationEvaluator, int stateCapacity = 1, int actionCapacity = 1)
        {
            SearchContext = new SearchContext<TStateKey, TActionKey, TStateManager, TStateData, TStateDataContext>(rootStateKey, stateManager, stateCapacity, actionCapacity);
            m_StateManager = stateManager;
            m_ActionScheduler = actionScheduler;
            m_Heuristic = heuristic;
            m_TerminationEvaluator = terminationEvaluator;

            m_AllSelectedStates = new NativeList<TStateKey>(Allocator.Persistent); //fixme dispose?
            m_SelectedUnexpandedStates = new NativeList<TStateKey>(Allocator.Persistent);
            m_CreatedStateInfoQueue = new NativeQueue<(TStateKey, TActionKey, ActionResult, TStateKey)>(Allocator.Persistent); //fixme dispose?
            m_CreatedStateInfoList = new NativeList<(TStateKey, TActionKey, ActionResult, TStateKey)>(Allocator.Persistent); //fixme dispose?
            m_NewStateQueue = new NativeQueue<TStateKey>(Allocator.Persistent); //fixme dispose?
            m_NewStateList = new NativeList<TStateKey>(Allocator.Persistent); //fixme dispose?
            m_StatesToDestroy = new NativeQueue<TStateKey>(Allocator.Persistent);
        }

        void ClearContainers()
        {
            m_AllSelectedStates.Clear();
            m_SelectedUnexpandedStates.Clear();
            m_CreatedStateInfoQueue.Clear();
            m_CreatedStateInfoList.Clear();
            m_NewStateQueue.Clear();
            m_NewStateList.Clear();
            m_StatesToDestroy.Clear();
        }

        /// <summary>
        /// Dispose of planner scheduler instance
        /// </summary>
        public void Dispose()
        {
            SearchContext.Dispose();

            m_AllSelectedStates.Dispose();
            m_SelectedUnexpandedStates.Dispose();
            m_CreatedStateInfoQueue.Dispose();
            m_CreatedStateInfoList.Dispose();
            m_NewStateQueue.Dispose();
            m_NewStateList.Dispose();
            m_StateKeyArray.Dispose();
            m_StatesToDestroy.Dispose();
        }
    }

}
