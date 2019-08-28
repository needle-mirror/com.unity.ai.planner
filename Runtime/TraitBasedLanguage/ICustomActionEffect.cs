namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Custom implementation of an action effect
    /// </summary>
    /// <typeparam name="TStateData">State data Type</typeparam>
    public interface ICustomActionEffect<TStateData>
        where TStateData : struct, IStateData
    {
        /// <summary>
        /// Apply custom modifications on a state given an action
        /// </summary>
        /// <param name="originalState">>State before basic effects were applied</param>
        /// <param name="action">Key index of the action evaluated</param>
        /// <param name="newState">State after basic effects were applied</param>
        void ApplyCustomActionEffectsToState(TStateData originalState, ActionKey action, TStateData newState);
    }
}
