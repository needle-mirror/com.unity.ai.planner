using System;
using System.Collections.Generic;

namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface marking the implementation of a plan.
    /// </summary>
    public interface IPlan
    {
        /// <summary>
        /// The number of states in the plan.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// The length of the longest sequence of actions within the plan graph starting from the root state.
        /// </summary>
        int MaxPlanDepth { get; }

        /// <summary>
        /// The state key for the root state of the plan.
        /// </summary>
        IStateKey RootStateKey { get; }

        /// <summary>
        /// Given an input state, will return true if an equivalent plan state is contained within the plan, outputting
        /// the corresponding plan state key. Returns false otherwise.
        /// </summary>
        /// <param name="stateKey">The state for which an equivalent plan state is sought.</param>
        /// <param name="matchingPlanStateKey">The state key for the plan state equivalent to stateKey.</param>
        /// <returns>Returns true if an equivalent plan state is contained within the plan and false otherwise.</returns>
        bool TryGetEquivalentPlanState(IStateKey stateKey, out IStateKey matchingPlanStateKey);

        /// <summary>
        /// Returns the plan information for a given state key.
        /// </summary>
        /// <param name="planStateKey">The key for the state</param>
        /// <param name="stateInfo">The state info for the state key</param>
        /// <returns>Returns true if the given state was found</returns>
        bool TryGetStateInfo(IStateKey planStateKey, out StateInfo stateInfo);

        /// <summary>
        /// Returns the state data for the current state of the plan.
        /// </summary>
        /// <param name="stateKey">The key of the state for which state data is requested.</param>
        /// <returns>Returns the state data for the current state of the plan.</returns>
        IStateData GetStateData(IStateKey stateKey);

        /// <summary>
        /// Returns true if the state meets plan termination conditions or has no valid actions. Returns false otherwise.
        /// </summary>
        /// <param name="planStateKey">The key of the plan state to evaluate.</param>
        /// <returns>Returns true if the state meets plan termination conditions or has no valid actions. Returns false otherwise.</returns>
        bool IsTerminal(IStateKey planStateKey);

        /// <summary>
        /// Populates a list of action keys for a given state key.
        /// </summary>
        /// <param name="planStateKey">The key for the state</param>
        /// <param name="actionKeys">A list of action key to be populated</param>
        /// <returns>Returns the number of action keys</returns>
        int GetActions(IStateKey planStateKey, IList<IActionKey> actionKeys);

        /// <summary>
        /// Returns the action key for the optimal action for a given state.
        /// </summary>
        /// <param name="planStateKey">The key for the state</param>
        /// <param name="actionKey">The optimal action key</param>
        /// <returns>Returns the action key for the optimal action for a given state</returns>
        bool TryGetOptimalAction(IStateKey planStateKey, out IActionKey actionKey);

        /// <summary>
        /// Returns the plan information for a given state and action.
        /// </summary>
        /// <param name="planStateKey">The key for the state</param>
        /// <param name="actionKey">The key for action</param>
        /// <param name="actionInfo">The action information for the plan for a given state/action</param>
        /// <returns>Returns true if plan information given state and action was found.</returns>
        bool TryGetActionInfo(IStateKey planStateKey, IActionKey actionKey, out ActionInfo actionInfo);

        /// <summary>
        /// Returns a list of potential states resulting from taking an action in a given state.
        /// </summary>
        /// <param name="planStateKey">The key of the state in which the action is taken</param>
        /// <param name="actionKey">The key of the action taken</param>
        /// <param name="resultingPlanStateKeys">A list of resulting state keys to be populated</param>
        /// <returns>Returns a list of potential states resulting from taking an action in a given state.</returns>
        int GetResultingStates(IStateKey planStateKey, IActionKey actionKey, IList<IStateKey> resultingPlanStateKeys);

        /// <summary>
        /// Returns the plan information for a given state transition.
        /// </summary>
        /// <param name="originatingPlanStateKey">The key of the originating state</param>
        /// <param name="actionKey">The key of the action</param>
        /// <param name="resultingPlanStateKey">The key of the resulting state</param>
        /// <param name="stateTransitionInfo">The state transition info</param>
        /// <returns>Returns true if a given state transition was found</returns>
        bool TryGetStateTransitionInfo(IStateKey originatingPlanStateKey, IActionKey actionKey, IStateKey resultingPlanStateKey, out StateTransitionInfo stateTransitionInfo);
    }
}
