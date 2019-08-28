using System;
using Unity.Entities;

namespace Unity.AI.Planner.Agent
{
    class NOOPAction<TAgent, TStateData, TActionKey> : IOperationalAction<TAgent, TStateData, TActionKey> where TActionKey : IActionKey
    {
        static NOOPAction<TAgent, TStateData, TActionKey>  s_Instance;
        public static NOOPAction<TAgent, TStateData, TActionKey> Instance => s_Instance ?? (s_Instance = new NOOPAction<TAgent, TStateData, TActionKey>());

        NOOPAction() { }

        public void BeginExecution(TStateData state, TActionKey action, TAgent actor) { }

        public void ContinueExecution(TStateData state, TActionKey action, TAgent actor) { }

        public void EndExecution(TStateData state, TActionKey action, TAgent actor) { }

        public OperationalActionStatus Status(TStateData state, TActionKey action, TAgent actor)
        {
            return OperationalActionStatus.Completed;
        }
    }
}
