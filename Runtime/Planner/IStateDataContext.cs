using System;
using System.Collections.Generic;

namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of a wrapper context for managing states within a domain. Not all
    /// domain language implementations may require a state data context, but for those that do (e.g. Entities-based)
    /// it provides a way to work with states in a detached manager from the StateManager (e.g. within jobs) and queue
    /// any changes up for playback later.
    /// </summary>
    /// <typeparam name="TStateKey">StateKey type</typeparam>
    /// <typeparam name="TStateData">StateData type</typeparam>
    interface IStateDataContext<TStateKey, TStateData> : IEqualityComparer<TStateData>
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct
    {
        /// <summary>
        /// Copy an existing state
        /// </summary>
        /// <param name="stateData">Existing state to copy</param>
        /// <returns>Copied state</returns>
        TStateData CopyStateData(TStateData stateData);

        /// <summary>
        /// Get the state data for a given key
        /// </summary>
        /// <param name="stateKey">Key to access the state</param>
        /// <returns>State data for the given key</returns>
        TStateData GetStateData(TStateKey stateKey);

        /// <summary>
        /// Destroy a state
        /// </summary>
        /// <param name="stateKey">Key to access the state that should be destroyed</param>
        void DestroyState(TStateKey stateKey);
    }
}
