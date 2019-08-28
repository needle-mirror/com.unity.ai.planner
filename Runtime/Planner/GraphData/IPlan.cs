using System;

namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of a data structure used by the planner to store the results of planning
    /// </summary>
    /// <typeparam name="TStateKey">StateKey type</typeparam>
    /// <typeparam name="TActionKey">ActionKey type</typeparam>
    public interface IPlan<TStateKey, TActionKey> : IDisposable
        where TStateKey : struct
    {
        /// <summary>
        /// A key to access the root state of the plan
        /// </summary>
        TStateKey RootStateKey { get; }

        /// <summary>
        /// Returns the key for the optimal action for the given state.
        /// </summary>
        /// <param name="stateKey">Key for state to access</param>
        /// <param name="actionKey">Key to hold optimal action key</param>
        /// <returns>Whether an optimal action was found</returns>
        bool GetOptimalAction(TStateKey stateKey, out TActionKey actionKey);

        /// <summary>
        /// Updates the root state to the given state
        /// </summary>
        /// <param name="rootStateKey">State key corresponding to the state for the new plan root</param>
        void UpdatePlan(TStateKey rootStateKey);

        /// <summary>
        /// Resets the plan with a new state for the root
        /// </summary>
        /// <param name="rootStateKey">State key corresponding to the state for the new plan root</param>
        void Reset(TStateKey rootStateKey);
    }
}
