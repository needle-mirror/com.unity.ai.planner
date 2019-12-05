using System;
using Unity.Entities;
#if !UNITY_DOTSPLAYER
using UnityEngine;
#endif

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A custom trait for marking objects that can move around, since it is commonly used in domains
    /// </summary>
    [Serializable]
    public struct Moveable : ICustomTrait, IEquatable<Moveable>
    {
        /// <summary>
        /// Set the value of a field
        /// </summary>
        /// <param name="fieldName">Name of field</param>
        /// <param name="value">Value</param>
        public void SetField(string fieldName, object value)
        {
        }

        /// <summary>
        /// Get the value of a field
        /// </summary>
        /// <param name="fieldName">Name of field</param>
        /// <returns>Field value if field exists</returns>
        public object GetField(string fieldName)
        {
            return null;
        }

        /// <summary>
        /// Returns whether the attributes of the trait are equal
        /// </summary>
        /// <param name="other">Another trait to which this trait is compared</param>
        /// <returns>True if the attributes of the two traits are equal</returns>
        public bool AttributesEqual(Moveable other)
        {
            return true;
        }

        /// <summary>
        /// Compares this Moveable trait to another
        /// </summary>
        /// <param name="other">Another trait to which this trait is compared</param>
        /// <returns>True if the two traits are equal</returns>
        public bool Equals(Moveable other)
        {
            return true;
        }

        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return ComponentType.ReadOnly<Moveable>().TypeIndex;
        }

        /// <summary>
        /// Returns a string that represents the trait
        /// </summary>
        /// <returns>A string that represents the trait</returns>
        public override string ToString()
        {
            return nameof(Moveable);
        }
    }
}
