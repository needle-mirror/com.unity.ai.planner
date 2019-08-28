using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace KeyDomain
{
    [Serializable]
    internal struct Localized : ITrait, IEquatable<Localized>
    {
        public const bool IsZeroSized = false;

        public Unity.AI.Planner.DomainLanguage.TraitBased.ObjectID Location;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Location):
                    Location = (Unity.AI.Planner.DomainLanguage.TraitBased.ObjectID)value;
                    break;
            }
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
