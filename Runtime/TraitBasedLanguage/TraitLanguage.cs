using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Component used to mark state entities
    /// </summary>
    struct State : IComponentData { }

    /// <summary>
    /// A unique identifier assigned to each trait-based object within a state
    /// </summary>
    public struct ObjectId : IEquatable<ObjectId>
    {
        /// <summary>
        /// Id Value
        /// </summary>
        public int Value;

        /// <summary>
        /// The reserved ObjectId value specifying a reference to no trait-based object
        /// </summary>
        public static readonly ObjectId None = new ObjectId { Value = 0 };

        static readonly SharedStatic<int> k_ObjectIds = SharedStatic<int>.GetOrCreate<ObjectId>();

        /// <summary>
        /// Provides a new trait-based object with an unassigned Id
        /// </summary>
        /// <returns>Returns a new, unassigned Id</returns>
        public static ObjectId GetNext()
        {
            Interlocked.Increment(ref k_ObjectIds.Data);
            return new ObjectId { Value = k_ObjectIds.Data };
        }

        /// <summary>
        /// Compares two given ObjectIds
        /// </summary>
        /// <param name="x">An ObjectId</param>
        /// <param name="y">An ObjectId</param>
        /// <returns>Returns if two TraitBasedObjectIds are equal</returns>
        public static bool operator ==(ObjectId x, ObjectId y) => x.Value == y.Value;

        /// <summary>
        /// Compares two given ObjectIds
        /// </summary>
        /// <param name="x">An ObjectId</param>
        /// <param name="y">An ObjectId</param>
        /// <returns>Returns if two TraitBasedObjectIds are not equal</returns>
        public static bool operator !=(ObjectId x, ObjectId y) => x.Value != y.Value;

        /// <summary>
        /// Compares an ObjectId to another ObjectId
        /// </summary>
        /// <param name="other">ObjectId for comparison</param>
        /// <returns>Returns true if the ObjectIds are equal</returns>
        public bool Equals(ObjectId other) => Value == other.Value;


        /// <summary>
        /// Test for equality
        /// </summary>
        /// <param name="obj">Other ObjectId</param>
        /// <returns>Result of equality test</returns>
        public override bool Equals(object obj) => !(obj is null) && obj is ObjectId other && Equals(other);

        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode() => Value;

        /// <summary>
        /// Returns a string that represents the ObjectId
        /// </summary>
        /// <returns>A string that represents the ObjectId</returns>
        public override string ToString()
        {
            return Equals(None) ? "None" : $"{Value}";
        }
    }

    /// <summary>
    /// The trait denoting that an entity represents a trait-based object
    /// </summary>
    public struct TraitBasedObjectId : ITrait, IEquatable<TraitBasedObjectId>
    {
        /// <summary>
        /// Default TraitBasedObjectId representing no Object
        /// </summary>
        public static readonly TraitBasedObjectId None = new TraitBasedObjectId { Id = ObjectId.None };

        /// <summary>
        /// Test for equality
        /// </summary>
        /// <param name="obj">Other TraitBasedObjectId</param>
        /// <returns>Result of equality test</returns>
        public override bool Equals(object obj)
        {
            return obj is TraitBasedObjectId other && Equals(other);
        }

        /// <summary>
        /// A unique ObjectId assigned to the trait-based object
        /// </summary>
        public ObjectId Id;

#if DEBUG
        public NativeString64 Name;
#endif

        /// <summary>
        /// Test for equality
        /// </summary>
        /// <param name="other">Other TraitBasedObjectId</param>
        /// <returns>Result of equality test</returns>
        public bool Equals(TraitBasedObjectId other) => Id.Equals(other.Id);

        /// <summary>
        /// Compares two given TraitBasedObjectIds
        /// </summary>
        /// <param name="x">A TraitBasedObjectId</param>
        /// <param name="y">A TraitBasedObjectId</param>
        /// <returns>Returns if two TraitBasedObjectIds are equal</returns>
        public static bool operator ==(TraitBasedObjectId x, TraitBasedObjectId y)
        {
            return x.Equals(y);
        }

        /// <summary>
        /// Compares two given TraitBasedObjectIds
        /// </summary>
        /// <param name="x">A TraitBasedObjectId</param>
        /// <param name="y">A TraitBasedObjectId</param>
        /// <returns>Returns if two TraitBasedObjectIds are not equal</returns>
        public static bool operator !=(TraitBasedObjectId x, TraitBasedObjectId y)
        {
            return !x.Equals(y);
        }

        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Provides a new TraitBasedObjectId with a unique ObjectId
        /// </summary>
        /// <returns>Returns a new TraitBasedObjectId with a new unique ObjectId</returns>
        public static TraitBasedObjectId GetNext()
        {
            return new TraitBasedObjectId { Id = ObjectId.GetNext() };
        }

        /// <summary>
        /// Get the value of a field
        /// </summary>
        /// <param name="fieldName">Name of field</param>
        /// <returns>Value</returns>
        public object GetField(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(Id):
                    return Id;
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
                case nameof(Id):
                    Id = (ObjectId)value;
                    break;
            }
        }

        /// <summary>
        /// Returns a string that represents the TraitBasedObjectId
        /// </summary>
        /// <returns>A string that represents the TraitBasedObjectId</returns>
        public override string ToString()
        {
#if DEBUG
            return $"{Name} ({Id})";
#else
            return $"Object ({Id})";
#endif
        }
    }
}
