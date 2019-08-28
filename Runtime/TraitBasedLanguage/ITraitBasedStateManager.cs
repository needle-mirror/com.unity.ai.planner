﻿using System;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A specialized interface of <see cref="IStateManager{TStateKey,TStateData,TStateDataContext}"/> for trait-based domains
    /// </summary>
    /// <typeparam name="TObject">Object type</typeparam>
    /// <typeparam name="TStateKey">StateKey type</typeparam>
    /// <typeparam name="TStateData">StateData type</typeparam>
    /// <typeparam name="TStateDataContext">StateDataContext type</typeparam>
    public interface ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext> : IStateManager<TStateKey, TStateData, TStateDataContext>
        where TObject : struct, IDomainObject
        where TStateKey : struct, IEquatable<TStateKey>, IStateKey
        where TStateData : struct, ITraitBasedStateData<TObject>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
    {
    }
}
