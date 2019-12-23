
namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of a data structure used by the planner to store the
    /// current state of a search process.
    /// </summary>
    /// <typeparam name="TStateKey">StateKey type</typeparam>
    /// <typeparam name="TActionKey">ActionKey type</typeparam>
    interface ISearchContext<TStateKey, TActionKey>
    {
        /// <summary>
        /// A key to access the root state of the plan
        /// </summary>
        TStateKey RootStateKey { get; }

        /// <summary>
        /// Assigns the initial root state.
        /// </summary>
        /// <param name="rootStateKey">State key corresponding to the state for the new plan root</param>
        void RegisterRoot(TStateKey rootStateKey);

        /// <summary>
        /// Updates the root state to the given state
        /// </summary>
        /// <param name="rootStateKey">State key corresponding to the state for the new plan root</param>
        void UpdateRootState(TStateKey rootStateKey);

        /// <summary>
        /// Resets the plan with a new state for the root
        /// </summary>
        /// <param name="rootStateKey">State key corresponding to the state for the new plan root</param>
        void Reset(TStateKey rootStateKey);
    }
}
