namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Custom implementation of a reward
    /// </summary>
    /// <typeparam name="TStateData">State data Type</typeparam>
    public interface ICustomReward<TStateData>
        where TStateData : struct, IStateData
    {
        /// <summary>
        /// Modify the value of a reward for a given state and action
        /// </summary>
        /// <param name="originalState">State before effects were applied</param>
        /// <param name="action">Key index of the action evaluated</param>
        /// <param name="newState">State after effects were applied</param>
        /// <param name="reward">Base reward</param>
        void SetCustomReward(TStateData originalState, ActionKey action, TStateData newState, ref float reward);
    }
}
