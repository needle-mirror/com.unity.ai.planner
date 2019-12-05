using System;
using System.Collections.Generic;

namespace Unity.AI.Planner.Controller
{
    enum ActionComplete
    {
        UseNextPlanState,
        UseWorldState,
    }

    interface IActionExecutionInfo
    {
        object InvokeMethod(object[] arguments);
        Type GetParameterType(int argumentIndex);
        IEnumerable<string> GetArgumentValues();
        ActionComplete OnActionComplete { get; }
    }
}
