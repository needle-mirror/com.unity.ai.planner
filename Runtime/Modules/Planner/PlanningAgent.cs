using System;
using Unity.Entities;
using UnityEngine;

namespace Unity.AI.Planner.Traits
{
    /// <summary>
    /// A custom trait for marking the planning agent (automatically set by planner)
    /// </summary>
    public struct PlanningAgent : ICustomTrait, IBufferElementData, IEquatable<PlanningAgent>
    {
        /// <summary>
        /// GetField is deprecated. Cast the trait to the appropriate type, then access the field.
        /// </summary>
        /// <param name="fieldName">The field name to be read.</param>
        /// <returns>The value of the field.</returns>
        /// <exception cref="ArgumentException">Always throws an exception. PlanningAgent contains no fields.</exception>
        [Obsolete("GetField is deprecated. Cast the trait to the appropriate type, then access the field.")]
        public object GetField(string fieldName)
        {
            throw new ArgumentException("No fields exist on the trait PlanningAgent.");
        }

        /// <summary>
        /// SetField is deprecated. Cast the trait to the appropriate type, then access the field.
        /// </summary>
        /// <param name="fieldName">The name of the field to be set.</param>
        /// <param name="value">The value for the field.</param>
        [Obsolete("SetField is deprecated. Cast the trait to the appropriate type, then access the field.")]
        public void SetField(string fieldName, object value)
        {
        }

        /// <summary>
        /// Indicates the equality of another PlanningAgent and this instance.
        /// </summary>
        /// <param name="other">The other PlanningAgent instance for comparison.</param>
        /// <returns>Always returns true.</returns>
        public bool Equals(PlanningAgent other)
        {
            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "PlanningAgent";
        }
    }
}
