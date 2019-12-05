namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of a heuristic for estimating the value of a state in a specific domain
    /// </summary>
    /// <typeparam name="TStateData"></typeparam>
    public interface IHeuristic<TStateData>
        where TStateData : struct
    {
        /// <summary>
        /// Evaluate a state to provide an estimate
        /// </summary>
        /// <param name="stateData">State to evaluate</param>
        /// <returns>Value estimate of the state</returns>
        BoundedValue Evaluate(TStateData stateData);
    }

    /// <summary>
    /// A specialized interface of <see cref="IHeuristic{TStateData}"/> that must be derived from to create a custom heuristic
    /// </summary>
    /// <typeparam name="TStateData">State to evaluate</typeparam>
    public interface ICustomHeuristic<TStateData> : IHeuristic<TStateData>
        where TStateData : struct
    {
    }
}
