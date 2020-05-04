using System;
#if !UNITY_DOTSPLAYER
using UnityEngine;
using UnityEngine.Serialization;
#endif

#if UNITY_DOTSPLAYER
/// <summary>
/// Representation of a 3D vector with three floating-point values
/// </summary>
public struct Vector3 : IEquatable<Vector3>
{
    /// <summary>
    /// The X component of the vector
    /// </summary>
    public float x;

    /// <summary>
    /// The Y component of the vector
    /// </summary>
    public float y;

    /// <summary>
    /// The Z component of the vector
    /// </summary>
    public float z;

    public override bool Equals(object obj)
    {
        return obj is Vector3 other && Equals(other);
    }

    public bool Equals(Vector3 other)
    {
      return (double) this.x == (double) other.x && (double) this.y == (double) other.y && (double) this.z == (double) other.z;
    }

    public static bool operator ==(Vector3 lhs, Vector3 rhs)
    {
      float num1 = lhs.x - rhs.x;
      float num2 = lhs.y - rhs.y;
      float num3 = lhs.z - rhs.z;
      return (double) num1 * (double) num1 + (double) num2 * (double) num2 + (double) num3 * (double) num3 < 9.99999943962493E-11;
    }

    public static bool operator !=(Vector3 lhs, Vector3 rhs)
    {
      return !(lhs == rhs);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = x.GetHashCode();
            hashCode = (hashCode * 397) ^ y.GetHashCode();
            hashCode = (hashCode * 397) ^ z.GetHashCode();
            return hashCode;
        }
    }
}
#endif

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A custom trait for locations, since it is commonly used in domains
    /// </summary>
    [Serializable]
    public struct Location : ICustomTrait, IEquatable<Location>
    {
        /// <summary>
        /// The Id of the transform of the location
        /// </summary>
        [FormerlySerializedAs("TransformInstanceID")]
        public int TransformInstanceId;

        /// <summary>
        /// The position of the location
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// The forward vector of the location
        /// </summary>
        public Vector3 Forward;

#if !UNITY_DOTSPLAYER
        /// <summary>
        /// The transform of the location
        /// </summary>
        public Transform Transform
        {
            get => null;
            set
            {
                    TransformInstanceId = value.GetInstanceID();
                    Position = value.position;
                    Forward = value.forward;
            }
        }
#endif

        /// <summary>
        /// Compares this location to another
        /// </summary>
        /// <param name="other">Another location to which the location is compared</param>
        /// <returns>Returns true if the two locations are equal</returns>
        public bool Equals(Location other)
        {
            return TransformInstanceId.Equals(other.TransformInstanceId)
                && Position == other.Position
                && Forward == other.Forward;
        }

        /// <summary>
        /// Get the value of a field
        /// </summary>
        /// <param name="fieldName">Name of field</param>
        /// <returns>Field value if field exists</returns>
        public object GetField(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(Position):
                    return Position;

                case nameof(Forward):
                    return Forward;

                case nameof(TransformInstanceId):
                    return TransformInstanceId;
            }

            return null;
        }

        /// <summary>
        /// Set the value of a field
        /// </summary>
        /// <param name="fieldName">Name of field</param>
        /// <param name="value">Value</param>
        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Position):
                    Position = (Vector3)value;
                    break;

                case nameof(Forward):
                    Forward = (Vector3)value;
                    break;

                case nameof(TransformInstanceId):
                    TransformInstanceId = (int)value;
                    break;

#if !UNITY_DOTSPLAYER
                case nameof(Transform):
                    Transform = (Transform)value;
                    break;
#endif
            }
        }

        /// <summary>
        /// Returns a string that represents the location
        /// </summary>
        /// <returns>A string that represents the location</returns>
        public override string ToString()
        {
            return $"Location: {Position} {Forward} {TransformInstanceId}";
        }
    }
}
