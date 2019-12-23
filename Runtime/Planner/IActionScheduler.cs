using System;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.AI.Planner.Jobs
{
    /// <summary>
    /// An interface that marks an implementation of an action scheduler for a specific domain/planning definition that
    /// will schedule actions and report newly created states
    /// </summary>
    /// <typeparam name="TStateKey">StateKey type</typeparam>
    /// <typeparam name="TStateData">StateData type</typeparam>
    /// <typeparam name="TStateDataContext">StateDataContext type</typeparam>
    /// <typeparam name="TStateManager">StateManager type</typeparam>
    /// <typeparam name="TActionKey">ActionKey type</typeparam>
    interface IActionScheduler<TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey>
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
        where TStateManager : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TActionKey : struct, IEquatable<TActionKey>
    {
        /// <summary>
        /// Input to action scheduler: List of states to expand via scheduled actions
        /// </summary>
        NativeList<TStateKey> UnexpandedStates { set; }
        /// <summary>
        /// Input to action scheduler: Instance of the state manager
        /// </summary>
        TStateManager StateManager { set; }

        /// <summary>
        /// Output from action scheduler: List of newly created states w/ info
        /// </summary>
        NativeQueue<StateTransitionInfoPair<TStateKey, TActionKey, StateTransitionInfo>> CreatedStateInfo { set; }

        /// <summary>
        /// Schedule job actions for delayed execution
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns></returns>
        JobHandle Schedule(JobHandle inputDeps);
    }
}
