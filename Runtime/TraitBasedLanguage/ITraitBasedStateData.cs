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
        void AddObject(NativeArray<ComponentType> types, out TObject traitBasedObject, TraitBasedObjectId objectId,  NativeString64 name = default);

        /// <summary>
        /// Add a trait-based object to a state
        /// </summary>
        /// <param name="types">Trait types to initialize the trait-based object with</param>
        /// <param name="traitBasedObject">Created trait-based object</param>
        /// <param name="objectId">Created Trait-based object Id</param>
        /// <param name="name">Name of the trait-based object</param>
        void AddObject(NativeArray<ComponentType> types, out TObject traitBasedObject, out TraitBasedObjectId objectId,  NativeString64 name = default);

        /// <summary>
        /// Set/update trait data on a trait-based object
        /// </summary>
        /// <param name="trait">Trait data (causes boxing)</param>
        /// <param name="traitBasedObject">Trait-based object</param>
        void SetTraitOnObject(ITrait trait, ref TObject traitBasedObject);

        /// <summary>
        /// Get object index for a trait-based object
        /// </summary>
        /// <param name="traitBasedObject">Trait-based object</param>
        /// <returns>Trait based object index</returns>
        int GetTraitBasedObjectIndex(TObject traitBasedObject);

        /// <summary>
        /// Get object index for a trait-based object Id
        /// </summary>
        /// <param name="traitBasedObjectId">Trait-based object Id</param>
        /// <returns>Trait based object index</returns>
        int GetTraitBasedObjectIndex(TraitBasedObjectId traitBasedObjectId);

        /// <summary>
        /// Set/update trait data on a trait-based object given its index
        /// </summary>
        /// <param name="trait">Trait data (causes boxing)</param>
        /// <param name="traitBasedObjectIndex">Trait based object index</param>
        void SetTraitOnObjectAtIndex(ITrait trait, int traitBasedObjectIndex);

        /// <summary>
        /// Set/update trait data on a trait-based object given its index
        /// </summary>
        /// <param name="trait">Trait data</param>
        /// <param name="traitBasedObjectIndex">Trait based object index</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        void SetTraitOnObjectAtIndex<TTrait>(TTrait trait, int traitBasedObjectIndex)
            where TTrait : struct, ITrait;

        /// <summary>
        /// Remove a trait from a trait-based object given its index
        /// </summary>
        /// <param name="traitBasedObjectIndex">Trait based object index</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Whether the trait was removed</returns>
        bool RemoveTraitOnObjectAtIndex<TTrait>(int traitBasedObjectIndex)
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
        /// Get trait data for a trait-based object
        /// </summary>
        /// <param name="traitBasedObject">Trait-based object</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Specified trait data</returns>
        TTrait GetTraitOnObject<TTrait>(TObject traitBasedObject)
            where TTrait : struct, ITrait;

        /// <summary>
        /// Check if a trait is set on a trait-based object
        /// </summary>
        /// <param name="traitBasedObject">Trait-based object</param>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Whether the trait is set on the object</returns>
        bool HasTraitOnObject<TTrait>(TObject traitBasedObject)
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
        /// Remove a trait-based object from a state
        /// </summary>
        /// <param name="traitBasedObject">Trait-based object to remove</param>
        /// <returns>Whether the trait-based object was removed or not</returns>
        bool RemoveObject(TObject traitBasedObject);

        /// <summary>
        /// Remove a trait-based object from a state
        /// </summary>
        /// <param name="traitBasedObjectIndex">Trait based object index</param>
        /// <returns>Whether the trait-based object was removed or not</returns>
        bool RemoveTraitBasedObjectAtIndex(int traitBasedObjectIndex);
    }

    interface ITraitBasedStateData<TObject, TStateData> : ITraitBasedStateData<TObject>
        where TObject : ITraitBasedObject
        where TStateData : ITraitBasedStateData<TObject, TStateData>
    {
        bool TryGetObjectMapping(TStateData rhsState, ObjectCorrespondence objectMap);
    }
}
