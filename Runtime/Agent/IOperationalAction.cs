
namespace Unity.AI.Planner.Agent
{
    /// <summary>
    /// The status of an operational action executing outside of the planner (used for monitoring)
    /// </summary>
    public enum OperationalActionStatus
    {
        /// <summary>
        /// Still executing
        /// </summary>
        InProgress,
        /// <summary>
        /// Interrupted or aborted
        /// </summary>
        NoLongerValid,
        /// <summary>
        /// Finished executing
        /// </summary>
        Completed
    }

    /// <summary>
    /// An interface used to mark implementations of operational actions. Base interface for <see cref="IOperationalAction{TAgent,TStateData,TAction}"/>.
    /// </summary>
    public interface IOperationalAction { }

    /// <summary>
    /// The required interface for operational actions, as used in executing a plan
    /// </summary>
    /// <typeparam name="TAgent">Agent type</typeparam>
    /// <typeparam name="TStateData">StateData type (custom per domain)</typeparam>
    /// <typeparam name="TAction">Action type</typeparam>
    public interface IOperationalAction<TAgent, TStateData, TAction>: IOperationalAction
        where TAction : IActionKey
    {
        /// <summary>
        /// Begins the execution of the operational action
        /// </summary>
        /// <param name="state">Current state</param>
        /// <param name="action">Action context for the planner representation of the operational action</param>
        /// <param name="agent">The agent enacting the operational action</param>
        void BeginExecution(TStateData state, TAction action, TAgent agent);

        /// <summary>
        /// Continues the execution of the operational action
        /// </summary>
        /// <param name="state">Current state</param>
        /// <param name="action">Action context for the planner representation of the operational action</param>
        /// <param name="agent">The agent enacting the operational action</param>
        void ContinueExecution(TStateData state, TAction action, TAgent agent);

        /// <summary>
        /// Ends the execution of the operational action
        /// </summary>
        /// <param name="state">Current state</param>
        /// <param name="action">Action context for the planner representation of the operational action</param>
        /// <param name="agent">The agent enacting the operational action</param>
        void EndExecution(TStateData state, TAction action, TAgent agent);

        /// <summary>
        /// Reports the execution status of the operational action
        /// </summary>
        /// <param name="state">Current state</param>
        /// <param name="action">Action context for the planner representation of the operational action</param>
        /// <param name="agent">The agent enacting the operational action</param>
        /// <returns>Returns the status of the operational action</returns>
        OperationalActionStatus Status(TStateData state, TAction action, TAgent agent);
    }
}
