using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace KeyDomain
{
    [Serializable]
    internal struct Localized : ITrait, IEquatable<Localized>
    {
        public ObjectId Location;

        public object GetField(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(Location):
                    return Location;
            }

            return null;
        }

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Location):
                    Location = (ObjectId)value;
                    break;
            }
        }

        public bool AttributesEqual(Localized other)
        {
            return true;
        }

        public bool Equals(Localized other)
        {
            return Location == other.Location;
        }

        public override int GetHashCode()
        {
            return 397
                ^ Location.GetHashCode();
        }

        public override string ToString()
        {
            return $"Localized: {Location}";
        }
    }
}
