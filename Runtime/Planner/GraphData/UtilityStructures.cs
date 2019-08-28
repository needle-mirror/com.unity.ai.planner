using System;
using Unity.Entities;

namespace Unity.AI.Planner
{
    struct HashCode : IComponentData, IEquatable<HashCode>
    {
        public int Value;

        public override bool Equals(object o) => (o is HashCode other) && Equals(other);
        public bool Equals(HashCode other) => Value == other.Value;
        public static bool operator ==(HashCode x, HashCode y) => x.Value == y.Value;
        public static bool operator !=(HashCode x, HashCode y) => x.Value != y.Value;

        public override int GetHashCode() => Value;
    }
}
