using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace KeyDomain
{
    [Serializable]
    internal struct Colored : ITrait, IEquatable<Colored>
    {
        public const bool IsZeroSized = false;

        public ColorValue Color;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Color):
                    Color = (ColorValue)Enum.ToObject(typeof(ColorValue), value);
                    break;
            }
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
