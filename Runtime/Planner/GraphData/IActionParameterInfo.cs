using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface for data representing information for an action parameter in a plan.
    /// </summary>
    interface IActionParameterInfo
    {
        /// <summary>
        /// The name of the parameter.
        /// </summary>
        string ParameterName { get; }

        /// <summary>
        /// The name of the TraitObject used as an argument for this parameter.
        /// </summary>
        string TraitObjectName { get; }

        /// <summary>
        /// The id of the TraitObject used as an argument for this parameter.
        /// </summary>
        ObjectId TraitObjectId { get; }
    }
}
