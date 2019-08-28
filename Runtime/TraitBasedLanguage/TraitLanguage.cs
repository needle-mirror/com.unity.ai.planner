using System;
using System.Threading;
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
    public struct ObjectID : IEquatable<ObjectID>
    {
        /// <summary>
        /// ID Value
        /// </summary>
        public int Value;

        /// <summary>
        /// The reserved ObjectID value specifying a reference to no domain object
        /// </summary>
        public static ObjectID None = new ObjectID { Value = 0 };

        static int s_ObjectIDs = 1; // 0 is the same as default (uninitialized)

        /// <summary>
        /// Provides a new domain object with an unassigned ID
        /// </summary>
        /// <returns>Returns a new, unassigned ID</returns>
        public static ObjectID GetNext()
        {
            Interlocked.Increment(ref s_ObjectIDs);
            return new ObjectID { Value = s_ObjectIDs };
        }

        /// <summary>
        /// Compares two given ObjectIDs
        /// </summary>
        /// <param name="x">An ObjectID</param>
        /// <param name="y">An ObjectID</param>
        /// <returns>Returns if two DomainObjectIDs are equal</returns>
        public static bool operator ==(ObjectID x, ObjectID y) => x.Value == y.Value;

        /// <summary>
        /// Compares two given ObjectIDs
        /// </summary>
        /// <param name="x">An ObjectID</param>
        /// <param name="y">An ObjectID</param>
        /// <returns>Returns if two DomainObjectIDs are not equal</returns>
        public static bool operator !=(ObjectID x, ObjectID y) => x.Value != y.Value;

        /// <summary>
        /// Compares an ObjectID to another ObjectID
        /// </summary>
        /// <param name="other">ObjectID for comparison</param>
        /// <returns>Returns true if the ObjectIDs are equal</returns>
        public bool Equals(ObjectID other) => Value == other.Value;


        /// <summary>
        /// Test for equality
        /// </summary>
        /// <param name="obj">Other ObjectID</param>
        /// <returns>Result of equality test</returns>
        public override bool Equals(object obj) => !(obj is null) && obj is ObjectID other && Equals(other);

        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode() => Value;

        /// <summary>
        /// Returns a string that represents the ObjectID
        /// </summary>
        /// <returns>A string that represents the ObjectID</returns>
        public override string ToString()
        {
            return Equals(None) ? "None" : $"<< {Value} >>";
        }
    }

    /// <summary>
    /// The trait denoting that an entity represents a domain object
    /// </summary>
    public struct DomainObjectID : ITrait, IEquatable<DomainObjectID>
    {
        /// <summary>
        /// Test for equality
        /// </summary>
        /// <param name="obj">Other DomainObjectID</param>
        /// <returns>Result of equality test</returns>
        public override bool Equals(object obj)
        {
            return obj is DomainObjectID other && Equals(other);
        }

        /// <summary>
        /// A unique ObjectID assigned to the domain object
        /// </summary>
        public ObjectID ID;

#if DEBUG
        public NativeString64 Name;
#endif

        /// <summary>
        /// Test for equality
        /// </summary>
        /// <param name="other">Other DomainObjectID</param>
        /// <returns>Result of equality test</returns>
        public bool Equals(DomainObjectID other) => ID.Equals(other.ID);

        /// <summary>
        /// Compares two given DomainObjectIDs
        /// </summary>
        /// <param name="x">A DomainObjectID</param>
        /// <param name="y">A DomainObjectID</param>
        /// <returns>Returns if two DomainObjectIDs are equal</returns>
        public static bool operator ==(DomainObjectID x, DomainObjectID y)
        {
            return x.Equals(y);
        }

        /// <summary>
        /// Compares two given DomainObjectIDs
        /// </summary>
        /// <param name="x">A DomainObjectID</param>
        /// <param name="y">A DomainObjectID</param>
        /// <returns>Returns if two DomainObjectIDs are not equal</returns>
        public static bool operator !=(DomainObjectID x, DomainObjectID y)
        {
            return !x.Equals(y);
        }

        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode() => ID.GetHashCode();

        /// <summary>
        /// Provides a new DomainObjectTrait with a unique DomainObjectID
        /// </summary>
        /// <returns>Returns a new DomainObjectTrait with an new unique DomainObjectID</returns>
        public static DomainObjectID GetNext()
        {
            return new DomainObjectID { ID = ObjectID.GetNext() };
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
                case nameof(ID):
                    ID = (ObjectID)value;
                    break;
            }
        }

        /// <summary>
        /// Returns a string that represents the DomainObjectID
        /// </summary>
        /// <returns>A string that represents the DomainObjectID</returns>
        public override string ToString()
        {
#if DEBUG
            return $"Domain Object: {Name} ({ID})";
#else
            return $"Domain Object: {ID}";
#endif
        }
    }
}
