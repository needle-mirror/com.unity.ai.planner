using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace KeyDomain
{
    [Serializable]
    internal struct Lockable : ITrait, IEquatable<Lockable>
    {
        public const bool IsZeroSized = false;

        public System.Boolean Locked;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Locked):
                    Locked = (System.Boolean)value;
                    break;
            }
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
