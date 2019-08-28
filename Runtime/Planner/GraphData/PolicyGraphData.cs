using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.AI.Planner
{
    struct PolicyGraph<TStateKey, TStateInfo, TActionKey, TActionInfo, TActionResult> : IDisposable
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateInfo : struct
        where TActionKey : struct, IEquatable<TActionKey>
        where TActionInfo : struct
        where TActionResult : struct
    {
        // Graph structure
        public NativeMultiHashMap<TStateKey, (TStateKey, TActionKey)> StateActionLookup => m_SuccessorGraph.NodeToEdgeLookup;
        public NativeMultiHashMap<(TStateKey, TActionKey), TStateKey> ResultingStateLookup => m_SuccessorGraph.EdgeToNodeLookup;
        public NativeMultiHashMap<TStateKey, TStateKey> PredecessorGraph => m_PredecessorGraph;

        // Graph info
        public NativeHashMap<TStateKey, TStateInfo> StateInfoLookup => m_GraphInfo.NodeInfoLookup;
        public NativeHashMap<(TStateKey, TActionKey), TActionInfo> ActionInfoLookup => m_GraphInfo.EdgeOriginInfoLookup;
        public NativeHashMap<(TStateKey, TActionKey, TStateKey), TActionResult> ActionResultLookup => m_GraphInfo.EdgeDestinationInfoLookup;

        OneToManyDirectedGraph<TStateKey, (TStateKey, TActionKey)> m_SuccessorGraph;
        NativeMultiHashMap<TStateKey, TStateKey> m_PredecessorGraph;
        OneToManyGraphInfo<TStateKey, TStateInfo, (TStateKey, TActionKey), TActionInfo, (TStateKey, TActionKey, TStateKey), TActionResult> m_GraphInfo;

        public PolicyGraph(int stateCapacity, int actionCapacity)
        {
            m_SuccessorGraph = new OneToManyDirectedGraph<TStateKey, (TStateKey, TActionKey)>(stateCapacity, actionCapacity);
            m_PredecessorGraph = new NativeMultiHashMap<TStateKey, TStateKey>(stateCapacity, Allocator.Persistent);
            m_GraphInfo = new OneToManyGraphInfo<TStateKey, TStateInfo, (TStateKey, TActionKey), TActionInfo, (TStateKey, TActionKey, TStateKey), TActionResult>(stateCapacity, actionCapacity);
        }

        internal void RemoveState(TStateKey stateKey)
        {
            var predecessorQueue = new NativeQueue<TStateKey>(Allocator.Temp);

            // State Info
            StateInfoLookup.Remove(stateKey);

            // Actions
            if (StateActionLookup.TryGetFirstValue(stateKey, out var stateActionPair, out var actionIterator))
            {
                do
                {
                    // Action Info
                    ActionInfoLookup.Remove(stateActionPair);

                    // Results
                    if (ResultingStateLookup.TryGetFirstValue(stateActionPair, out var resultingStateKey, out var resultIterator))
                    {
                        do
                        {
                            // Remove Predecessor Link
                            if (PredecessorGraph.TryGetFirstValue(resultingStateKey, out var predecessorKey, out var predecessorIterator))
                            {
                                predecessorQueue.Clear();

                                do
                                {
                                    if (!stateKey.Equals(predecessorKey))
                                    {
                                        predecessorQueue.Enqueue(predecessorKey);
                                    }
                                } while (PredecessorGraph.TryGetNextValue(out predecessorKey, ref predecessorIterator));

                                // Reset Predecessors
                                PredecessorGraph.Remove(resultingStateKey);

                                // Requeue Predecessors
                                while (predecessorQueue.TryDequeue(out var queuedPredecessorKey))
                                {
                                    PredecessorGraph.Add(resultingStateKey, queuedPredecessorKey);
                                }
                            }

                            // Action Result Info
                            ActionResultLookup.Remove((stateKey, stateActionPair.Item2, resultingStateKey));

                        } while (ResultingStateLookup.TryGetNextValue(out resultingStateKey, ref resultIterator));

                        ResultingStateLookup.Remove(stateActionPair);
                    }

                } while (StateActionLookup.TryGetNextValue(out stateActionPair, ref actionIterator));

                StateActionLookup.Remove(stateKey);
            }

            // Predecessors
            PredecessorGraph.Remove(stateKey);

            predecessorQueue.Dispose();
        }

        #region ContainerManagement
        public void ExpandBy(int minimumFreeStateCapacity, int minimumFreeActionCapacity)
        {
            m_SuccessorGraph.ExpandBy(minimumFreeStateCapacity, minimumFreeActionCapacity);
            m_PredecessorGraph.Capacity = Math.Max(PredecessorGraph.Capacity, PredecessorGraph.Length + minimumFreeStateCapacity);
            m_GraphInfo.ExpandBy(minimumFreeStateCapacity, minimumFreeActionCapacity);
        }

        public void Dispose()
        {
            m_SuccessorGraph.Dispose();
            if (m_PredecessorGraph.IsCreated)
                m_PredecessorGraph.Dispose();
            m_GraphInfo.Dispose();
        }
        #endregion
    }

    struct StateInfo
    {
        public int VisitCount;
        public bool Complete;
        public float PolicyValue;
    }

    struct ActionInfo
    {
        public float ActionValue;
        public bool Complete;
        public int VisitCount;

        public override string ToString() => $"ActionInfo: Value={ActionValue} Complete={Complete} VisitCount={VisitCount}";
    }

    /// <summary>
    /// Action result information for an action node in the plan
    /// </summary>
    public struct ActionResult
    {
        /// <summary>
        /// Probability of resulting state occurring
        /// </summary>
        public float Probability;

        /// <summary>
        /// The value (utility) in transitioning to the resulting state
        /// </summary>
        public float TransitionUtilityValue;
    }
}
