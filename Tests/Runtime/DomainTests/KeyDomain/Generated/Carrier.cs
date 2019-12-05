using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;


namespace KeyDomain
{
    [Serializable]
    internal struct Carrier : ITrait, IEquatable<Carrier>
    {
        public ObjectId CarriedObject;

        public object GetField(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(CarriedObject):
                    return CarriedObject;
            }

            return null;
        }

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(CarriedObject):
                    CarriedObject = (ObjectId)value;
                    break;
            }
        }

        public bool AttributesEqual(Carrier other)
        {
            return true;
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
