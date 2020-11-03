using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.AI.Planner.Jobs
{
    /// <summary>
    /// Modes designating the strategy for computing updated decision estimated reward values during backpropagation.
    /// </summary>
    public enum BackpropagationJobMode
    {
        /// <summary>
        /// All values will be updated in a job by a single worker.
        /// </summary>
        Sequential,

        /// <summary>
        /// Values will be updated in a parallel job by multiple workers.
        /// </summary>
        Parallel
    }

    [BurstCompile]
    struct BackpropagationJob<TStateKey, TActionKey> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        [ReadOnly] public float DiscountFactor;
        [ReadOnly] public NativeMultiHashMap<TStateKey, int> SelectedStates;

        public NativeHashMap<TStateKey, int> DepthMap;
        public PlanGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> planGraph;

        public void Execute()
        {
            var statesToUpdateLength = SelectedStates.Count();
            var statesToUpdate = new NativeMultiHashMap<int, TStateKey>(statesToUpdateLength, Allocator.Temp);
            var currentHorizon = new NativeList<TStateKey>(statesToUpdateLength, Allocator.Temp);
            var nextHorizon = new NativeList<TStateKey>(statesToUpdateLength, Allocator.Temp);

            var maxDepth = int.MinValue;
            var selectedStateKeys = SelectedStates.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < selectedStateKeys.Length; i++)
            {
                var stateKey = selectedStateKeys[i];
                int stateDepth = int.MinValue;
                SelectedStates.TryGetFirstValue(stateKey, out var selectedDepth, out var iterator);
                do
                    stateDepth = math.max(stateDepth, selectedDepth);
                while (SelectedStates.TryGetNextValue(out selectedDepth, ref iterator));

                // Update depth map
                DepthMap[stateKey] = stateDepth;

                // Queue state and track max depth of backpropagation
                statesToUpdate.AddValueIfUnique(stateDepth, stateKey);
                maxDepth = math.max(maxDepth, stateDepth);
            }
            selectedStateKeys.Dispose();

            var actionLookup = planGraph.ActionLookup;
            var resultingStateLookup = planGraph.ResultingStateLookup;
            var actionInfoLookup = planGraph.ActionInfoLookup;
            var stateInfoLookup = planGraph.StateInfoLookup;
            var stateTransitionInfoLookup = planGraph.StateTransitionInfoLookup;
            var predecessorLookup = planGraph.PredecessorGraph;
            var depth = maxDepth;

            // Pull states from statesToUpdate
            if (statesToUpdate.TryGetFirstValue(depth, out var stateToAdd, out var stateIterator))
            {
                do
                    currentHorizon.AddIfUnique(stateToAdd);
                while (statesToUpdate.TryGetNextValue(out stateToAdd, ref stateIterator));
            }

            // Update values from leaf state(s) to root
            while (depth >= 0)
            {
                for (int i = 0; i < currentHorizon.Length; i++)
                {
                    var stateKey = currentHorizon[i];
                    var updateState = true;
                    if (actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var stateActionIterator))
                    {
                        // Expanded state. Only update if one or more actions have updated.
                        updateState = false;

                        // Update all actions
                        do
                            updateState |= UpdateCumulativeReward(new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey), resultingStateLookup, stateInfoLookup, actionInfoLookup, stateTransitionInfoLookup);
                        while (actionLookup.TryGetNextValue(out actionKey, ref stateActionIterator));
                    }

                    if (!updateState)
                        continue;

                    // Update state
                    if (UpdateStateValue(stateKey, actionLookup, stateInfoLookup, actionInfoLookup))
                    {
                        // If a change has occured, update predecessors
                        if (predecessorLookup.TryGetFirstValue(stateKey, out var predecessorStateKey, out var predecessorIterator))
                        {
                            do
                                nextHorizon.AddIfUnique(predecessorStateKey);
                            while (predecessorLookup.TryGetNextValue(out predecessorStateKey, ref predecessorIterator));
                        }
                    }
                }

                var temp = currentHorizon;
                currentHorizon = nextHorizon;
                nextHorizon = temp;
                nextHorizon.Clear();

                depth--;

                // pull out states from statesToUpdate
                if (statesToUpdate.TryGetFirstValue(depth, out stateToAdd, out stateIterator))
                {
                    do
                        currentHorizon.AddIfUnique(stateToAdd);
                    while (statesToUpdate.TryGetNextValue(out stateToAdd, ref stateIterator));
                }
            }

            // new: continue propagating complete flag changes
            while (currentHorizon.Length > 0)
            {
                for (int i = 0; i < currentHorizon.Length; i++)
                {
                    var stateKey = currentHorizon[i];
                    var updateState = false;

                    // Update all actions
                    actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var stateActionIterator);
                    do
                    {   var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                        if (UpdateSubplanCompleteStatus(stateActionPair, out var updatedActionInfo))
                        {
                            updateState = true;

                            // Write back updated info
                            actionInfoLookup[stateActionPair] = updatedActionInfo;
                        }
                    }
                    while (actionLookup.TryGetNextValue(out actionKey, ref stateActionIterator));

                    // Update state
                    if (updateState && UpdateSubplanCompleteStatus(stateKey, out var updatedStateInfo))
                    {
                        // Write back updated info
                        stateInfoLookup[stateKey] = updatedStateInfo;

                        // If a change has occured, update predecessors
                        if (predecessorLookup.TryGetFirstValue(stateKey, out var predecessorStateKey, out var predecessorIterator))
                        {
                            do
                                nextHorizon.AddIfUnique(predecessorStateKey);
                            while (predecessorLookup.TryGetNextValue(out predecessorStateKey, ref predecessorIterator));
                        }
                    }
                }

                var temp = currentHorizon;
                currentHorizon = nextHorizon;
                nextHorizon = temp;
                nextHorizon.Clear();
            }

            currentHorizon.Dispose();
            nextHorizon.Dispose();
            statesToUpdate.Dispose();
        }

        bool UpdateStateValue(TStateKey stateKey, NativeMultiHashMap<TStateKey, TActionKey> actionLookup,
            NativeHashMap<TStateKey, StateInfo> stateInfoLookup,
            NativeHashMap<StateActionPair<TStateKey, TActionKey>, ActionInfo> actionInfoLookup)
        {
            var stateInfo = stateInfoLookup[stateKey];

            // Handle case of no valid actions (mark complete)
            if (!actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var iterator))
            {
                if (!stateInfo.SubplanIsComplete)
                {
                    // State was not marked terminal, so the value should be reset, as to not use the estimated value.
                    stateInfo.CumulativeRewardEstimate = new BoundedValue(0,0,0);
                    stateInfo.SubplanIsComplete = true;
                    stateInfoLookup[stateKey] = stateInfo;
                    return true;
                }

                // Terminal state. No update required.
                return false;
            }

            var originalValue = stateInfo.CumulativeRewardEstimate;
            var originalCompleteStatus = stateInfo.SubplanIsComplete;
            stateInfo.CumulativeRewardEstimate = new BoundedValue(float.MinValue, float.MinValue, float.MinValue);
            stateInfo.SubplanIsComplete = true;
            var maxLowerBound = float.MinValue;

            // Pick max action; find max lower bound
            do
            {
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                var actionInfo = actionInfoLookup[stateActionPair];

                stateInfo.CumulativeRewardEstimate = stateInfo.CumulativeRewardEstimate.Average < actionInfo.CumulativeRewardEstimate.Average ?
                    actionInfo.CumulativeRewardEstimate :
                    stateInfo.CumulativeRewardEstimate;

                maxLowerBound = math.max(maxLowerBound, actionInfo.CumulativeRewardEstimate.LowerBound);
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            // Update complete status (ignore pruned actions)
            actionLookup.TryGetFirstValue(stateKey, out actionKey, out iterator);
            do
            {
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                var actionInfo = actionInfoLookup[stateActionPair];

                if (actionInfo.CumulativeRewardEstimate.UpperBound >= maxLowerBound)
                    stateInfo.SubplanIsComplete &= actionInfo.SubplanIsComplete;
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            // Reassign
            stateInfoLookup[stateKey] = stateInfo;

            return !originalValue.Approximately(stateInfo.CumulativeRewardEstimate) || originalCompleteStatus != stateInfo.SubplanIsComplete;
        }

        bool UpdateCumulativeReward(StateActionPair<TStateKey, TActionKey> stateActionPair,
            NativeMultiHashMap<StateActionPair<TStateKey, TActionKey>, TStateKey> resultingStateLookup,
            NativeHashMap<TStateKey, StateInfo> stateInfoLookup,
            NativeHashMap<StateActionPair<TStateKey, TActionKey>, ActionInfo> actionInfoLookup,
            NativeHashMap<StateTransition<TStateKey, TActionKey>, StateTransitionInfo> stateTransitionInfoLookup)
        {
            var actionInfo = actionInfoLookup[stateActionPair];
            var originalValue = actionInfo.CumulativeRewardEstimate;
            var originalCompleteStatus = actionInfo.SubplanIsComplete;
            actionInfo.CumulativeRewardEstimate = default;
            actionInfo.SubplanIsComplete = true;

            resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var iterator);
            do
            {
                var stateTransitionInfo = stateTransitionInfoLookup[new StateTransition<TStateKey, TActionKey>(stateActionPair, resultingStateKey)];
                var resultingStateInfo = stateInfoLookup[resultingStateKey];

                actionInfo.SubplanIsComplete &= resultingStateInfo.SubplanIsComplete;
                actionInfo.CumulativeRewardEstimate += stateTransitionInfo.Probability *
                    (stateTransitionInfo.TransitionUtilityValue + DiscountFactor * resultingStateInfo.CumulativeRewardEstimate);
            } while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            actionInfoLookup[stateActionPair] = actionInfo;

            return !originalValue.Approximately(actionInfo.CumulativeRewardEstimate) || originalCompleteStatus != actionInfo.SubplanIsComplete;
        }

        bool UpdateSubplanCompleteStatus(TStateKey stateKey, out StateInfo updatedStateInfo)
        {
            updatedStateInfo = planGraph.StateInfoLookup[stateKey];
            var originalCompleteStatus = updatedStateInfo.SubplanIsComplete;
            updatedStateInfo.SubplanIsComplete = true;
            var maxLowerBound = float.MinValue;

            // Find max lower bound
            var actionLookup = planGraph.ActionLookup;
            actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var iterator);
            do
            {
                var actionInfo = planGraph.ActionInfoLookup[new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey)];
                maxLowerBound = math.max(maxLowerBound, actionInfo.CumulativeRewardEstimate.LowerBound);
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            // Update complete status (ignore pruned actions)
            actionLookup.TryGetFirstValue(stateKey, out actionKey, out iterator);
            do
            {
                var actionInfo = planGraph.ActionInfoLookup[new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey)];
                if (actionInfo.CumulativeRewardEstimate.UpperBound >= maxLowerBound)
                    updatedStateInfo.SubplanIsComplete &= actionInfo.SubplanIsComplete;
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            return originalCompleteStatus != updatedStateInfo.SubplanIsComplete;
        }

        bool UpdateSubplanCompleteStatus(StateActionPair<TStateKey, TActionKey> stateActionPair, out ActionInfo updatedActionInfo)
        {
            updatedActionInfo = planGraph.ActionInfoLookup[stateActionPair];
            var originalCompleteStatus = updatedActionInfo.SubplanIsComplete;
            updatedActionInfo.SubplanIsComplete = true;

            // Update complete status
            planGraph.ResultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var iterator);
            do
                updatedActionInfo.SubplanIsComplete &= planGraph.StateInfoLookup[resultingStateKey].SubplanIsComplete;
            while (planGraph.ResultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            return originalCompleteStatus != updatedActionInfo.SubplanIsComplete;
        }
    }

    [BurstCompile]
    struct UpdateDepthMapAndResizeContainersJob<TStateKey> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
    {
        // Input
        public NativeMultiHashMap<TStateKey, int> SelectedStates;
        public int MaxDepth;

        // Output
        public NativeHashMap<TStateKey, int> DepthMap;
        public NativeMultiHashMap<int, TStateKey> SelectedStatesByHorizon;
        public NativeHashMap<TStateKey, byte> PredecessorStates;
        public NativeList<TStateKey> HorizonStateList;

        public void Execute()
        {
            // Resize containers
            int graphSize = DepthMap.Count();
            SelectedStatesByHorizon.Capacity = math.max(SelectedStatesByHorizon.Capacity, MaxDepth + 1);
            PredecessorStates.Capacity = math.max(PredecessorStates.Capacity, graphSize);
            HorizonStateList.Capacity = math.max(HorizonStateList.Capacity, graphSize);

            var selectedStateKeys = SelectedStates.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < selectedStateKeys.Length; i++)
            {
                var stateKey = selectedStateKeys[i];
                int stateDepth = int.MinValue;
                SelectedStates.TryGetFirstValue(stateKey, out var selectedDepth, out var iterator);
                do
                    stateDepth = math.max(stateDepth, selectedDepth);
                while (SelectedStates.TryGetNextValue(out selectedDepth, ref iterator));

                // Update depth map
                DepthMap[stateKey] = stateDepth;
                SelectedStatesByHorizon.AddValueIfUnique(stateDepth, stateKey);
            }
        }
    }

    [BurstCompile]
    struct PrepareBackpropagationHorizon<TStateKey> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
    {
        // Input
        public int Horizon;
        public NativeMultiHashMap<int, TStateKey> SelectedStatesByHorizon;
        public NativeHashMap<TStateKey, byte> PredecessorInputStates;

        // Output
        public NativeList<TStateKey> OutputStates;

        public void Execute()
        {
            // Gather selected states together with predecessor input states
            if (SelectedStatesByHorizon.TryGetFirstValue(Horizon, out var stateKey, out var iterator))
            {
                do
                    PredecessorInputStates.TryAdd(stateKey, default);
                while (SelectedStatesByHorizon.TryGetNextValue(out stateKey, ref iterator));
            }

            // Write to output list
            OutputStates.Clear();
            var keys = PredecessorInputStates.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                OutputStates.Add(keys[i]);
            }
            keys.Dispose();

            // Clear out containers
            PredecessorInputStates.Clear();
            SelectedStatesByHorizon.Remove(Horizon);
        }
    }

    [BurstCompile]
    struct ParallelBackpropagationJob<TStateKey, TActionKey> : IJobParallelForDefer
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {

        // Inputs
        [ReadOnly] public float DiscountFactor;
        [ReadOnly] public NativeArray<TStateKey> StatesToUpdate;

        // Plan graph
        [ReadOnly] public NativeMultiHashMap<TStateKey, TActionKey> ActionLookup;
        [ReadOnly] public NativeMultiHashMap<TStateKey, TStateKey> PredecessorGraph;
        [ReadOnly] public NativeHashMap<StateTransition<TStateKey, TActionKey>, StateTransitionInfo> StateTransitionInfoLookup;
        [ReadOnly] public NativeMultiHashMap<StateActionPair<TStateKey, TActionKey>, TStateKey> ResultingStateLookup;
        [NativeDisableParallelForRestriction] public NativeHashMap<TStateKey, StateInfo> StateInfoLookup;
        [NativeDisableParallelForRestriction] public NativeHashMap<StateActionPair<TStateKey, TActionKey>, ActionInfo> ActionInfoLookup;

        // Outputs
        [WriteOnly] public NativeHashMap<TStateKey, byte>.ParallelWriter PredecessorStatesToUpdate;

        [NativeDisableContainerSafetyRestriction] NativeList<ActionInfo> m_ActionInfoForState;

        public void Execute(int jobIndex)
        {
            var stateKey = StatesToUpdate[jobIndex];
            StateInfo updatedStateInfo;

            var actionCount = ActionLookup.CountValuesForKey(stateKey);
            if (actionCount == 0)
            {
                if (UpdateStateValueNoActions(stateKey, out updatedStateInfo))
                {
                    // Queue for write job
                    StateInfoLookup[stateKey] = updatedStateInfo;

                    // If a change has occured, queue predecessors for update
                    if (PredecessorGraph.TryGetFirstValue(stateKey, out var predecessorStateKey, out var predecessorIterator))
                    {
                        do
                            PredecessorStatesToUpdate.TryAdd(predecessorStateKey, default);
                        while (PredecessorGraph.TryGetNextValue(out predecessorStateKey, ref predecessorIterator));
                    }
                }
                return;
            }

            // Allocate local container
            if (!m_ActionInfoForState.IsCreated)
                m_ActionInfoForState = new NativeList<ActionInfo>(actionCount, Allocator.Temp);
            else
                m_ActionInfoForState.Clear();

            // Expanded state. Only update if one or more actions have updated.
            var updateState = false;

            // Update all actions
            ActionLookup.TryGetFirstValue(stateKey, out var actionKey, out var stateActionIterator);
            do
            {
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                if (UpdateCumulativeReward(stateActionPair, out var actionInfo))
                {
                    // Queue for write job
                    ActionInfoLookup[stateActionPair] = actionInfo;

                    updateState = true;
                }

                m_ActionInfoForState.Add(actionInfo);
            }
            while (ActionLookup.TryGetNextValue(out actionKey, ref stateActionIterator));



            // If no actions have changed info, the state info will not change, so check before updating state.
            if (updateState && UpdateStateValueFromActions(stateKey, m_ActionInfoForState, out updatedStateInfo))
            {
                // Queue for write job
                StateInfoLookup[stateKey] = updatedStateInfo;

                // If a change has occured, queue predecessors for update
                if (PredecessorGraph.TryGetFirstValue(stateKey, out var predecessorStateKey, out var predecessorIterator))
                {
                    do
                        PredecessorStatesToUpdate.TryAdd(predecessorStateKey, default);
                    while (PredecessorGraph.TryGetNextValue(out predecessorStateKey, ref predecessorIterator));
                }
            }
        }

        bool UpdateStateValueNoActions(TStateKey stateKey, out StateInfo updatedStateInfo)
        {
            // Handle case of no actions (mark complete, override bounds)
            var originalStateInfo = StateInfoLookup[stateKey];
            var rewardAverage = originalStateInfo.CumulativeRewardEstimate.Average;
            updatedStateInfo = new StateInfo
            {
                CumulativeRewardEstimate = originalStateInfo.SubplanIsComplete ?
                    new BoundedValue(rewardAverage, rewardAverage, rewardAverage) : new BoundedValue(0,0,0),
                SubplanIsComplete = true
            };

            return !originalStateInfo.CumulativeRewardEstimate.Approximately(updatedStateInfo.CumulativeRewardEstimate) ||
                   originalStateInfo.SubplanIsComplete != updatedStateInfo.SubplanIsComplete;
        }

        bool UpdateStateValueFromActions(TStateKey stateKey, NativeArray<ActionInfo> updatedActionInfo, out StateInfo updatedStateInfo)
        {
            updatedStateInfo = new StateInfo
            {
                CumulativeRewardEstimate = new BoundedValue(float.MinValue, float.MinValue, float.MinValue),
                SubplanIsComplete = true
            };

            // Set state value = max action reward value; find max lower bound
            var maxLowerBound = float.MinValue;
            for (int i = 0; i < updatedActionInfo.Length; i++)
            {
                var cumulativeReward = updatedActionInfo[i].CumulativeRewardEstimate;
                maxLowerBound = math.max(maxLowerBound, cumulativeReward.LowerBound);
                if (updatedStateInfo.CumulativeRewardEstimate.Average < cumulativeReward.Average)
                    updatedStateInfo.CumulativeRewardEstimate = cumulativeReward;
            }

            // Update complete status (ignore pruned actions)
            for (int i = 0; i < updatedActionInfo.Length; i++)
            {
                var actionInfo = updatedActionInfo[i];
                if (actionInfo.CumulativeRewardEstimate.UpperBound >= maxLowerBound)
                    updatedStateInfo.SubplanIsComplete &= actionInfo.SubplanIsComplete;
            }

            var originalStateInfo = StateInfoLookup[stateKey];
            return originalStateInfo.SubplanIsComplete != updatedStateInfo.SubplanIsComplete ||
                   !originalStateInfo.CumulativeRewardEstimate.Approximately(updatedStateInfo.CumulativeRewardEstimate);
        }

        bool UpdateCumulativeReward(StateActionPair<TStateKey, TActionKey> stateActionPair, out ActionInfo updatedActionInfo)
        {
            updatedActionInfo = new ActionInfo
            {
                CumulativeRewardEstimate = default,
                SubplanIsComplete = true
            };

            ResultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var iterator);
            do
            {
                var stateTransitionInfo = StateTransitionInfoLookup[new StateTransition<TStateKey, TActionKey>(stateActionPair, resultingStateKey)];
                var resultingStateInfo = StateInfoLookup[resultingStateKey];

                updatedActionInfo.SubplanIsComplete &= resultingStateInfo.SubplanIsComplete;
                updatedActionInfo.CumulativeRewardEstimate.m_ValueVector += stateTransitionInfo.Probability *
                    (stateTransitionInfo.TransitionUtilityValue + DiscountFactor * resultingStateInfo.CumulativeRewardEstimate.m_ValueVector);
            } while (ResultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            var originalActionInfo = ActionInfoLookup[stateActionPair];
            return originalActionInfo.SubplanIsComplete != updatedActionInfo.SubplanIsComplete ||
                !originalActionInfo.CumulativeRewardEstimate.Approximately(updatedActionInfo.CumulativeRewardEstimate);
        }
    }

    [BurstCompile]
    struct UpdateCompleteStatusJob<TStateKey, TActionKey> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        public NativeHashMap<TStateKey, byte> StatesToUpdate;
        public PlanGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> planGraph;

        public void Execute()
        {
            if (StatesToUpdate.IsEmpty)
                return;

            var statesToUpdateLength = StatesToUpdate.Count();
            var currentHorizon = new NativeList<TStateKey>(statesToUpdateLength, Allocator.Temp);
            var nextHorizon = new NativeList<TStateKey>(statesToUpdateLength, Allocator.Temp);

            var stateKeys = StatesToUpdate.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < stateKeys.Length; i++)
            {
                currentHorizon.Add(stateKeys[i]);
            }
            stateKeys.Dispose();

            var actionLookup = planGraph.ActionLookup;
            var actionInfoLookup = planGraph.ActionInfoLookup;
            var stateInfoLookup = planGraph.StateInfoLookup;
            var predecessorLookup = planGraph.PredecessorGraph;
            while (currentHorizon.Length > 0)
            {
                for (int i = 0; i < currentHorizon.Length; i++)
                {
                    var stateKey = currentHorizon[i];
                    var updateState = false;

                    // Update all actions
                    actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var stateActionIterator);
                    do
                    {   var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                        if (UpdateActionCompleteStatus(stateActionPair, out var updatedActionInfo))
                        {
                            updateState = true;

                            // Write back updated info
                            actionInfoLookup[stateActionPair] = updatedActionInfo;
                        }
                    }
                    while (actionLookup.TryGetNextValue(out actionKey, ref stateActionIterator));

                    // Update state
                    if (updateState && UpdateStateCompleteStatus(stateKey, out var updatedStateInfo))
                    {
                        // Write back updated info
                        stateInfoLookup[stateKey] = updatedStateInfo;

                        // If a change has occured, update predecessors
                        if (predecessorLookup.TryGetFirstValue(stateKey, out var predecessorStateKey, out var predecessorIterator))
                        {
                            do
                                nextHorizon.AddIfUnique(predecessorStateKey);
                            while (predecessorLookup.TryGetNextValue(out predecessorStateKey, ref predecessorIterator));
                        }
                    }
                }

                var temp = currentHorizon;
                currentHorizon = nextHorizon;
                nextHorizon = temp;
                nextHorizon.Clear();
            }

            currentHorizon.Dispose();
            nextHorizon.Dispose();
        }

        bool UpdateStateCompleteStatus(TStateKey stateKey, out StateInfo updatedStateInfo)
        {
            updatedStateInfo = planGraph.StateInfoLookup[stateKey];
            var originalCompleteStatus = updatedStateInfo.SubplanIsComplete;
            updatedStateInfo.SubplanIsComplete = true;
            var maxLowerBound = float.MinValue;

            // Find max lower bound
            var actionLookup = planGraph.ActionLookup;
            actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var iterator);
            do
            {
                var actionInfo = planGraph.ActionInfoLookup[new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey)];
                maxLowerBound = math.max(maxLowerBound, actionInfo.CumulativeRewardEstimate.LowerBound);
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            // Update complete status (ignore pruned actions)
            actionLookup.TryGetFirstValue(stateKey, out actionKey, out iterator);
            do
            {
                var actionInfo = planGraph.ActionInfoLookup[new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey)];
                if (actionInfo.CumulativeRewardEstimate.UpperBound >= maxLowerBound)
                    updatedStateInfo.SubplanIsComplete &= actionInfo.SubplanIsComplete;
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            return originalCompleteStatus != updatedStateInfo.SubplanIsComplete;
        }

        bool UpdateActionCompleteStatus(StateActionPair<TStateKey, TActionKey> stateActionPair, out ActionInfo updatedActionInfo)
        {
            updatedActionInfo = planGraph.ActionInfoLookup[stateActionPair];
            var originalCompleteStatus = updatedActionInfo.SubplanIsComplete;
            updatedActionInfo.SubplanIsComplete = true;

            // Update complete status
            planGraph.ResultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var iterator);
            do
                updatedActionInfo.SubplanIsComplete &= planGraph.StateInfoLookup[resultingStateKey].SubplanIsComplete;
            while (planGraph.ResultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            return originalCompleteStatus != updatedActionInfo.SubplanIsComplete;
        }
    }
}
