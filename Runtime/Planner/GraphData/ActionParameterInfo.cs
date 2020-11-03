using System;
using Unity.AI.Planner.Traits;

namespace Unity.AI.Planner
{
    /// <summary>
    /// Data representing information for an action parameter in a plan.
    /// </summary>
    public struct ActionParameterInfo
    {
        /// <summary>
        /// The name of the parameter.
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// The name of the TraitObject used as an argument for this parameter.
        /// </summary>
        public string TraitObjectName { get; set; }

        /// <summary>
        /// The id of the TraitObject used as an argument for this parameter.
        /// </summary>
        public ObjectId TraitObjectId { get; set; }
    }
}
