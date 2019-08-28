using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
#if UNITY_DOTSPLAYER
using TinyDebug = Unity.Tiny.Debugging.Debug;
#endif

namespace Unity.AI.Planner.Jobs
{
#if UNITY_DOTSPLAYER
    struct Debug
    {
        public static void Assert(bool condition, string message)
        {
            if (condition)
                return;

            TinyDebug.LogError(message);
        }
    }
#endif
    struct BackpropagationJob<TStateKey, TActionKey> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        const float k_DefaultDiscountFactor = 1f;
        const float k_DefaultPolicyValueTolerance = 10e-5f;

        [ReadOnly] public float? DiscountFactor;
        [ReadOnly] public float? PolicyValueTolerance;

        [ReadOnly] public NativeHashMap<TStateKey, int> DepthMap;
        [ReadOnly] public NativeList<TStateKey> SelectedStates;

        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> PolicyGraph;

        public void Execute()
        {
            DiscountFactor = DiscountFactor ?? k_DefaultDiscountFactor;
            PolicyValueTolerance = PolicyValueTolerance ?? k_DefaultPolicyValueTolerance;

            var statesToUpdate = new NativeMultiHashMap<int, TStateKey>(SelectedStates.Length, Allocator.Temp);
            var maxDepth = int.MinValue;

            foreach (var stateKey in SelectedStates.AsArray())
            {
                var found = DepthMap.TryGetValue(stateKey, out var stateDepth);
                if (!found)
                    Debug.Assert(found, $"StateKey {stateKey} not found in depth map."); //todo fixme? conditional compile?

                statesToUpdate.AddValueIfUnique(stateDepth, stateKey);

                maxDepth = Math.Max(maxDepth, stateDepth);
            }

            var stateActionLookup = PolicyGraph.StateActionLookup;
            var resultingStateLookup = PolicyGraph.ResultingStateLookup;
            var actionInfoLookup = PolicyGraph.ActionInfoLookup;
            var stateInfoLookup = PolicyGraph.StateInfoLookup;
            var actionResultLookup = PolicyGraph.ActionResultLookup;
            var predecessorLookup = PolicyGraph.PredecessorGraph;
            var depth = maxDepth;

            var currentHorizon = new NativeList<TStateKey>(statesToUpdate.Length, Allocator.Temp);
            var nextHorizon = new NativeList<TStateKey>(statesToUpdate.Length, Allocator.Temp);

            // pull out states from statesToUpdate
            if (statesToUpdate.TryGetFirstValue(depth, out var stateToAdd, out var stateIterator))
            {
                do
                    currentHorizon.AddIfUnique(stateToAdd);
                while (statesToUpdate.TryGetNextValue(out stateToAdd, ref stateIterator));
            }

            // fill current horizon
            while (currentHorizon.Length > 0)
            {
                for (int i = 0; i < currentHorizon.Length; i++)
                {
                    var stateKey = currentHorizon[i];
                    if (stateActionLookup.TryGetFirstValue(stateKey, out var stateActionTuple, out var stateActionIterator))
                    {
                        // Update all actions
                        do
                        {
                            UpdateActionValue(stateKey, stateActionTuple.Item2, resultingStateLookup, stateInfoLookup,
                                actionInfoLookup, actionResultLookup);
                        } while (stateActionLookup.TryGetNextValue(out stateActionTuple, ref stateActionIterator));
                    }
                    else
                    {
                        //Debug.Assert(found, $"Encountered a non-terminal state with no valid actions. Check action preconditions or state termination criteria. Use the Policy Graph Visualizer to inspect state with key: {stateKey}");
                    }

                    // Update state
                    var (valueChanged, completeStatusChanged) = UpdateStateValue(stateKey, stateActionLookup, stateInfoLookup, actionInfoLookup);

                    if (completeStatusChanged || valueChanged && depth > 0)
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

        (bool, bool) UpdateStateValue(TStateKey stateKey, NativeMultiHashMap<TStateKey, (TStateKey, TActionKey)> stateActionLookup,
            NativeHashMap<TStateKey, StateInfo> stateInfoLookup,
            NativeHashMap<(TStateKey, TActionKey), ActionInfo> actionInfoLookup)
        {
            var found = stateInfoLookup.TryGetValue(stateKey, out var stateInfo);
            if (!found)
                Debug.Assert(found, "State info not found.");

            found = stateActionLookup.TryGetFirstValue(stateKey, out var stateActionTuple, out var iterator);
            if (!found)
            {
                if (!stateInfo.Complete)
                {
                    stateInfo.Complete = true;
                    stateInfoLookup.Remove(stateKey);
                    stateInfoLookup.TryAdd(stateKey, stateInfo);
                    return (false, true);
                }
                return (false, false);
            }

            // Store previous node value for comparison
            var previousValue = stateInfo.PolicyValue;

            // Pick max action
            var maxValue = float.MinValue;
            var originalCompleteStatus = stateInfo.Complete;
            var stateCompleteStatus = true;

            do
            {
                found = actionInfoLookup.TryGetValue(stateActionTuple, out var actionInfo);
                if (!found)
                    Debug.Assert(found, "Action info not found.");

                maxValue = Math.Max(actionInfo.ActionValue, maxValue);
                stateCompleteStatus &= actionInfo.Complete;
            }
            while (stateActionLookup.TryGetNextValue(out stateActionTuple, ref iterator));

            stateInfo.PolicyValue = maxValue;
            stateInfo.Complete = stateCompleteStatus;

            stateInfoLookup.Remove(stateKey);
            found = stateInfoLookup.TryAdd(stateKey, stateInfo);
            if (!found)
                Debug.Assert(found, $"Failed to set new state info {stateKey} {stateInfo}");

            // Don't continue updating upward when the node has not changed in a significant way
            return (Math.Abs(maxValue - previousValue) > PolicyValueTolerance, originalCompleteStatus != stateCompleteStatus);
        }

        void UpdateActionValue(TStateKey stateKey, TActionKey actionKey,
            NativeMultiHashMap<(TStateKey, TActionKey), TStateKey> resultingStateLookup,
            NativeHashMap<TStateKey, StateInfo> stateInfoLookup,
            NativeHashMap<(TStateKey, TActionKey), ActionInfo> actionInfoLookup,
            NativeHashMap<(TStateKey, TActionKey, TStateKey), ActionResult> actionResultLookup)
        {
            var stateActionTuple = (stateKey, actionKey);
            bool found = resultingStateLookup.TryGetFirstValue(stateActionTuple, out var resultingStateKey, out var iterator);
            if (found)
            {
                actionInfoLookup.TryGetValue(stateActionTuple, out var actionInfo);
                actionInfo.ActionValue = 0f;
                actionInfo.Complete = true;

                do
                {
                    found = actionResultLookup.TryGetValue((stateKey, actionKey, resultingStateKey), out var actionResult);
                    if (!found)
                        Debug.Assert(found, "Action result not found.");

                    found = stateInfoLookup.TryGetValue(resultingStateKey, out var stateInfo);
                    if (!found)
                        Debug.Assert(found, $"StateInfo not found: {resultingStateKey}");

                    actionInfo.ActionValue += actionResult.Probability * (actionResult.TransitionUtilityValue
                        + DiscountFactor.Value * stateInfo.PolicyValue);

                    actionInfo.Complete &= stateInfo.Complete;
                } while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

                actionInfoLookup.Remove(stateActionTuple);
                found = actionInfoLookup.TryAdd(stateActionTuple, actionInfo);
                if (!found)
                    Debug.Assert(found, $"Action info not updated: {stateActionTuple} {actionInfo}");
            }
            else
            {
                if (!found)
                    Debug.Assert(found, $"Failed to get resulting states: {stateActionTuple}");
            }
        }
    }
}
