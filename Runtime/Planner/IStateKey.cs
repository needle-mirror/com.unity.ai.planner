namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of a state key to access state data in a domain
    /// </summary>
    interface IStateKey
    {
        /// <summary>
        /// A label for the key (for debug purposes)
        /// </summary>
        string Label { get; }
    }
}
