using System;
using Unity.Semantic.Traits;
using UnityEngine;

namespace Generated.Semantic.Traits
{
    /// <summary>
    /// The component data representation of the Location trait.
    /// </summary>
    [Serializable]
    public struct LocationData : ICustomTraitData, IEquatable<LocationData>
    {
        /// <summary>
        /// The instance ID of the transform for the semantic object.
        /// </summary>
        [HideInInspector]
        public int TransformInstanceId;

        /// <summary>
        /// The position of the object.
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// The forward vector of the object.
        /// </summary>
        public Vector3 Forward;

        /// <summary>
        /// The transform of the object. Setting this property sets the TransformInstanceId, Position, and Forward properties.
        /// </summary>
        public Transform Transform
        {
            set
            {
                TransformInstanceId = value ? value.GetInstanceID() : -1;
                Position = value ? value.position : Vector3.zero;
                Forward = value ? value.forward : Vector3.zero;
            }
        }

        /// <summary>
        /// Indicates whether another LocationData is equal to this one.
        /// </summary>
        /// <param name="other">The other LocationData to compare to.</param>
        /// <returns>Returns true if the two LocationData are equal.</returns>
        public bool Equals(LocationData other)
        {
            return TransformInstanceId == other.TransformInstanceId && Position == other.Position && Forward == other.Forward;
        }

        /// <summary>
        /// Returns the string representation of the trait.
        /// </summary>
        /// <returns>Returns the string representation of the trait.</returns>
        public override string ToString()
        {
            return $"Location: {TransformInstanceId} {Position} {Forward}";
        }
    }
}
