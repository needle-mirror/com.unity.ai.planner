using System.Collections.Generic;

namespace Unity.AI.Planner
{
    interface IPlanInternal
    {
        IStateManagerInternal StateManager { get; }

        IStateKey RootStateKey { get; }

        int MaxHorizonFromRoot { get; }

        IActionKey CurrentAction { get; }

        bool GetOptimalAction(IStateKey stateKey, out IActionKey actionKey);

        string GetActionName(IActionKey actionKey);

        ActionInfo GetActionInfo((IStateKey, IActionKey) stateActionKey);

        int GetActions(IStateKey stateKey, List<IActionKey> actionKeys);

        int GetActionResults((IStateKey, IActionKey) stateActionKey, List<(ActionResult, IStateKey)> actionResults);

        StateInfo GetStateInfo(IStateKey stateKey);
    }
}
