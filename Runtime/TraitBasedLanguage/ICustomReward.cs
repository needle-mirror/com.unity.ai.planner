namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Custom implementation of a reward modifier for termination state
    /// </summary>
    /// <typeparam name="TStateData">IStateData Type</typeparam>
    public interface ICustomTerminationReward<TStateData>
        where TStateData : struct, IStateData
    {
        /// <summary>
        /// Return the value of a reward for a given state
        /// </summary>
        /// <param name="state">Current state</param>
        /// <returns>Reward value modification</returns>
        float RewardModifier(TStateData state);
    }

    /// <summary>
    /// Custom implementation of a reward modifier for action state
    /// </summary>
    /// <typeparam name="TStateData">IStateData Type</typeparam>
    public interface ICustomActionReward<TStateData>
        where TStateData : struct, IStateData
    {
        /// <summary>
        /// Return the value of a reward for a given state and action
        /// </summary>
        /// <param name="originalState">State before effects were applied</param>
        /// <param name="action">Key index of the action evaluated</param>
        /// <param name="newState">State after effects were applied</param>
        /// <returns>Reward value modification</returns>
        float RewardModifier(TStateData originalState, ActionKey action, TStateData newState);
    }

    /// <summary>
    /// Custom implementation of a reward modifier based on trait data
    /// </summary>
    /// <typeparam name="TTrait1">Trait type</typeparam>
    public interface ICustomTraitReward<TTrait1>
        where TTrait1 : struct, ITrait
    {
        /// <summary>
        /// Return the value of a reward for a given trait
        /// </summary>
        /// <param name="trait">A trait from the action evaluated</param>
        /// <returns>Reward value modification</returns>
        float RewardModifier(TTrait1 trait);
    }

    /// <summary>
    /// Custom implementation of a reward modifier based on trait data
    /// </summary>
    /// <typeparam name="TTrait1">Trait type</typeparam>
    /// <typeparam name="TTrait2">Trait type</typeparam>
    public interface ICustomTraitReward<TTrait1, TTrait2>
        where TTrait1 : struct, ITrait
        where TTrait2 : struct, ITrait
    {
        /// <summary>
        /// Return the value of a reward for 2 given traits
        /// </summary>
        /// <param name="trait1">A trait from the action evaluated</param>
        /// <param name="trait2">A trait from the action evaluated</param>
        /// <returns>Reward value modification</returns>
        float RewardModifier(TTrait1 trait1, TTrait2 trait2);
    }

    /// <summary>
    /// Custom implementation of a reward modifier based on trait data
    /// </summary>
    /// <typeparam name="TTrait1">Trait type</typeparam>
    /// <typeparam name="TTrait2">Trait type</typeparam>
    /// <typeparam name="TTrait3">Trait type</typeparam>
    public interface ICustomTraitReward<TTrait1, TTrait2, TTrait3>
        where TTrait1 : struct, ITrait
        where TTrait2 : struct, ITrait
        where TTrait3 : struct, ITrait
    {
        /// <summary>
        /// Return the value of a reward for 3 given traits
        /// </summary>
        /// <param name="trait1">A trait from the action evaluated</param>
        /// <param name="trait2">A trait from the action evaluated</param>
        /// <param name="trait3">A trait from the action evaluated</param>
        /// <returns>Reward value modification</returns>
        float RewardModifier(TTrait1 trait1, TTrait2 trait2, TTrait3 trait3);
    }
}
