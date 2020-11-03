namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of a state termination evaluator. Terminal states are not evaluated
    /// further by the planner.
    /// </summary>
    public interface ITerminationEvaluator { }

    /// <summary>
    /// An interface that marks an implementation of a state termination evaluator. Terminal states are not evaluated
    /// further by the planner.
    /// </summary>
    /// <typeparam name="TStateData"></typeparam>
    interface ITerminationEvaluator<TStateData> : ITerminationEvaluator
        where TStateData : struct
    {
        /// <summary>
        /// Evaluate whether a state is terminal
        /// </summary>
        /// <param name="stateData">State to evaluation termination criteria</param>
        /// <param name="terminalReward">The reward for satisfying the termination criteria</param>
        /// <returns>Whether the state is terminal or not</returns>
        bool IsTerminal(TStateData stateData, out float terminalReward);
    }
}
