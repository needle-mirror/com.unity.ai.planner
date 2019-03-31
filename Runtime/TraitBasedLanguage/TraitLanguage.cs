using System;
using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Component used to mark state entities
    /// </summary>
    public struct State : IComponentData { }

    /// <summary>
    /// A unique identifier assigned to each domain object within a state
    /// </summary>
    public struct DomainObjectID : IEquatable<DomainObjectID>
    {
        public int Value;

        /// <summary>
        /// The reserved DomainObjectID value specifying a reference to no domain object
        /// </summary>
        public static DomainObjectID None = new DomainObjectID { Value = 0 };

        static int s_DomainObjectIDs = 1; // 0 is the same as default (uninitialized)

        /// <summary>
        /// Provides a new DomainObjectID with an unassigned ID
        /// </summary>
        /// <returns>Returns a new DomainObjectID with an unassigned ID</returns>
        public static DomainObjectID GetNext()
        {
            return new DomainObjectID { Value = s_DomainObjectIDs++ };
        }

        /// <summary>
        /// Compares two given DomainObjectIDs
        /// </summary>
        /// <param name="x">A DomainObjectID</param>
        /// <param name="y">A DomainObjectID</param>
        /// <returns>Returns if two DomainObjectIDs are equal</returns>
        public static bool operator ==(DomainObjectID x, DomainObjectID y) => x.Value == y.Value;

        /// <summary>
        /// Compares two given DomainObjectIDs
        /// </summary>
        /// <param name="x">A DomainObjectID</param>
        /// <param name="y">A DomainObjectID</param>
        /// <returns>Returns if two DomainObjectIDs are not equal</returns>
        public static bool operator !=(DomainObjectID x, DomainObjectID y) => x.Value != y.Value;

        /// <summary>
        /// Compares the DomainObjectID to another DomainObjectID
        /// </summary>
        /// <param name="other">The DomainObjectID for comparison</param>
        /// <returns>Returns true if the DomainObjectIDs are equal</returns>
        public bool Equals(DomainObjectID other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object obj) => !(obj is null) && obj is DomainObjectID other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Value;

        /// <inheritdoc />
        public override string ToString()
        {
            return Equals(None) ? "None" : $"<< {Value} >>";
        }
    }

    /// <summary>
    /// The trait denoting that an entity represents a domain object
    /// </summary>
    public struct DomainObjectTrait : ITrait<DomainObjectTrait>, IEquatable<DomainObjectTrait>
    {
        /// <summary>
        /// A unique ID assigned to the domain object
        /// </summary>
        public DomainObjectID ID;

        /// <inheritdoc />
        public bool Equals(DomainObjectTrait other) => ID.Equals(other.ID);

        /// <inheritdoc />
        public override int GetHashCode() => ID.GetHashCode();

        /// <summary>
        /// Provides a new DomainObjectTrait with a unique DomainObjectID
        /// </summary>
        /// <returns>Returns a new DomainObjectTrait with an new unique DomainObjectID</returns>
        public static DomainObjectTrait GetNext()
        {
            return new DomainObjectTrait { ID = DomainObjectID.GetNext() };
        }

        /// <inheritdoc />
        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(ID):
                    ID = (DomainObjectID)value;
                    break;
            }
        }

        /// <inheritdoc />
        public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)
        {
            entityManager.SetComponentData(domainObjectEntity, this);
        }

        /// <inheritdoc />
        public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity)
        {
            // DomainObjectTrait is assumed to be on each domain object, so not necessary to reserve a bit for it
        }
    }

    /// <summary>
    /// A container used as a reference to another entity, for use in buffers
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct DomainObjectReference : IBufferElementData
    {
        /// <summary>
        /// The entity to which this refers
        /// </summary>
        public Entity DomainObjectEntity;
    }
}
