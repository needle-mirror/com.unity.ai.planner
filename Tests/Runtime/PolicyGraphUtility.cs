using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.AI.Planner.Tests.Performance
{
    static class PolicyGraphUtility
    {
        internal static int GetTotalNodeCountForTreeDepth(int subNodesPerNode, int depth)
        {
            if (subNodesPerNode == 1)
                return depth;
            // https://stackoverflow.com/questions/515214/total-number-of-nodes-in-a-tree-data-structure
            return (int)(Math.Pow(subNodesPerNode, depth) - 1) / (subNodesPerNode - 1);
        }

        /// <summary>
        /// Will build a tree with N subnodes per node = actionsPerState * resultsPerAction;
        /// A depth of 1 will result in a single root node
        /// </summary>
        internal static PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> BuildTree(int actionsPerState = 2, int resultsPerAction = 2, int depth = 10)
        {
            Debug.Assert(depth > 0);

            // tree
            float probabilityPerResult = 1f / resultsPerAction;
            int nextStateIndex = 0;
            var subNodesPerNode = actionsPerState * resultsPerAction;
            int totalStates = GetTotalNodeCountForTreeDepth(subNodesPerNode, depth);

            var policyGraph = new PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo>(totalStates, totalStates, totalStates);
            var builder = new PolicyGraphBuilder<int, int> { PolicyGraph = policyGraph };
            var queue = new NativeQueue<int>(Allocator.TempJob);

            // Add root
            int rootStateIndex = nextStateIndex;
            nextStateIndex++;
            builder.AddState(rootStateIndex);
            queue.Enqueue(rootStateIndex);

            for (int horizon = 0; horizon < depth - 1; horizon++)
            {
                var statesInHorizon = Math.Pow(actionsPerState * resultsPerAction, horizon);
                for (int i = 0; i < statesInHorizon; i++)
                {
                    var state = queue.Dequeue();
                    var stateContext = builder.WithState(state);

                    for (int actionIndex = 0; actionIndex < actionsPerState; actionIndex++)
                    {
                        var actionContext = stateContext.AddAction(actionIndex);

                        for (int j = 0; j < resultsPerAction; j++)
                        {
                            var newStateIndex = nextStateIndex;
                            nextStateIndex++;
                            actionContext.AddResultingState(newStateIndex, probability: probabilityPerResult);
                            queue.Enqueue(newStateIndex);
                        }
                    }
                }
            }

            queue.Dispose();

            return policyGraph;
        }

        internal static PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> BuildLattice(int midLatticeDepth = 10)
        {
            int nextStateIndex = 0;
            int totalStates = (int)Math.Pow(2, midLatticeDepth) * 2;

            var policyGraph = new PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo>(totalStates, totalStates, totalStates);
            var builder = new PolicyGraphBuilder<int, int> { PolicyGraph = policyGraph };
            var queue = new NativeQueue<int>(Allocator.TempJob);

            // Add root
            int rootStateIndex = nextStateIndex;
            nextStateIndex++;
            builder.AddState(rootStateIndex);
            queue.Enqueue(rootStateIndex);

            for (int horizon = 0; horizon < midLatticeDepth; horizon++)
            {
                var statesInHorizon = Math.Pow(2, horizon);
                for (int i = 0; i < statesInHorizon; i++)
                {
                    var state = queue.Dequeue();
                    var stateContext = builder.WithState(state);

                    var leftIndex = i == 0 ? nextStateIndex : nextStateIndex - 1;
                    nextStateIndex++;
                    stateContext.AddAction(0).AddResultingState(leftIndex);
                    if (i == 0)
                        queue.Enqueue(leftIndex);

                    var rightIndex = nextStateIndex;
                    nextStateIndex++;
                    stateContext.AddAction(1).AddResultingState(rightIndex);
                    queue.Enqueue(rightIndex);
                }
            }

            for (int horizon = midLatticeDepth - 1; horizon >= 0; horizon--)
            {
                var statesInHorizon = Math.Pow(2, horizon);
                for (int i = 0; i < statesInHorizon; i++)
                {
                    var state = queue.Dequeue();
                    var stateContext = builder.WithState(state);

                    if (i > 0)
                    {
                        var leftIndex = nextStateIndex - 1;
                        stateContext.AddAction(0).AddResultingState(leftIndex);
                        //queue.Enqueue(leftIndex);
                    }

                    if (i < statesInHorizon - 1)
                    {
                        var rightIndex = nextStateIndex;
                        nextStateIndex++;
                        stateContext.AddAction(1).AddResultingState(rightIndex);
                        queue.Enqueue(rightIndex);
                    }
                }
            }

            queue.Dispose();

            return policyGraph;
        }

        internal static void AddSelfCycles(PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> policyGraph, int actionKey = -1)
        {
            var builder = new PolicyGraphBuilder<int, int> { PolicyGraph = policyGraph };
            var stateInfoLookup = policyGraph.StateInfoLookup;
            using (var stateKeyArray = stateInfoLookup.GetKeyArray(Allocator.TempJob))
            {
                foreach (var stateKey in stateKeyArray)
                {
                    if (policyGraph.ActionLookup.TryGetFirstValue(stateKey, out _, out _))
                        builder.WithState(stateKey).AddAction(actionKey).AddResultingState(stateKey);
                }
            }
        }

        internal static void AddRootCycles(PolicyGraph<int, StateInfo, int, ActionInfo, StateTransitionInfo> policyGraph, int rootKey, int actionKey = -2)
        {
            var builder = new PolicyGraphBuilder<int, int> { PolicyGraph = policyGraph };
            var stateInfoLookup = policyGraph.StateInfoLookup;
            using (var stateKeyArray = stateInfoLookup.GetKeyArray(Allocator.TempJob))
            {
                foreach (var stateKey in stateKeyArray)
                {
                    if (policyGraph.ActionLookup.TryGetFirstValue(stateKey, out _, out _))
                        builder.WithState(stateKey).AddAction(actionKey).AddResultingState(rootKey);
                }
            }
        }
    }
}
