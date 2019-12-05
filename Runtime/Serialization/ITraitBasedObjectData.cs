using System;
using System.Collections.Generic;

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
        /// List of initialization data for traits
        /// </summary>
        IEnumerable<ITraitData> TraitData { get; }

        /// <summary>
        /// Get initialization data for a given trait
        /// </summary>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Initialization data</returns>
        ITraitData GetTraitData<TTrait>() where TTrait : ITrait;
    }
}
