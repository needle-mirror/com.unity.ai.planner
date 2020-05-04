using System;
using System.Collections.Generic;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// An interface that marks an implementation of object data used to initialize a planner state;
    /// </summary>
    public interface ITraitBasedObjectData
    {
        /// <summary>
        /// Name of the TraitBasedObject
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Object that holds this instance
        /// </summary>
        object ParentObject { get; }

        /// <summary>
        /// List of data for traits
        /// </summary>
        IList<TraitData> TraitData { get; }

        /// <summary>
        /// Get data for a given trait
        /// </summary>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Initialization data</returns>
        ITraitData GetTraitData<TTrait>() where TTrait : ITrait;

        /// <summary>
        /// Remove data for a given trait
        /// </summary>
        /// <typeparam name="TTrait">Trait type</typeparam>
        void RemoveTraitData<TTrait>() where TTrait : ITrait;

        /// <summary>
        /// Checks whether the object has a specific type of trait
        /// </summary>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>True, if the object has the trait</returns>
        bool HasTraitData<TTrait>() where TTrait : ITrait;
    }
}
