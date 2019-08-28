using System.Collections.Generic;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Use this interface to provide a list of domain objects for use in initializing planner states
    /// </summary>
    public interface IDomainObjectProvider
    {
        /// <summary>
        /// List of domain objects contained by the provider
        /// </summary>
        IEnumerable<IDomainObjectData> DomainObjects { get; }
    }
}
