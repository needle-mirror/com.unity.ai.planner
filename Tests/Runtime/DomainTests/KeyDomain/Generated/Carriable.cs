using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace KeyDomain
{
    [Serializable]
    internal struct Carriable : ITrait, IEquatable<Carriable>
    {
        public ObjectId Carrier;

        public object GetField(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(Carrier):
                    return Carrier;;
            }

            return null;
        }

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Carrier):
                    Carrier = (ObjectId)value;
                    break;
            }
        }

        public bool AttributesEqual(Carriable other)
        {
            return true;
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
