using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Entities;

namespace KeyDomain
{
    [Serializable]
    internal struct End : ITrait, IEquatable<End>
    {
        public object GetField(string fieldName)
        {
            return null;
        }

        public void SetField(string fieldName, object value)
        {
        }

        public bool AttributesEqual(End other)
        {
            return true;
        }

        public bool Equals(End other)
        {
            return true;
        }

        public override int GetHashCode()
        {
            return ComponentType.ReadOnly<End>().TypeIndex;
        }

        public override string ToString()
        {
            return $"End";
        }
    }
}
