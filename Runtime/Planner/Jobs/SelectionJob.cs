using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Assertions;
using Random = Unity.Mathematics.Random;


namespace Unity.AI.Planner.Jobs
{
    struct SelectionJob<TStateKey, TActionKey> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        public NativeHashMap<TStateKey, int> StateDepthLookup;

        public NativeHashMap<TStateKey, StateInfo> StateInfoLookup;
        public NativeHashMap<(TStateKey, TActionKey), ActionInfo> ActionInfoLookup;

        [ReadOnly] public TStateKey RootStateKey;
        [ReadOnly] public NativeMultiHashMap<TStateKey, (TStateKey, TActionKey)> StateActionLookup;
        [ReadOnly] public NativeMultiHashMap<(TStateKey, TActionKey), TStateKey> ResultingStateLookup;
        [ReadOnly] public NativeHashMap<(TStateKey, TActionKey, TStateKey), ActionResult> ActionResultLookup;


        [WriteOnly] public NativeList<TStateKey> AllSelectedStates;
        [WriteOnly] public NativeList<TStateKey> SelectedUnexpandedStates;

        Random m_Random;

        public void Execute()
        {
            m_Random = new Random();
            m_Random.InitState();

            var maxActionNodes = new NativeList<((TStateKey, TActionKey), ActionInfo)>(1, Allocator.Temp);

            var stateKey = RootStateKey;

            var stateInfo = StateInfoLookup[stateKey];
            ActionInfo actionInfo;

            if (stateInfo.Complete) // Completed root; no need to expand.
                return;

            int depth = 0;
            while (StateDepthLookup.TryGetValue(stateKey, out var stateDepth)
                && depth <= stateDepth
                && StateActionLookup.TryGetFirstValue(stateKey, out var stateActionKey, out _))
            {
                // Update node visit count
                stateInfo.VisitCount += 1;
                StateInfoLookup.Remove(stateKey);
                StateInfoLookup.TryAdd(stateKey, stateInfo);

                // Select an action via UCT.
                (stateActionKey, actionInfo) = SelectAction(stateKey, stateInfo, maxActionNodes, StateActionLookup, ActionInfoLookup);

                // Update action count.
                actionInfo.VisitCount += 1;
                ActionInfoLookup.Remove(stateActionKey);
                ActionInfoLookup.TryAdd(stateActionKey, actionInfo);

                // Sample a node from the action results.
                (stateKey, stateInfo) = SelectState(stateActionKey, StateInfoLookup, ResultingStateLookup, ActionResultLookup);

                // Increment depth
                depth++;
            }

            if (!StateActionLookup.TryGetFirstValue(stateKey, out _, out _))
                SelectedUnexpandedStates.Add(stateKey);

            AllSelectedStates.Add(stateKey);

            StateDepthLookup.Remove(stateKey);
            StateDepthLookup.TryAdd(stateKey, depth);
        }

        ((TStateKey, TActionKey), ActionInfo) SelectAction(TStateKey stateKey, StateInfo stateInfo,
            NativeList<((TStateKey, TActionKey), ActionInfo)> maxActionNodes,
            NativeMultiHashMap<TStateKey, (TStateKey, TActionKey)> stateActionLookup,
            NativeHashMap<(TStateKey, TActionKey), ActionInfo> actionInfoLookup)
        {
            float explorationFactor = Math.Abs(stateInfo.PolicyValue) + 1;
            float utilityFactor = 0.5f;
            float maxUCTValue = float.MinValue;
            maxActionNodes.Clear();

            stateActionLookup.TryGetFirstValue(stateKey, out var stateActionKey, out var iterator);

            do
            {
                actionInfoLookup.TryGetValue(stateActionKey, out var actionInfo);

                if (actionInfo.Complete)
                    continue;

                var uctValue = actionInfo.ActionValue * utilityFactor + explorationFactor * (float)Math.Sqrt(Math.Log(stateInfo.VisitCount) / actionInfo.VisitCount);

                if (uctValue > maxUCTValue)
                {
                    maxUCTValue = uctValue;
                    maxActionNodes.Clear();
                    maxActionNodes.Add((stateActionKey, actionInfo));
                }
                else if (Math.Abs(uctValue - maxUCTValue) < float.Epsilon)
                {
                    maxActionNodes.Add((stateActionKey, actionInfo));
                }
            }
            while (stateActionLookup.TryGetNextValue(out stateActionKey, ref iterator));

            return maxActionNodes[Math.Abs(m_Random.NextInt()) % maxActionNodes.Length];
        }

        (TStateKey, StateInfo) SelectState((TStateKey, TActionKey) stateActionKey,
            NativeHashMap<TStateKey, StateInfo> stateInfoLookup,
            NativeMultiHashMap<(TStateKey, TActionKey), TStateKey> resultingStateLookup,
            NativeHashMap<(TStateKey, TActionKey, TStateKey), ActionResult> actionResultLookup)
        {
            // Sample successor node to for traversal
            if (!resultingStateLookup.TryGetFirstValue(stateActionKey, out var resultingStateKey, out var iterator))
                Assert.IsTrue(false, "No ActionResults found.");

            float totalProbability = 0f;
            do
            {
                var actionResult = actionResultLookup[(stateActionKey.Item1, stateActionKey.Item2, resultingStateKey)];
                if (!stateInfoLookup[resultingStateKey].Complete) // Don't sample complete nodes.
                    totalProbability += actionResult.Probability;
            }
            while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            if (totalProbability <= 0)
                Assert.IsTrue(totalProbability > 0, $"All child nodes are Complete. {stateActionKey}");

            var chosen = m_Random.NextDouble() * totalProbability;
            totalProbability = 0;
            resultingStateLookup.TryGetFirstValue(stateActionKey, out resultingStateKey, out iterator);
            do
            {
                var actionResult = actionResultLookup[(stateActionKey.Item1, stateActionKey.Item2, resultingStateKey)];
                var stateInfo = stateInfoLookup[resultingStateKey];

                // Don't sample complete nodes.
                if (stateInfo.Complete)
                    continue;

                totalProbability += actionResult.Probability;
                if (totalProbability >= chosen)
                    return (resultingStateKey, stateInfo);
            }
            while (resultingStateLookup.TryGetNextValue(out resultingStateKey, ref iterator));

            throw new Exception("No state selected during graph traversal.");
        }
    }
}
