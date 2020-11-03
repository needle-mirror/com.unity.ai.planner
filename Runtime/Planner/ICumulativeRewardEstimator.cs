namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of a cumulative reward estimator
    /// </summary>
    public interface ICumulativeRewardEstimator { }

    /// <summary>
    /// An interface that marks an implementation of a cumulative reward estimator for states in a specific domain
    /// </summary>
    /// <typeparam name="TStateData"></typeparam>
    public interface ICumulativeRewardEstimator<TStateData> : ICumulativeRewardEstimator
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
    /// A specialized interface of <see cref="ICumulativeRewardEstimator{TStateData}"/> that must be derived from to create a custom cumulative reward estimator
    /// </summary>
    /// <typeparam name="TStateData">State to evaluate</typeparam>
    public interface ICustomCumulativeRewardEstimator<TStateData> : ICumulativeRewardEstimator<TStateData>
        where TStateData : struct
    {
    }
}
