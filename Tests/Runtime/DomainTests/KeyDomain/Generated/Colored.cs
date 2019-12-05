using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace KeyDomain
{
    [Serializable]
    internal struct Colored : ITrait, IEquatable<Colored>
    {
        public ColorValue Color;

        public object GetField(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(Color):
                    return Color;
            }

            return null;
        }

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Color):
                    Color = (ColorValue)Enum.ToObject(typeof(ColorValue), value);
                    break;
            }
        }

        public bool AttributesEqual(Colored other)
        {
            return Color == other.Color;
        }

        public bool Equals(Colored other)
        {
            return Color == other.Color;
        }

        public override int GetHashCode()
        {
            return 397 ^ Color.GetHashCode();
        }

        public override string ToString()
        {
            return $"Colored: {Color}";
        }
    }
}
