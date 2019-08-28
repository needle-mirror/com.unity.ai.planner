using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A specialized interface of <see cref="IStateData"/> for trait-based domains
    /// </summary>
    /// <typeparam name="TObject">Object type</typeparam>
    public interface ITraitBasedStateData<TObject> : IStateData
        where TObject : struct, IDomainObject
    {
        /// <summary>
        /// Add a domain object to a state
        /// </summary>
        /// <param name="types">Trait types to initialize the domain object with</param>
        /// <param name="name">Name of the domain object</param>
        /// <returns>Domain object and ID</returns>
        (TObject, DomainObjectID) AddDomainObject(ComponentType[] types, string name);

        /// <summary>
        /// Set/update trait data on a domain object
        /// </summary>
        /// <param name="trait">Trait data (causes boxing)</param>
        /// <param name="domainObject">Domain object</param>
        void SetTraitOnObject(ITrait trait, ref TObject domainObject);

        /// <summary>
        /// Get trait data for a domain object
        /// </summary>
        /// <param name="domainObject">Domain object</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Specified trait data</returns>
        TTrait GetTraitOnObject<TTrait>(TObject domainObject)
            where TTrait : struct, ITrait;

        /// <summary>
        /// Set/update trait data on a domain object
        /// </summary>
        /// <param name="trait">Trait data</param>
        /// <param name="domainObject">Domain object</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        void SetTraitOnObject<TTrait>(TTrait trait, ref TObject domainObject)
            where TTrait : struct, ITrait;

        /// <summary>
        /// Remove a trait from a domain object
        /// </summary>
        /// <param name="domainObject">Domain object</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Whether the trait was removed</returns>
        bool RemoveTraitOnObject<TTrait>(ref TObject domainObject)
            where TTrait : struct, ITrait;

        /// <summary>
        /// Remove a domain object from a state
        /// </summary>
        /// <param name="domainObject">Domain object to remove</param>
        /// <returns>Whether the domain object was removed or not</returns>
        bool RemoveDomainObject(TObject domainObject);
    }
}
