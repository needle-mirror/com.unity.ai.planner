using System;
using System.Collections.Generic;

namespace Unity.AI.Planner
{
    /// <summary>
    /// A state manager.
    /// </summary>
    /// <typeparam name="TStateKey">StateKey type</typeparam>
    /// <typeparam name="TStateData">StateData type</typeparam>
    /// <typeparam name="TStateDataContext">StateDataContext type</typeparam>
    interface IStateManager<TStateKey, TStateData, TStateDataContext> : IEqualityComparer<TStateData>
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
    {
        /// <summary>
        /// Get a wrapper context to access states in a detached manner (e.g. within jobs); See <see cref="IStateDataContext{TStateKey,TStateData}"/>
        /// </summary>
        /// <returns></returns>
        TStateDataContext GetStateDataContext();

        /// <summary>
        /// Create a new state
        /// </summary>
        /// <returns>Newly created state</returns>
        TStateData CreateStateData();

        /// <summary>
        /// Destroy a state
        /// </summary>
        /// <param name="stateKey">Key to access the state that should be destroyed</param>
        void DestroyState(TStateKey stateKey);

        /// <summary>
        /// Get the state data for a given key
        /// </summary>
        /// <param name="stateKey">Key to access the state</param>
        /// <param name="readWrite">Whether the state needs write-back capabilities</param>
        /// <returns>State data for the given key</returns>
        TStateData GetStateData(TStateKey stateKey, bool readWrite);

        /// <summary>
        /// Get the key used to access the state data
        /// </summary>
        /// <param name="stateData">State data</param>
        /// <returns>Key that can be used to access the state</returns>
        TStateKey GetStateDataKey(TStateData stateData);

        /// <summary>
        /// Copy an existing state
        /// </summary>
        /// <param name="stateData">Existing state to copy</param>
        /// <returns>Copied state</returns>
        TStateData CopyStateData(TStateData stateData);

        /// <summary>
        /// Copy an existing state
        /// </summary>
        /// <param name="stateKey">Key to access the state to copy</param>
        /// <returns>Copied state</returns>
        TStateKey CopyState(TStateKey stateKey);
    }
}
