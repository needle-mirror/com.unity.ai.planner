using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace KeyDomain
{
    [Serializable]
    internal struct Lockable : ITrait, IEquatable<Lockable>
    {
        public bool Locked;

        public object GetField(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(Locked):
                    return Locked;
            }

            return null;
        }

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Locked):
                    Locked = (bool)value;
                    break;
            }
        }

        public bool AttributesEqual(Lockable other)
        {
            return Locked == other.Locked;
        }

        public bool Equals(Lockable other)
        {
            return Locked == other.Locked;
        }

        public override int GetHashCode()
        {
            return 397
                ^ Locked.GetHashCode();
        }

        public override string ToString()
        {
            return $"Lockable: {Locked}";
        }
    }
}
