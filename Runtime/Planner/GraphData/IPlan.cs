using System;

namespace Unity.AI.Planner
{
    interface IPlan : IDisposable
    {
        /// <summary>
        /// The number of states in the plan.
        /// </summary>
        int Size { get; }
    }

    interface IPlan<TStateKey, TActionKey> : IPlan
    {
        /// <summary>
        /// Returns the key for the optimal action for the given state.
        /// </summary>
        /// <param name="stateKey">Key for state to access</param>
        /// <param name="actionKey">Key to hold optimal action key</param>
        /// <returns>Whether an optimal action was found</returns>
        bool GetOptimalAction(TStateKey stateKey, out TActionKey actionKey);
    }
}
