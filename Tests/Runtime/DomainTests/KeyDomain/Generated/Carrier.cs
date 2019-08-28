using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;


namespace KeyDomain
{
    [Serializable]
    internal struct Carrier : ITrait, IEquatable<Carrier>
    {
        public const bool IsZeroSized = false;

        public Unity.AI.Planner.DomainLanguage.TraitBased.ObjectID CarriedObject;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(CarriedObject):
                    CarriedObject = (Unity.AI.Planner.DomainLanguage.TraitBased.ObjectID)value;
                    break;
            }
        }

        public bool Equals(Carrier other)
        {
            return CarriedObject == other.CarriedObject;
        }

        public override int GetHashCode()
        {
            return 397
                   ^ CarriedObject.GetHashCode();
        }

        public override string ToString()
        {
            return $"Carrier: {CarriedObject}";
        }
    }
}
