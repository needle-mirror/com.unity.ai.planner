using System;
using System.Collections.Generic;

namespace Unity.AI.Planner
{
    interface IPlan
    {
        /// <summary>
        /// The number of states in the plan.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Returns the plan information for a given state key.
        /// </summary>
        /// <param name="stateKey">The key for the state</param>
        /// <param name="stateInfo">The state info for the state key</param>
        /// <returns>Returns true if the given state was found</returns>
        bool TryGetStateInfo(IStateKey stateKey, out StateInfo stateInfo);

        /// <summary>
        /// Populates a list of action keys for a given state key.
        /// </summary>
        /// <param name="stateKey">The key for the state</param>
        /// <param name="actionKeys">A list of action key to be populated</param>
        /// <returns>Returns the number of action keys</returns>
        int GetActions(IStateKey stateKey, List<IActionKey> actionKeys);

        /// <summary>
        /// Returns the action key for the optimal action for a given state.
        /// </summary>
        /// <param name="stateKey">The key for the state</param>
        /// <param name="actionKey">The optimal action key</param>
        /// <returns>Returns the action key for the optimal action for a given state</returns>
        bool TryGetOptimalAction(IStateKey stateKey, out IActionKey actionKey);

        /// <summary>
        /// Returns the plan information for a given state and action.
        /// </summary>
        /// <param name="stateKey">The key for the state</param>
        /// <param name="actionKey">The key for action</param>
        /// <param name="actionInfo">The action information for the plan for a given state/action</param>
        /// <returns>Returns true if plan information given state and action was found.</returns>
        bool TryGetActionInfo(IStateKey stateKey, IActionKey actionKey, out ActionInfo actionInfo);

        /// <summary>
        /// Returns a list of potential states resulting from taking an action in a given state.
        /// </summary>
        /// <param name="stateKey">The key of the state in which the action is taken</param>
        /// <param name="actionKey">The key of the action taken</param>
        /// <param name="resultingStateKeys">A list of resulting state keys to be populated</param>
        /// <returns>Returns a list of potential states resulting from taking an action in a given state.</returns>
        int GetResultingStates(IStateKey stateKey, IActionKey actionKey, List<IStateKey> resultingStateKeys);

        /// <summary>
        /// Returns the plan information for a given state transition.
        /// </summary>
        /// <param name="originatingStateKey">The key of the originating state</param>
        /// <param name="actionKey">The key of the action</param>
        /// <param name="resultingStateKey">The key of the resulting state</param>
        /// <param name="stateTransitionInfo">The state transition info</param>
        /// <returns>Returns true if a given state transition was found</returns>
        bool TryGetStateTransitionInfo(IStateKey originatingStateKey, IActionKey actionKey, IStateKey resultingStateKey, out StateTransitionInfo stateTransitionInfo);
    }
}
