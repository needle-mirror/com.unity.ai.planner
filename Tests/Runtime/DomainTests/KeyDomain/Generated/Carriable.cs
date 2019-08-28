using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace KeyDomain
{
    [Serializable]
    internal struct Carriable : ITrait, IEquatable<Carriable>
    {
        public const bool IsZeroSized = false;

        public Unity.AI.Planner.DomainLanguage.TraitBased.ObjectID Carrier;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Carrier):
                    Carrier = (Unity.AI.Planner.DomainLanguage.TraitBased.ObjectID)value;
                    break;
            }
        }

        public bool Equals(Carriable other)
        {
            return Carrier == other.Carrier;
        }

        public override int GetHashCode()
        {
            return 397
                   ^ Carrier.GetHashCode();
        }

        public override string ToString()
        {
            return $"Carriable: {Carrier}";
        }
    }
}
