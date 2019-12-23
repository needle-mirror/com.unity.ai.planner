using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.AI.Planner
{
    struct PolicyGraph<TStateKey, TStateInfo, TActionKey, TActionInfo, TStateTransitionInfo>
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateInfo : struct, IStateInfo
        where TActionKey : struct, IEquatable<TActionKey>
        where TActionInfo : struct, IActionInfo
        where TStateTransitionInfo : struct
    {
        // Graph structure
        public NativeMultiHashMap<TStateKey, TActionKey> ActionLookup;
        public NativeMultiHashMap<StateActionPair<TStateKey, TActionKey>, TStateKey> ResultingStateLookup;
        public NativeMultiHashMap<TStateKey, TStateKey> PredecessorGraph;

        // Graph info
        public NativeHashMap<TStateKey, TStateInfo> StateInfoLookup;
        public NativeHashMap<StateActionPair<TStateKey, TActionKey>, TActionInfo> ActionInfoLookup;
        public NativeHashMap<StateTransition<TStateKey, TActionKey>, TStateTransitionInfo> StateTransitionInfoLookup;

        // Interface
        public int Size => StateInfoLookup.Length;

        public PolicyGraph(int stateCapacity, int actionCapacity)
        {
            ActionLookup = new NativeMultiHashMap<TStateKey, TActionKey>(stateCapacity, Allocator.Persistent);
            ResultingStateLookup = new NativeMultiHashMap<StateActionPair<TStateKey, TActionKey>, TStateKey>(actionCapacity, Allocator.Persistent);
            PredecessorGraph = new NativeMultiHashMap<TStateKey, TStateKey>(stateCapacity, Allocator.Persistent);

            StateInfoLookup = new NativeHashMap<TStateKey, TStateInfo>(stateCapacity, Allocator.Persistent);
            ActionInfoLookup = new NativeHashMap<StateActionPair<TStateKey, TActionKey>, TActionInfo>(actionCapacity, Allocator.Persistent);
            StateTransitionInfoLookup = new NativeHashMap<StateTransition<TStateKey, TActionKey>, TStateTransitionInfo>(actionCapacity, Allocator.Persistent);
        }

        public bool GetOptimalAction(TStateKey stateKey, out TActionKey action)
        {
            bool actionsFound = ActionLookup.TryGetFirstValue(stateKey, out var actionKey, out var iterator);
            if (!actionsFound)
            {
                action = default;
                return false;
            }

            var maxActionValuePair = (default(TActionKey), float.MinValue);

            do
            {
                var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
                ActionInfoLookup.TryGetValue(stateActionPair, out var actionInfo);
                if (actionInfo.ActionValue.Average > maxActionValuePair.Item2)
                    maxActionValuePair = (actionKey, actionInfo.ActionValue.Average);

            } while (ActionLookup.TryGetNextValue(out actionKey, ref iterator));

            action = maxActionValuePair.Item1;
            return true;
        }

        internal void RemoveState(TStateKey stateKey)
        {
            var predecessorQueue = new NativeQueue<TStateKey>(Allocator.Temp);

            // State Info
            StateInfoLookup.Remove(stateKey);

            // Actions
            if (ActionLookup.TryGetFirstValue(stateKey, out var actionKey, out var actionIterator))
            {
                do
                {
                    var stateActionPair = new StateActionPair<TStateKey, TActionKey>(stateKey, actionKey);
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
                            StateTransitionInfoLookup.Remove(new StateTransition<TStateKey, TActionKey>(stateKey, stateActionPair.ActionKey, resultingStateKey));

                        } while (ResultingStateLookup.TryGetNextValue(out resultingStateKey, ref resultIterator));

                        ResultingStateLookup.Remove(stateActionPair);
                    }

                } while (ActionLookup.TryGetNextValue(out actionKey, ref actionIterator));

                ActionLookup.Remove(stateKey);
            }

            // Predecessors
            PredecessorGraph.Remove(stateKey);

            predecessorQueue.Dispose();
        }

        #region ContainerManagement
        public void ExpandBy(int minimumFreeStateCapacity, int minimumFreeActionCapacity)
        {
            if (ActionLookup.Length + minimumFreeActionCapacity > ActionLookup.Capacity)
                ActionLookup.Capacity = Math.Max(ActionLookup.Length + minimumFreeActionCapacity, ActionLookup.Capacity * 2);
            if (ResultingStateLookup.Length + minimumFreeStateCapacity > ResultingStateLookup.Capacity)
                ResultingStateLookup.Capacity = Math.Max(ResultingStateLookup.Length + minimumFreeStateCapacity, ResultingStateLookup.Capacity * 2);
            if (PredecessorGraph.Length + minimumFreeStateCapacity > PredecessorGraph.Capacity)
                PredecessorGraph.Capacity = Math.Max(PredecessorGraph.Length + minimumFreeStateCapacity, PredecessorGraph.Capacity * 2);

            if (StateInfoLookup.Length + minimumFreeStateCapacity > StateInfoLookup.Capacity)
                StateInfoLookup.Capacity = Math.Max(StateInfoLookup.Length + minimumFreeStateCapacity, StateInfoLookup.Capacity * 2);
            if (ActionInfoLookup.Length + minimumFreeActionCapacity > ActionInfoLookup.Capacity)
                ActionInfoLookup.Capacity = Math.Max(ActionInfoLookup.Length + minimumFreeActionCapacity, ActionInfoLookup.Capacity * 2);
            if (StateTransitionInfoLookup.Length + minimumFreeActionCapacity > StateTransitionInfoLookup.Capacity)
                StateTransitionInfoLookup.Capacity = Math.Max(StateTransitionInfoLookup.Length + minimumFreeActionCapacity, StateTransitionInfoLookup.Capacity * 2);
        }

        public void Dispose()
        {
            if (ActionLookup.IsCreated)
                ActionLookup.Dispose();
            if (ResultingStateLookup.IsCreated)
                ResultingStateLookup.Dispose();
            if (PredecessorGraph.IsCreated)
                PredecessorGraph.Dispose();

            if (StateInfoLookup.IsCreated)
                StateInfoLookup.Dispose();
            if (ActionInfoLookup.IsCreated)
                ActionInfoLookup.Dispose();
            if (StateTransitionInfoLookup.IsCreated)
                StateTransitionInfoLookup.Dispose();
        }
        #endregion
    }

    struct StateInfo : IStateInfo
    {
        public BoundedValue PolicyValue;
        public bool SubgraphComplete;

        BoundedValue IStateInfo.PolicyValue => PolicyValue;
        bool IStateInfo.SubgraphComplete => SubgraphComplete;
    }

    struct ActionInfo : IActionInfo
    {
        public BoundedValue ActionValue;
        public bool SubgraphComplete;

        BoundedValue IActionInfo.ActionValue => ActionValue;
        bool IActionInfo.SubgraphComplete => SubgraphComplete;
    }

    /// <summary>
    /// Action result information for an action node in the plan
    /// </summary>
    internal struct StateTransitionInfo
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

    /// <summary>
    /// A bounded estimate of a value, given by an average as well as an upper and lower bound.
    /// </summary>
    public struct BoundedValue
    {
        /// <summary>
        /// The lower bound of the estimated value.
        /// </summary>
        public float LowerBound => m_ValueVector.x;

        /// <summary>
        /// The average estimate of the value.
        /// </summary>
        public float Average => m_ValueVector.y;

        /// <summary>
        /// The upper bound of the estimated value.
        /// </summary>
        public float UpperBound => m_ValueVector.z;

        /// <summary>
        /// The range of the estimated value (range = upper bound - lower bound).
        /// </summary>
        public float Range => m_ValueVector.z - m_ValueVector.x;

        internal float3 m_ValueVector;

        /// <summary>
        /// Constructs a bounded value from a float3 vector (x = lower bound, y = average, z = upper bound).
        /// </summary>
        /// <param name="vector">The vector to be used to set the bounds and average.</param>
        public BoundedValue(float3 vector) => m_ValueVector = vector;

        /// <summary>
        /// Constructs a bounded value.
        /// </summary>
        /// <param name="lb">The value of the lower bound.</param>
        /// <param name="avg">The value of the average.</param>
        /// <param name="ub">The value of the upper bound.</param>
        public BoundedValue(float lb, float avg, float ub) => m_ValueVector = new float3(lb, avg, ub);

        /// <summary>
        /// Converts a BoundedValue to a float3.
        /// </summary>
        /// <param name="value">The BoundedValue instance to convert.</param>
        /// <returns>A float3 representation of the bounded value.</returns>
        public static implicit operator float3 (BoundedValue value) => value.m_ValueVector;

        /// <summary>
        /// Converts a float3 to a BoundedValue.
        /// </summary>
        /// <param name="vector">The vector to convert to a bounded value.</param>
        /// <returns>A BoundedValue representation of the vector.</returns>
        public static implicit operator BoundedValue (float3 vector) => new BoundedValue(vector);

        /// <summary>
        /// Returns the product of a bounded value and a scalar value.
        /// </summary>
        /// <param name="boundedValue">The bounded value to be multiplied.</param>
        /// <param name="scalar">The scalar to be multiplied.</param>
        /// <returns>Returns the product of a bounded value and a scalar value.</returns>
        public static BoundedValue operator *(BoundedValue boundedValue, float scalar)
            => new BoundedValue( scalar * boundedValue.m_ValueVector );

        /// <summary>
        /// Returns the product of a bounded value and a scalar value.
        /// </summary>
        /// <param name="scalar">The scalar to be multiplied.</param>
        /// <param name="boundedValue">The bounded value to be multiplied.</param>
        /// <returns>Returns the product of a bounded value and a scalar value.</returns>
        public static BoundedValue operator *(float scalar, BoundedValue boundedValue)
            => new BoundedValue( scalar * boundedValue.m_ValueVector );

        /// <summary>
        /// Returns the sum of a bounded value and a scalar value.
        /// </summary>
        /// <param name="boundedValue">The bounded value to be added.</param>
        /// <param name="scalar">The scalar to be added.</param>
        /// <returns>Returns the sum of a bounded value and a scalar value.</returns>
        public static BoundedValue operator +(BoundedValue boundedValue, float scalar)
            => new BoundedValue ( boundedValue.m_ValueVector + scalar );

        /// <summary>
        /// Returns the sum of a bounded value and a scalar value.
        /// </summary>
        /// <param name="scalar">The scalar to be added.</param>
        /// <param name="boundedValue">The bounded value to be added.</param>
        /// <returns>Returns the sum of a bounded value and a scalar value.</returns>
        public static BoundedValue operator +(float scalar, BoundedValue boundedValue)
            => new BoundedValue ( boundedValue.m_ValueVector + scalar );

        /// <summary>
        /// Returns the sum of two bounded values.
        /// </summary>
        /// <param name="boundedValue1">The first bounded value to be added.</param>
        /// <param name="boundedValue2">The second bounded value to be added.</param>
        /// <returns>Returns the sum of two bounded values.</returns>
        public static BoundedValue operator +(BoundedValue boundedValue1, BoundedValue boundedValue2)
            => new BoundedValue ( boundedValue1.m_ValueVector + boundedValue2.m_ValueVector );

        /// <summary>
        /// Determines if two bounded values are approximately equal.
        /// </summary>
        /// <param name="other">The BoundedValue instance to compare.</param>
        /// <returns>Returns true if two bounded values are approximately equal.</returns>
        public bool Approximately(BoundedValue other)
            => Approximately(Average, other.Average) &&
               Approximately(LowerBound, other.LowerBound) &&
               Approximately(UpperBound, other.UpperBound);

        static bool Approximately(float a, float b)
        {
            return Mathf.Abs(b - a) < Mathf.Max(1E-06f * Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)), 1.175494E-38f * 8f);
        }

        /// <summary>
        /// Returns a string that represents the bounds
        /// </summary>
        /// <returns>A string that represents the bounds</returns>
        public override string ToString() => $"[{LowerBound:0.00}, {Average:0.00}, {UpperBound:0.00}]";
    }

    /// <summary>
    /// Data representing a state transition and corresponding information for that transition.
    /// </summary>
    /// <typeparam name="TStateKey">The type of state key.</typeparam>
    /// <typeparam name="TActionKey">The type of action key.</typeparam>
    /// <typeparam name="TStateTransitionInfo">The type of state transition information.</typeparam>
    struct StateTransitionInfoPair<TStateKey, TActionKey, TStateTransitionInfo>
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
        where TStateTransitionInfo : struct
    {
        /// <summary>
        /// The state transition.
        /// </summary>
        public StateTransition<TStateKey, TActionKey> StateTransition;

        /// <summary>
        /// Information about the state transition.
        /// </summary>
        public TStateTransitionInfo StateTransitionInfo;

        /// <summary>
        /// Constructor for the StateTransitionInfoPair.
        /// </summary>
        /// <param name="predecessorStateKey">The state key for the originating state of the transition.</param>
        /// <param name="actionKey">The action key for the state transition.</param>
        /// <param name="successorStateKey">The resulting state for the state transition.</param>
        /// <param name="stateTransitionInfo">Information about the state transition.</param>
        public StateTransitionInfoPair(TStateKey predecessorStateKey, TActionKey actionKey, TStateKey successorStateKey, TStateTransitionInfo stateTransitionInfo)
        {
            StateTransition = new StateTransition<TStateKey, TActionKey>(predecessorStateKey, actionKey, successorStateKey);
            StateTransitionInfo = stateTransitionInfo;
        }
    }

    /// <summary>
    /// Data struct for a state key and an action key.
    /// </summary>
    /// <typeparam name="TStateKey">The type of state key.</typeparam>
    /// <typeparam name="TActionKey">The type of action key.</typeparam>
    struct StateActionPair<TStateKey, TActionKey> : IEquatable<StateActionPair<TStateKey, TActionKey>>
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        /// <summary>
        /// The state key.
        /// </summary>
        public TStateKey StateKey;

        /// <summary>
        /// The action key.
        /// </summary>
        public TActionKey ActionKey;

        /// <summary>
        /// Constructs a StateActionPair.
        /// </summary>
        /// <param name="stateKey">The state key.</param>
        /// <param name="actionKey">The action key.</param>
        public StateActionPair(TStateKey stateKey, TActionKey actionKey)
        {
            StateKey = stateKey;
            ActionKey = actionKey;
        }

        /// <summary>
        /// Checks the equality of two StateActionPairs.
        /// </summary>
        /// <param name="other">The StateActionPair to compare to.</param>
        /// <returns>Returns true if the StateActionPairs are equal.</returns>
        public bool Equals(StateActionPair<TStateKey, TActionKey> other)
        {
            return StateKey.Equals(other.StateKey) && ActionKey.Equals(other.ActionKey);
        }

        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (StateKey.GetHashCode() * 397) ^ ActionKey.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Data representing the transition from one state to another given an action.
    /// </summary>
    /// <typeparam name="TStateKey"></typeparam>
    /// <typeparam name="TActionKey"></typeparam>
    struct StateTransition<TStateKey, TActionKey> : IEquatable<StateTransition<TStateKey, TActionKey>>
        where TStateKey : struct, IEquatable<TStateKey>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        /// <summary>
        /// The state key for the originating state of the transition.
        /// </summary>
        public TStateKey PredecessorStateKey;

        /// <summary>
        /// The action key for the action used in the state transition.
        /// </summary>
        public TActionKey ActionKey;

        /// <summary>
        /// The state key for the resulting state of the transition.
        /// </summary>
        public TStateKey SuccessorStateKey;

        /// <summary>
        /// Constructs a StateTransition.
        /// </summary>
        /// <param name="predecessorStateKey">The state key for the originating state of the transition.</param>
        /// <param name="actionKey">The action key for the action used in the state transition.</param>
        /// <param name="successorStateKey">The state key for the resulting state of the transition.</param>
        public StateTransition(TStateKey predecessorStateKey, TActionKey actionKey, TStateKey successorStateKey)
        {
            PredecessorStateKey = predecessorStateKey;
            ActionKey = actionKey;
            SuccessorStateKey = successorStateKey;
        }

        /// <summary>
        /// Constructs a StateTransition.
        /// </summary>
        /// <param name="stateActionPair">The StateActionPair for the originating state and action.</param>
        /// <param name="successorStateKey">The state key for the resulting state of the transition.</param>
        public StateTransition(StateActionPair<TStateKey, TActionKey> stateActionPair, TStateKey successorStateKey)
        {
            PredecessorStateKey = stateActionPair.StateKey;
            ActionKey = stateActionPair.ActionKey;
            SuccessorStateKey = successorStateKey;
        }
        /// <summary>
        /// Checks the equality of two StateTransitions.
        /// </summary>
        /// <param name="other">The StateTransition to compare to.</param>
        /// <returns>Returns true if the StateTransitions are equal.</returns>
        public bool Equals(StateTransition<TStateKey, TActionKey> other)
        {
            return PredecessorStateKey.Equals(other.PredecessorStateKey)
                && ActionKey.Equals(other.ActionKey)
                && SuccessorStateKey.Equals(other.SuccessorStateKey);
        }

        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (((PredecessorStateKey.GetHashCode() * 397) ^ ActionKey.GetHashCode()) * 397) ^ SuccessorStateKey.GetHashCode();
            }
        }
    }
}
