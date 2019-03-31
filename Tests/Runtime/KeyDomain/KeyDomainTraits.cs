using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Entities;

namespace Unity.AI.Planner.Tests
{
    enum Color
    {
        Black,
        White
    }

    struct ColorTrait : IEquatable<ColorTrait>, ITrait
    {
        public Color Color;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Color):
                    Color = (Color)value;
                    break;
            }
        }


        public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)
        {
            entityManager.SetComponentData(domainObjectEntity, this);
        }

        public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity) { }

        public bool Equals(ColorTrait other)
        {
            return Color == other.Color;
        }

        public override int GetHashCode()
        {
            return Color.GetHashCode();
        }
    }

    struct CarrierTrait : IEquatable<CarrierTrait>, ITrait
    {
        public DomainObjectID CarriedObject;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(CarriedObject):
                    CarriedObject = (DomainObjectID)value;
                    break;
            }
        }

        public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)
        {
            entityManager.SetComponentData(domainObjectEntity, this);
        }

        public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity) { }

        public bool Equals(CarrierTrait other)
        {
            return CarriedObject.Equals(other.CarriedObject);
        }

        public override int GetHashCode()
        {
            return CarriedObject.GetHashCode();
        }
    }

    struct CarriableTrait : IEquatable<CarriableTrait>, ITrait
    {
        public DomainObjectID Carrier;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Carrier):
                    Carrier = (DomainObjectID)value;
                    break;
            }
        }

        public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)
        {
            entityManager.SetComponentData(domainObjectEntity, this);
        }

        public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity) { }

        public bool Equals(CarriableTrait other)
        {
            return Carrier.Equals(other.Carrier);
        }

        public override int GetHashCode()
        {
            return Carrier.GetHashCode();
        }
    }

    struct LocalizedTrait : IEquatable<LocalizedTrait>, ITrait
    {
        public DomainObjectID Location;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Location):
                    Location = (DomainObjectID)value;
                    break;
            }
        }

        public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)
        {
            entityManager.SetComponentData(domainObjectEntity, this);
        }

        public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity) { }

        public bool Equals(LocalizedTrait other)
        {
            return Location.Equals(other.Location);
        }

        public override int GetHashCode()
        {
            return Location.GetHashCode();
        }
    }

    struct LockableTrait : IEquatable<LockableTrait>, ITrait
    {
        public Bool Locked;

        public void SetField(string fieldName, object value)
        {
            switch (fieldName)
            {
                case nameof(Locked):
                    Locked = (Bool)value;
                    break;
            }
        }

        public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)
        {
            entityManager.SetComponentData(domainObjectEntity, this);
        }

        public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity) { }

        public bool Equals(LockableTrait other)
        {
            return Locked.Equals(other.Locked);
        }

        public override int GetHashCode()
        {
            return Locked.GetHashCode();
        }
    }

    struct EndTrait : IEquatable<EndTrait>, ITrait
    {
        public void SetField(string fieldName, object value) {}

        public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)
        {
            entityManager.SetComponentData(domainObjectEntity, this);
        }

        public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity) {}

        public bool Equals(EndTrait other)
        {
            return true;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }
}
