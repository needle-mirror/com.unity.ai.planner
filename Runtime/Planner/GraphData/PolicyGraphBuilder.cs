using System;
using Unity.Collections;
using Unity.Mathematics;


namespace Unity.AI.Planner
{
    struct PolicyGraphBuilder<TStateKey, TActionKey>
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        public PolicyGraph<TStateKey, StateInfo, TActionKey, ActionInfo, StateTransitionInfo> PolicyGraph { private get; set; }

        public StateContext AddState(TStateKey stateKey, bool complete = false, float3 value = default, int visitCount = 0)
        {
            //policy graph
            PolicyGraph.StateInfoLookup.TryAdd(stateKey, new StateInfo()
            {
                SubgraphComplete = complete,
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

            public StateContext UpdateInfo(bool? complete = null, float3? policyValue = null)
            {
                var stateInfoLookup = Builder.PolicyGraph.StateInfoLookup;
                if (!stateInfoLookup.TryGetValue(StateKey, out var stateInfo))
                    throw new ArgumentException($"State {StateKey} has not been added to the graph. Use AddState before setting state info.");

                stateInfo.SubgraphComplete = complete ?? stateInfo.SubgraphComplete;
                stateInfo.PolicyValue = policyValue ?? stateInfo.PolicyValue;

                stateInfoLookup.Remove(StateKey);
                stateInfoLookup.TryAdd(StateKey, stateInfo);

                return this;
            }

            public ActionContext AddAction(TActionKey actionKey, bool complete = false, float3 actionValue = default)
            {
                var policyGraph = Builder.PolicyGraph;
                var addedAction = policyGraph.ActionInfoLookup.TryAdd(new StateActionPair<TStateKey, TActionKey>(StateKey, actionKey), new ActionInfo()
                {
                    SubgraphComplete = complete,
                    ActionValue = actionValue
                });

                if (!addedAction)
                    throw new ArgumentException($"Action {actionKey} for state {StateKey} has already been added to the policy graph.");

                var actionLookup = policyGraph.ActionLookup;
                if (actionLookup.TryGetFirstValue(StateKey, out var otherActionKey, out var iterator))
                {
                    do
                    {

                        if (actionKey.Equals(otherActionKey))
                            throw new ArgumentException($"Action {actionKey} already added to State {StateKey}. Ensure action keys are unique for a state.");

                    } while (actionLookup.TryGetNextValue(out otherActionKey, ref iterator));
                }

                actionLookup.Add(StateKey, actionKey);

                return WithAction(actionKey);
            }

            public ActionContext WithAction(TActionKey actionKey)
            {
                if (!Builder.PolicyGraph.ActionInfoLookup.TryGetValue(new StateActionPair<TStateKey, TActionKey>(StateKey, actionKey), out _))
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

            public ActionContext UpdateInfo(int? visitCount = null, bool? complete = null, float3? actionValue = null)
            {
                var actionInfoLookup = Builder.PolicyGraph.ActionInfoLookup;
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>(StateKey, ActionKey);
                if (!actionInfoLookup.TryGetValue(stateActionPair, out var actionInfo))
                    throw new ArgumentException($"Action {ActionKey} for state {StateKey} does not exist in policy graph.");

                actionInfo.SubgraphComplete = complete ?? actionInfo.SubgraphComplete;
                actionInfo.ActionValue = actionValue ?? actionInfo.ActionValue;

                actionInfoLookup.Remove(stateActionPair);
                actionInfoLookup.TryAdd(stateActionPair, actionInfo);

                return this;
            }

            public ActionContext AddResultingState(TStateKey resultingStateKey, bool complete = false, float3 value = default,
                float probability = 1f, float transitionUtility = 0f)
            {
                Builder.AddState(resultingStateKey, complete, value);

                var policyGraph = Builder.PolicyGraph;
                bool addedResult = policyGraph.StateTransitionInfoLookup.TryAdd(new StateTransition<TStateKey, TActionKey>(StateKey, ActionKey, resultingStateKey), new StateTransitionInfo
                {
                    Probability = probability,
                    TransitionUtilityValue = transitionUtility
                });

                if (!addedResult)
                    throw new ArgumentException($"Resulting state {resultingStateKey} has already been added for action {ActionKey}. Add each resulting state only once.");

                var resultingStateLookup = policyGraph.ResultingStateLookup;

                if (resultingStateLookup.TryGetFirstValue(new StateActionPair<TStateKey, TActionKey>(StateKey, ActionKey), out var existingResultingStateKey, out var iterator))
                {
                    do
                    {
                        if (resultingStateKey.Equals(existingResultingStateKey))
                            throw new ArgumentException($"Resulting state {resultingStateKey} has already been added for action {ActionKey}. Add each resulting state only once.");
                    } while (resultingStateLookup.TryGetNextValue(out existingResultingStateKey, ref iterator));
                }

                resultingStateLookup.Add(new StateActionPair<TStateKey, TActionKey>(StateKey, ActionKey), resultingStateKey);

                policyGraph.PredecessorGraph.AddValueIfUnique(resultingStateKey, StateKey);

                return this;
            }
        }
    }
}
