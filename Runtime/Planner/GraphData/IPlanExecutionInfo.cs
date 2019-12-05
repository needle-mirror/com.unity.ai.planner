using System.Collections.Generic;

namespace Unity.AI.Planner
{
    interface IPlanExecutionInfo
    {
        IStateManagerInternal StateManager { get; }

        IStateKey RootStateKey { get; }

        int MaxHorizonFromRoot { get; }

        IActionKey CurrentAction { get; }

        int Size { get; }

        bool PlanExists { get; }

        bool GetOptimalAction(IStateKey stateKey, out IActionKey actionKey);

        string GetActionName(IActionKey actionKey);

        ActionInfo GetActionInfo((IStateKey, IActionKey) stateActionKey);

        int GetActions(IStateKey stateKey, List<IActionKey> actionKeys);

        int GetActionResults((IStateKey, IActionKey) stateActionKey, List<(StateTransitionInfo, IStateKey)> stateTransitions);

        StateInfo GetStateInfo(IStateKey stateKey);
    }
}
