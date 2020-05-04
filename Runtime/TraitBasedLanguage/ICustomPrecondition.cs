namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Custom implementation of a precondition for an action
    /// </summary>
    /// <typeparam name="TStateData">State data Type</typeparam>
    public interface ICustomActionPrecondition<TStateData>
        where TStateData : struct, IStateData
    {
        /// <summary>
        /// Check the validity of an action for a given state
        /// </summary>
        /// <param name="state">Current state</param>
        /// <param name="action">Key index of the action evaluated</param>
        /// <returns>True if the action is valid</returns>
        bool CheckCustomPrecondition(TStateData state, ActionKey action);
    }

    /// <summary>
    /// Custom implementation of a precondition for an termination
    /// </summary>
    /// <typeparam name="TStateData">State data Type</typeparam>
    public interface ICustomTerminationPrecondition<TStateData>
        where TStateData : struct, IStateData
    {
        /// <summary>
        /// Check the validity of a termination for a given state
        /// </summary>
        /// <param name="state">Current state</param>
        /// <returns>True if the action is valid</returns>
        bool CheckCustomPrecondition(TStateData state);
    }
}
