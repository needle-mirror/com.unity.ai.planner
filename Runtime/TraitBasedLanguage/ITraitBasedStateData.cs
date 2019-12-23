using Unity.Collections;
using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A specialized interface of <see cref="IStateData"/> for trait-based domains
    /// </summary>
    /// <typeparam name="TObject">Object type</typeparam>
    interface ITraitBasedStateData<TObject> : IStateData
        where TObject : ITraitBasedObject
    {
        /// <summary>
        /// Add a trait-based object to a state
        /// </summary>
        /// <param name="types">Trait types to initialize the trait-based object with</param>
        /// <param name="traitBasedObject">Created trait-based object</param>
        /// <param name="objectId">Trait-based object Id</param>
        /// <param name="name">Name of the trait-based object</param>
        void AddObject(NativeArray<ComponentType> types, out TObject traitBasedObject, TraitBasedObjectId objectId, string name = null);

        /// <summary>
        /// Add a trait-based object to a state
        /// </summary>
        /// <param name="types">Trait types to initialize the trait-based object with</param>
        /// <param name="traitBasedObject">Created trait-based object</param>
        /// <param name="objectId">Created Trait-based object Id</param>
        /// <param name="name">Name of the trait-based object</param>
        void AddObject(NativeArray<ComponentType> types, out TObject traitBasedObject, out TraitBasedObjectId objectId, string name = null);

        /// <summary>
        /// Set/update trait data on a domain object
        /// </summary>
        /// <param name="trait">Trait data (causes boxing)</param>
        /// <param name="traitBasedObject">Domain object</param>
        void SetTraitOnObject(ITrait trait, ref TObject traitBasedObject);

        /// <summary>
        /// Get trait data for a trait-based object
        /// </summary>
        /// <param name="traitBasedObject">Trait-based object</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Specified trait data</returns>
        TTrait GetTraitOnObject<TTrait>(TObject traitBasedObject)
            where TTrait : struct, ITrait;

        /// <summary>
        /// Set/update trait data on a trait-based object
        /// </summary>
        /// <param name="trait">Trait data</param>
        /// <param name="traitBasedObject">Trait-based object</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        void SetTraitOnObject<TTrait>(TTrait trait, ref TObject traitBasedObject)
            where TTrait : struct, ITrait;

        /// <summary>
        /// Remove a trait from a trait-based object
        /// </summary>
        /// <param name="traitBasedObject">Trait-based object</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Whether the trait was removed</returns>
        bool RemoveTraitOnObject<TTrait>(ref TObject traitBasedObject)
            where TTrait : struct, ITrait;

        /// <summary>
        /// Remove a trait-based object from a state
        /// </summary>
        /// <param name="traitBasedObject">Trait-based object to remove</param>
        /// <returns>Whether the trait-based object was removed or not</returns>
        bool RemoveObject(TObject traitBasedObject);
    }

    interface ITraitBasedStateData<TObject, TStateData> : ITraitBasedStateData<TObject>
        where TObject : ITraitBasedObject
        where TStateData : ITraitBasedStateData<TObject, TStateData>
    {
        bool TryGetObjectMapping(TStateData rhsState, ObjectCorrespondence objectMap);
    }
}
