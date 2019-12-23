using Unity.Collections;
using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// An interface that marks an implementation of a trait-based object type for DOTS, trait-based domains
    /// </summary>
    interface ITraitBasedObject : IBufferElementData
    {
        /// <summary>
        /// Evaluate whether this trait-based object has the specified trait types
        /// </summary>
        /// <param name="traitTypes">Types of traits to match</param>
        /// <returns>Whether or not the trait-based object has the trait types</returns>
        bool MatchesTraitFilter(NativeArray<ComponentType> traitTypes);
    }
}
