using System;
using System.Collections.Generic;

namespace Unity.AI.Planner.Controller
{
    /// <summary>
    /// Modes designating how a plan executor will acquire a new, current state
    /// </summary>
    public enum PlanExecutorStateUpdateMode
    {
        /// <summary>
        /// Use the predicted resulting state for the last executed action. Only works for deterministic planning domains.
        /// </summary>
        UseNextPlanState,

        /// <summary>
        /// Query the game world for a new state.
        /// </summary>
        UseWorldState,
    }

    interface IActionExecutionInfo
    {
        object InvokeMethod(object[] arguments);
        Type GetParameterType(int argumentIndex);
        IEnumerable<string> GetArgumentValues();
        PlanExecutorStateUpdateMode PlanExecutorStateUpdateMode { get; }
        bool IsValidForAction(string actionName);
    }
}
