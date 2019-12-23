using System;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A specialized interface of <see cref="IActionKey"/> for trait-based domains that provides GUIDs for lookup
    /// </summary>
    interface IActionKeyWithGuid : IActionKey
    {
        /// <summary>
        /// A GUID (Globally Unique Identifier) for an action type
        /// </summary>
        Guid ActionGuid { get; }
    }
}
