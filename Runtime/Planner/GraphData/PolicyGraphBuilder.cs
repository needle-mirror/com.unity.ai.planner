using System;
using Unity.Collections;
using UnityEngine;


namespace Unity.AI.Planner
{
    struct PolicyGraphBuilder<TStateKey, TActionKey>
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, ActionResult> PolicyGraph { private get; set; }

        public StateContext AddState(TStateKey stateKey, bool complete = false, float value = 0f, int visitCount = 0)
        {
            //policy graph
            PolicyGraph.StateInfoLookup.TryAdd(stateKey, new StateInfo()
            {
                Complete = complete,
                VisitCount = visitCount,
                PolicyValue = value,
            });

            return WithState(stateKey);
        }

        public StateContext WithState(TStateKey stateKey)
        {
            if (!PolicyGraph.StateInfoLookup.TryGetValue(stateKey, out _))
                throw new ArgumentException($"State {stateKey} does not exist in policy graph.");

            return new StateContext()
            {
                StateKey = stateKey,
                Builder = this,
            };
        }

        public struct StateContext
        {
            public TStateKey StateKey;
            public PolicyGraphBuilder<TStateKey, TActionKey> Builder { private get; set; }

            public StateContext UpdateInfo(int? visitCount = null, bool? complete = null, float? policyValue = null)
            {
                var stateInfoLookup = Builder.PolicyGraph.StateInfoLookup;
                if (!stateInfoLookup.TryGetValue(StateKey, out var stateInfo))
                    throw new ArgumentException($"State {StateKey} has not been added to the graph. Use AddState before setting state info.");

                stateInfo.VisitCount = visitCount ?? stateInfo.VisitCount;
                stateInfo.Complete = complete ?? stateInfo.Complete;
                stateInfo.PolicyValue = policyValue ?? stateInfo.PolicyValue;

                stateInfoLookup.Remove(StateKey);
                stateInfoLookup.TryAdd(StateKey, stateInfo);

                return this;
            }

            public ActionContext AddAction(TActionKey actionKey, int visitCount = 1, bool complete = false, float actionValue = 0f)
            {
                var policyGraph = Builder.PolicyGraph;
                var addedAction = policyGraph.ActionInfoLookup.TryAdd((StateKey, actionKey), new ActionInfo()
                {
                    VisitCount = visitCount,
                    Complete = complete,
                    ActionValue = actionValue
                });

                if (!addedAction)
                    throw new ArgumentException($"Action {actionKey} for state {StateKey} has already been added to the policy graph.");

                var stateActionLookup = policyGraph.StateActionLookup;
                if (stateActionLookup.TryGetFirstValue(StateKey, out var stateActionPair, out var iterator))
                {
                    do
                    {
                        if (stateActionPair.Item2.Equals(actionKey))
                            throw new ArgumentException($"Action {actionKey} already added to State {StateKey}. Ensure action keys are unique for a state.");

                    } while (stateActionLookup.TryGetNextValue(out stateActionPair, ref iterator));
                }

                stateActionLookup.Add(StateKey, (StateKey, actionKey));

                return WithAction(actionKey);
            }

            public ActionContext WithAction(TActionKey actionKey)
            {
                if (!Builder.PolicyGraph.ActionInfoLookup.TryGetValue((StateKey, actionKey), out _))
                    throw new ArgumentException($"Action {actionKey} for state {StateKey} does not exist in policy graph.");

                return new ActionContext()
                {
                    ActionKey = actionKey,
                    StateKey = StateKey,
                    Builder = Builder,
                };
            }
        }

        public struct ActionContext
        {
            public TActionKey ActionKey;
            public TStateKey StateKey;
            public PolicyGraphBuilder<TStateKey, TActionKey> Builder { private get; set; }

            public ActionContext UpdateInfo(int? visitCount = null, bool? complete = null, float? actionValue = null)
            {
                var actionInfoLookup = Builder.PolicyGraph.ActionInfoLookup;
                var stateActionPair = (StateKey, ActionKey);
                if (!actionInfoLookup.TryGetValue(stateActionPair, out var actionInfo))
                    throw new ArgumentException($"Action {ActionKey} for state {StateKey} does not exist in policy graph.");

                actionInfo.VisitCount = visitCount ?? actionInfo.VisitCount;
                actionInfo.Complete = complete ?? actionInfo.Complete;
                actionInfo.ActionValue = actionValue ?? actionInfo.ActionValue;

                actionInfoLookup.Remove(stateActionPair);
                actionInfoLookup.TryAdd(stateActionPair, actionInfo);

                return this;
            }

            public ActionContext AddResultingState(TStateKey resultingStateKey, bool complete = false, float value = 0f,
                float probability = 1f, float transitionUtility = 0f)
            {
                Builder.AddState(resultingStateKey, complete, value);

                var policyGraph = Builder.PolicyGraph;
                bool addedResult = policyGraph.ActionResultLookup.TryAdd((StateKey, ActionKey, resultingStateKey), new ActionResult
                {
                    Probability = probability,
                    TransitionUtilityValue = transitionUtility
                });

                if (!addedResult)
                    throw new ArgumentException($"Resulting state {resultingStateKey} has already been added for action {ActionKey}. Add each resulting state only once.");

                var resultingStateLookup = policyGraph.ResultingStateLookup;

                if (resultingStateLookup.TryGetFirstValue((StateKey, ActionKey), out var existingResultingStateKey, out var iterator))
                {
                    do
                    {
                        if (resultingStateKey.Equals(existingResultingStateKey))
                            throw new ArgumentException($"Resulting state {resultingStateKey} has already been added for action {ActionKey}. Add each resulting state only once.");
                    } while (resultingStateLookup.TryGetNextValue(out existingResultingStateKey, ref iterator));
                }

                resultingStateLookup.Add((StateKey, ActionKey), resultingStateKey);

                policyGraph.PredecessorGraph.AddValueIfUnique(resultingStateKey, StateKey);

                return this;
            }
        }
    }
}
