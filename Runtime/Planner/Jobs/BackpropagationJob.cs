using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.AI.Planner.Jobs
{
    /// <summary>
    /// Modes designating the strategy for computing updated decision policy values during backpropagation.
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
        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> PolicyGraph;

        public void Execute()
        {
            var statesToUpdate = new NativeMultiHashMap<int, TStateKey>(SelectedStates.Count(), Allocator.Temp);
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
                maxDepth = Math.Max(maxDepth, stateDepth);
            }

            var actionLookup = PolicyGraph.ActionLookup;
            var resultingStateLookup = PolicyGraph.ResultingStateLookup;
            var actionInfoLookup = PolicyGraph.ActionInfoLookup;
            var stateInfoLookup = PolicyGraph.StateInfoLookup;
            var stateTransitionInfoLookup = PolicyGraph.StateTransitionInfoLookup;
            var predecessorLookup = PolicyGraph.PredecessorGraph;
            var depth = maxDepth;

            var currentHorizon = new NativeList<TStateKey>(statesToUpdate.Count(), Allocator.Temp);
            var nextHorizon = new NativeList<TStateKey>(statesToUpdate.Count(), Allocator.Temp);

            // pull out states from statesToUpdate
            if (statesToUpdate.TryGetFirstValue(depth, out var stateToAdd, out var stateIterator))
            {
                do
                    currentHorizon.AddIfUnique(stateToAdd);
                while (statesToUpdate.TryGetNextValue(out stateToAdd, ref stateIterator));
            }

            // fill current horizon
            while (currentHorizon.Length > 0 || depth >= 0)
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
                            updateState |= UpdateActionValue(new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey), resultingStateLookup, stateInfoLookup, actionInfoLookup, stateTransitionInfoLookup);
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
                if (!stateInfo.SubgraphComplete)
                {
                    // State was not marked terminal, so the value should be reset, as to not use the heuristic value.
                    stateInfo.PolicyValue = new BoundedValue(0,0,0);
                    stateInfo.SubgraphComplete = true;
                    stateInfoLookup[stateKey] = stateInfo;
                    return true;
                }

                // Terminal state. No update required.
                return false;
            }

            var originalValue = stateInfo.PolicyValue;
            var originalCompleteStatus = stateInfo.SubgraphComplete;
            stateInfo.PolicyValue = new BoundedValue(float.MinValue, float.MinValue, float.MinValue);
            stateInfo.SubgraphComplete = true;
            var maxLowerBound = float.MinValue;

            // Pick max action; find max lower bound
            do
            {
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                var actionInfo = actionInfoLookup[stateActionPair];

                stateInfo.PolicyValue = stateInfo.PolicyValue.Average < actionInfo.ActionValue.Average ?
                    actionInfo.ActionValue :
                    stateInfo.PolicyValue;

                maxLowerBound = math.max(maxLowerBound, actionInfo.ActionValue.LowerBound);
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            // Update complete status (ignore pruned actions)
            actionLookup.TryGetFirstValue(stateKey, out actionKey, out iterator);
            do
            {
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                var actionInfo = actionInfoLookup[stateActionPair];

                if (actionInfo.ActionValue.UpperBound >= maxLowerBound)
                    stateInfo.SubgraphComplete &= actionInfo.SubgraphComplete;
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            // Reassign
            stateInfoLookup[stateKey] = stateInfo;

            return !originalValue.Approximately(stateInfo.PolicyValue) || originalCompleteStatus != stateInfo.SubgraphComplete;
        }

        bool UpdateActionValue(StateActionPair<TStateKey, TActionKey> stateActionPair,
            NativeMultiHashMap<StateActionPair<TStateKey, TActionKey>, TStateKey> resultingStateLookup,
            NativeHashMap<TStateKey, StateInfo> stateInfoLookup,
            NativeHashMap<StateActionPair<TStateKey, TActionKey>, ActionInfo> actionInfoLookup,
            NativeHashMap<StateTransition<TStateKey, TActionKey>, StateTransitionInfo> stateTransitionInfoLookup)
        {
            var actionInfo = actionInfoLookup[stateActionPair];
            var originalValue = actionInfo.ActionValue;
            var originalCompleteStatus = actionInfo.SubgraphComplete;
            actionInfo.ActionValue = default;
            actionInfo.SubgraphComplete = true;

            resultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var iterator);
            do
            {
                var stateTransitionInfo = stateTransitionInfoLookup[new StateTransition<TStateKey, TActionKey>(stateActionPair, resultingStateKey)];
                var resultingStateInfo = stateInfoLookup[resultingStateKey];

                actionInfo.SubgraphComplete &= resultingStateInfo.SubgraphComplete;
                actionInfo.ActionValue += stateTransitionInfo.Probability *
                    (stateTransitionInfo.TransitionUtilityValue + DiscountFactor * resultingStateInfo.PolicyValue);
            } while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            actionInfoLookup[stateActionPair] = actionInfo;

            return !originalValue.Approximately(actionInfo.ActionValue) || originalCompleteStatus != actionInfo.SubgraphComplete;
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

        // Policy graph
        [ReadOnly] public NativeMultiHashMap<TStateKey, TActionKey> ActionLookup;
        [ReadOnly] public NativeMultiHashMap<TStateKey, TStateKey> PredecessorGraph;
        [ReadOnly] public NativeHashMap<StateTransition<TStateKey, TActionKey>, StateTransitionInfo> StateTransitionInfoLookup;
        [ReadOnly] public NativeMultiHashMap<StateActionPair<TStateKey, TActionKey>, TStateKey> ResultingStateLookup;
        [NativeDisableParallelForRestriction] public NativeHashMap<TStateKey, StateInfo> StateInfoLookup;
        [NativeDisableParallelForRestriction] public NativeHashMap<StateActionPair<TStateKey, TActionKey>, ActionInfo> ActionInfoLookup;

        // Outputs
        [WriteOnly] public NativeHashMap<TStateKey, byte>.ParallelWriter PredecessorStatesToUpdate;

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

            // Expanded state. Only update if one or more actions have updated.
            var updateState = false;
            var actionInfoForState = new NativeArray<ActionInfo>(actionCount, Allocator.Temp);
            var index = 0;

            // Update all actions
            ActionLookup.TryGetFirstValue(stateKey, out var actionKey, out var stateActionIterator);
            do
            {
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                if (UpdateActionValue(stateActionPair, out var actionInfo))
                {
                    // Queue for write job
                    ActionInfoLookup[stateActionPair] = actionInfo;

                    updateState = true;
                }

                actionInfoForState[index++] = actionInfo;
            }
            while (ActionLookup.TryGetNextValue(out actionKey, ref stateActionIterator));



            // If no actions have changed info, the state info will not change, so check before updating state.
            if (updateState && UpdateStateValueFromActions(stateKey, actionInfoForState, out updatedStateInfo))
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
            actionInfoForState.Dispose();
        }

        bool UpdateStateValueNoActions(TStateKey stateKey, out StateInfo updatedStateInfo)
        {
            // Handle case of no actions (mark complete, override bounds)
            var originalStateInfo = StateInfoLookup[stateKey];
            var policyAverage = originalStateInfo.PolicyValue.Average;
            updatedStateInfo = new StateInfo
            {
                PolicyValue = originalStateInfo.SubgraphComplete ?
                    new BoundedValue(policyAverage, policyAverage, policyAverage) : new BoundedValue(0,0,0),
                SubgraphComplete = true
            };

            return !originalStateInfo.PolicyValue.Approximately(updatedStateInfo.PolicyValue) ||
                   originalStateInfo.SubgraphComplete != updatedStateInfo.SubgraphComplete;
        }

        bool UpdateStateValueFromActions(TStateKey stateKey, NativeArray<ActionInfo> updatedActionInfo, out StateInfo updatedStateInfo)
        {
            updatedStateInfo = new StateInfo
            {
                PolicyValue = new BoundedValue(float.MinValue, float.MinValue, float.MinValue),
                SubgraphComplete = true
            };

            // Set state value = max action value; find max lower bound
            var maxLowerBound = float.MinValue;
            for (int i = 0; i < updatedActionInfo.Length; i++)
            {
                var actionValue = updatedActionInfo[i].ActionValue;
                maxLowerBound = math.max(maxLowerBound, actionValue.LowerBound);
                if (updatedStateInfo.PolicyValue.Average < actionValue.Average)
                    updatedStateInfo.PolicyValue = actionValue;
            }

            // Update complete status (ignore pruned actions)
            for (int i = 0; i < updatedActionInfo.Length; i++)
            {
                var actionInfo = updatedActionInfo[i];
                if (actionInfo.ActionValue.UpperBound >= maxLowerBound)
                    updatedStateInfo.SubgraphComplete &= actionInfo.SubgraphComplete;
            }

            var originalStateInfo = StateInfoLookup[stateKey];
            return originalStateInfo.SubgraphComplete != updatedStateInfo.SubgraphComplete ||
                   !originalStateInfo.PolicyValue.Approximately(updatedStateInfo.PolicyValue);
        }

        bool UpdateActionValue(StateActionPair<TStateKey, TActionKey> stateActionPair, out ActionInfo updatedActionInfo)
        {
            updatedActionInfo = new ActionInfo
            {
                ActionValue = default,
                SubgraphComplete = true
            };

            ResultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var iterator);
            do
            {
                var stateTransitionInfo = StateTransitionInfoLookup[new StateTransition<TStateKey, TActionKey>(stateActionPair, resultingStateKey)];
                var resultingStateInfo = StateInfoLookup[resultingStateKey];

                updatedActionInfo.SubgraphComplete &= resultingStateInfo.SubgraphComplete;
                updatedActionInfo.ActionValue.m_ValueVector += stateTransitionInfo.Probability *
                    (stateTransitionInfo.TransitionUtilityValue + DiscountFactor * resultingStateInfo.PolicyValue.m_ValueVector);
            } while (ResultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            var originalActionInfo = ActionInfoLookup[stateActionPair];
            return originalActionInfo.SubgraphComplete != updatedActionInfo.SubgraphComplete ||
                !originalActionInfo.ActionValue.Approximately(updatedActionInfo.ActionValue);
        }
    }

    [BurstCompile]
    struct UpdateCompleteStatusJob<TStateKey, TActionKey> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        public NativeHashMap<TStateKey, byte> StatesToUpdate;
        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> PolicyGraph;

        public void Execute()
        {
            if (StatesToUpdate.Count() == 0)
                return;

            var currentHorizon = new NativeList<TStateKey>(StatesToUpdate.Count(), Allocator.Temp);
            var nextHorizon = new NativeList<TStateKey>(StatesToUpdate.Count(), Allocator.Temp);

            var stateKeys = StatesToUpdate.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < stateKeys.Length; i++)
            {
                currentHorizon.Add(stateKeys[i]);
            }
            stateKeys.Dispose();

            var actionLookup = PolicyGraph.ActionLookup;
            var actionInfoLookup = PolicyGraph.ActionInfoLookup;
            var stateInfoLookup = PolicyGraph.StateInfoLookup;
            var predecessorLookup = PolicyGraph.PredecessorGraph;
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
            updatedStateInfo = PolicyGraph.StateInfoLookup[stateKey];
            var originalCompleteStatus = updatedStateInfo.SubgraphComplete;
            updatedStateInfo.SubgraphComplete = true;
            var maxLowerBound = float.MinValue;

            // Find max lower bound
            var actionLookup = PolicyGraph.ActionLookup;
            actionLookup.TryGetFirstValue(stateKey, out var actionKey, out var iterator);
            do
            {
                var actionInfo = PolicyGraph.ActionInfoLookup[new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey)];
                maxLowerBound = math.max(maxLowerBound, actionInfo.ActionValue.LowerBound);
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            // Update complete status (ignore pruned actions)
            actionLookup.TryGetFirstValue(stateKey, out actionKey, out iterator);
            do
            {
                var actionInfo = PolicyGraph.ActionInfoLookup[new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey)];
                if (actionInfo.ActionValue.UpperBound >= maxLowerBound)
                    updatedStateInfo.SubgraphComplete &= actionInfo.SubgraphComplete;
            }
            while (actionLookup.TryGetNextValue(out actionKey, ref iterator));

            return originalCompleteStatus != updatedStateInfo.SubgraphComplete;
        }

        bool UpdateActionCompleteStatus(StateActionPair<TStateKey, TActionKey> stateActionPair, out ActionInfo updatedActionInfo)
        {
            updatedActionInfo = PolicyGraph.ActionInfoLookup[stateActionPair];
            var originalCompleteStatus = updatedActionInfo.SubgraphComplete;
            updatedActionInfo.SubgraphComplete = true;

            // Update complete status
            PolicyGraph.ResultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var iterator);
            do
                updatedActionInfo.SubgraphComplete &= PolicyGraph.StateInfoLookup[resultingStateKey].SubgraphComplete;
            while (PolicyGraph.ResultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            return originalCompleteStatus != updatedActionInfo.SubgraphComplete;
        }
    }
}
