using System;
using Unity.AI.Planner.Jobs;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A specialized interface of <see cref="IActionScheduler{TStateKey,TStateData,TStateDataContext,TStateManager,TActionKey,TActionResult}"/>
    /// for trait-based domains
    /// </summary>
    /// <typeparam name="TObject">Object type</typeparam>
    /// <typeparam name="TStateKey">StateKey type</typeparam>
    /// <typeparam name="TStateData">StateData type</typeparam>
    /// <typeparam name="TStateDataContext">StateDataContext type</typeparam>
    /// <typeparam name="TStateManager">StateManager type</typeparam>
    /// <typeparam name="TActionKey">ActionKey type</typeparam>
    /// <typeparam name="TActionResult">ActionResult type</typeparam>
    public interface ITraitBasedActionScheduler<TObject, TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey, TActionResult> :
        IActionScheduler<TStateKey, TStateData, TStateDataContext, TStateManager, TActionKey, TActionResult>
        where TStateKey : struct, IEquatable<TStateKey>, IStateKey
        where TStateData : struct, ITraitBasedStateData<TObject>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
        where TStateManager : ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext>
        where TActionKey : struct, IEquatable<TActionKey>, IActionKeyWithGuid
        where TActionResult : struct
        where TObject : struct, IDomainObject
    {
    }
}
