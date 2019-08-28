using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// An interface that marks an implementation of a domain object type for DOTS, trait-based domains
    /// </summary>
    public interface IDomainObject : IBufferElementData
    {
        /// <summary>
        /// Evaluate whether this domain object has the specified trait types
        /// </summary>
        /// <param name="traitTypes">Types of traits to match</param>
        /// <returns>Whether or not the domain object has the trait types</returns>
        bool MatchesTraitFilter(ComponentType[] traitTypes);
    }
}
