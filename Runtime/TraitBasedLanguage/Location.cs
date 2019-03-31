using System;
using Unity.Entities;
using UnityEngine;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A custom trait for locations, since it is commonly used in domains
    /// </summary>
    [Serializable]
    public struct Location : ICustomTrait<Location>, IEquatable<Location>
    {
        const uint TraitMask = 1U << 31;

        /// <summary>
        /// The ID of the transform of the location
        /// </summary>
        public int TransformInstanceID;

        /// <summary>
        /// The position of the location
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// The forward vector of the location
        /// </summary>
        public Vector3 Forward;

        /// <summary>
        /// The transform of the location
        /// </summary>
        public Transform Transform
        {
            get => null;
            set
            {
                    TransformInstanceID = value.GetInstanceID();
                    Position = value.position;
                    Forward = value.forward;
            }
        }

        /// <summary>
        /// Compares the location to another
        /// </summary>
        /// <param name="other">Another location to which the location is compared</param>
        /// <returns>Returns true if the two locations are equal</returns>
        public bool Equals(Location other)
        {
            return TransformInstanceID.Equals(other.TransformInstanceID)
                && Position == other.Position
                && Forward == other.Forward;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return 397 ^ TransformInstanceID.GetHashCode();
        }

        /// <inheritdoc />
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

                case nameof(TransformInstanceID):
                    TransformInstanceID = (int)value;
                    break;

                case nameof(Transform):
                    Transform = (Transform)value;
                    break;
            }
        }

        /// <inheritdoc />
        public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)
        {
            SetTraitMask(entityManager, domainObjectEntity);
            entityManager.SetComponentData(domainObjectEntity, this);
        }

        /// <inheritdoc />
        public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity)
        {
            var objectHash = entityManager.GetComponentData<HashCode>(domainObjectEntity);
            objectHash.TraitMask = objectHash.TraitMask | TraitMask;
            entityManager.SetComponentData(domainObjectEntity, objectHash);
        }
    }
}
