using System;
using UnityEngine.AI.Planner;

namespace Unity.AI.Planner
{
    interface IPlanExecutor
    {
        /// <summary>
        /// The plan to be executed.
        /// </summary>
        IPlan Plan { get; }

        /// <summary>
        /// The state key of the current executor state, as used by the executor to track the execution of the plan.
        /// </summary>
        IStateKey CurrentExecutorStateKey { get; }

        /// <summary>
        /// The state key of the plan state corresponding to the executor's current state
        /// </summary>
        IStateKey CurrentPlanStateKey { get; }

        /// <summary>
        /// State data for the executor's current state.
        /// </summary>
        IStateData CurrentStateData { get; }

        /// <summary>
        /// The action key of the current action in the plan.
        /// </summary>
        IActionKey CurrentActionKey { get; }

        /// <summary>
        /// The status of the plan executor.
        /// </summary>
        PlanExecutionStatus Status { get; }

        /// <summary>
        /// The scheduler for the planning jobs.
        /// </summary>
        IPlannerScheduler PlannerScheduler { get; }

        /// <summary>
        /// Sets the plan for the executor to enact.
        /// </summary>
        /// <param name="plan">The plan to enact.</param>
        void SetPlan(IPlan plan);

        /// <summary>
        /// Specifies the settings for the execution of the plan, as well as callbacks to invoke under certain conditions.
        /// </summary>
        /// <param name="executionSettings">Settings governing the execution of the plan</param>
        /// <param name="onActionComplete">A callback to invoke at the completion of each action</param>
        /// <param name="onTerminalStateReached">A callback to invoke once a terminal state is reached by the executor</param>
        /// <param name="onUnexpectedState">A callback to invoke if the executor enters a state not contained within the plan</param>
        void SetExecutionSettings(PlanExecutionSettings executionSettings, Action<IActionKey> onActionComplete = null, Action<IStateKey> onTerminalStateReached = null, Action<IStateKey> onUnexpectedState = null);

        /// <summary>
        /// Updates the current state used by the executor.
        /// </summary>
        /// <param name="stateKey"></param>
        void UpdateCurrentState(IStateKey stateKey);

        /// <summary>
        ///  Updates the current state used by the controller to track execution of the plan.
        /// </summary>
        /// <param name="stateData">The state data representing the state to be used.</param>
        void UpdateCurrentState(IStateData stateData);

        /// <summary>
        /// Checks the state of the executor and of the plan against the criteria from the plan execution settings. If
        /// the criteria are met, the executor is ready to initiate the next action.
        /// </summary>
        /// <returns>Returns true if the plan execution criteria are met and false, otherwise.</returns>
        bool ReadyToAct();

        /// <summary>
        /// Initiates the next action of the plan. By default, the action with the highest value is chosen.
        /// </summary>
        /// <param name="overrideAction">An optionally specified action to enact.</param>
        void ExecuteNextAction(IActionKey overrideAction = null);

        /// <summary>
        /// Stops the execution of the current action.
        /// </summary>
        void StopExecution();

        /// <summary>
        /// Returns the name of an action given the action key.
        /// </summary>
        /// <param name="actionKey">The key of the action.</param>
        /// <returns>Returns the name of an action given the action key.</returns>
        string GetActionName(IActionKey actionKey);

        /// <summary>
        /// Returns parameter data string for a given action key.
        /// </summary>
        /// <param name="stateKey">The key for the state.</param>
        /// <param name="actionKey">The key for the action.</param>
        /// <returns>Returns parameter name and data string for a given action key.</returns>
        IActionParameterInfo[] GetActionParametersInfo(IStateKey stateKey, IActionKey actionKey);
    }
}
