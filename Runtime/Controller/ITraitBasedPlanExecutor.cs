using System;
using Unity.AI.Planner.Controller;
using UnityEngine;

namespace Unity.AI.Planner.Traits
{
    /// <summary>
    /// An interface denoting the implementation of a plan executor for trait-based planning domains.
    /// </summary>
    public interface ITraitBasedPlanExecutor : IPlanExecutor, IDisposable
    {
        /// <summary>
        /// Specifies the settings for the execution of the plan, as well as callbacks to invoke under certain conditions.
        /// </summary>
        /// <param name="actor">A MonoBehaviour used to start and stop coroutines.</param>
        /// <param name="actionExecutionInfos">Action execution information for the actions contained in the problem definition.</param>
        /// <param name="executionSettings">Settings governing the execution of the plan</param>
        /// <param name="onActionComplete">A callback to invoke at the completion of each action</param>
        /// <param name="onTerminalStateReached">A callback to invoke once a terminal state is reached by the executor</param>
        /// <param name="onUnexpectedState">A callback to invoke if the executor enters a state not contained within the plan</param>
        void SetExecutionSettings(MonoBehaviour actor, ActionExecutionInfo[] actionExecutionInfos, PlanExecutionSettings executionSettings, Action<IActionKey> onActionComplete = null, Action<IStateKey> onTerminalStateReached = null, Action<IStateKey> onUnexpectedState = null);
    }
}
