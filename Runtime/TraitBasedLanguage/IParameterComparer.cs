using System;
using System.Collections.Generic;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// An interface that marks an implementation of a comparer to sort actions parameter
    /// </summary>
    /// <typeparam name="TStateData"></typeparam>
    public interface IParameterComparer<TStateData> : IComparer<int>
        where TStateData : struct, IStateData
    {
        /// <summary>
        /// State data for the state in which an action will be taken.
        /// </summary>
        TStateData StateData { get; set; }
    }

    /// <summary>
    /// An interface that marks an implementation of a comparer to sort actions parameter depending on a referenced trait value
    /// </summary>
    /// <typeparam name="TStateData"></typeparam>
    /// <typeparam name="TTraitType"></typeparam>
    public interface IParameterComparerWithReference<TStateData, TTraitType> : IParameterComparer<TStateData>
        where TStateData : struct, IStateData
        where TTraitType : ITrait
    {
        /// <summary>
        /// The reference trait used to make comparisons.
        /// </summary>
        TTraitType ReferenceTrait { get; set; }
    }
}
