using System.Collections.Generic;
using KeyDomain;
using NUnit.Framework;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;

namespace Unity.AI.Planner.Tests
{
//    [TestFixture]
//    class DomainObjectTests : ECSTestsFixture
//    {
//        struct NestedTrait : IComponentData
//        {
//            public Localized MyTrait;
//        }
//
//        class DomainObjectUpdater : PolicyGraphUpdateSystem
//        {
//            ComponentType m_LocalizedType;
//            ComponentType m_NestedTraitType;
//            List<Entity> m_EntityListLHS = new List<Entity>();
//            List<Entity> m_EntityListRHS = new List<Entity>();
//
//            protected override void OnCreateManager()
//            {
//                base.OnCreateManager();
//
//                m_NestedTraitType = ComponentType.ReadWrite<NestedTrait>();
//                m_LocalizedType = ComponentType.ReadWrite<Localized>();
//            }
//
//            internal override void HashStates()
//            {
//                var stateEntities = GetEntityQuery(typeof(CreatedStateInfo)).ToEntityArray(Allocator.TempJob);
//                foreach (var stateEntity in stateEntities)
//                {
//                    var domainObjectBuffer = EntityManager.GetBuffer<DomainObjectReference>(stateEntity);
//
//                    var LocalizedLookup = GetComponentDataFromEntity<Localized>(true);
//                    var nestedTraitLookup = GetComponentDataFromEntity<NestedTrait>(true);
//
//                    var hash = 19;
//                    for (var i = 0; i < domainObjectBuffer.Length; i++)
//                    {
//                        var entity = domainObjectBuffer[i].DomainObjectEntity;
//
//                        if (EntityManager.HasComponent(entity, m_NestedTraitType))
//                            hash += nestedTraitLookup[entity].GetHashCode();
//                        if (EntityManager.HasComponent(entity, m_LocalizedType))
//                            hash += LocalizedLookup[entity].GetHashCode();
//                    }
//
//                    EntityManager.SetComponentData(stateEntity, new HashCode { Value = hash, TraitMask = 0 });
//                }
//
//                stateEntities.Dispose();
//            }
//
//            internal override void StateEquals(NativeArray<Entity> createdStates, NativeMultiHashMap<HashCode, Entity> stateLookup, NativeHashMap<Entity, Entity> stateMatches)
//            {
//                throw new System.NotImplementedException();
//            }
//
//            protected override bool StateEquals(Entity lhsStateEntity, Entity rhsStateEntity)
//            {
//                m_EntityListLHS.Clear();
//                m_EntityListRHS.Clear();
//
//                // Check for same number of domain objects.
//                var lhsObjectBuffer = EntityManager.GetBuffer<DomainObjectReference>(lhsStateEntity);
//                var rhsObjectBuffer = EntityManager.GetBuffer<DomainObjectReference>(rhsStateEntity);
//
//                if (lhsObjectBuffer.Length != rhsObjectBuffer.Length)
//                    return false;
//
//                // Set up entity lists.
//                for (int i = 0; i < lhsObjectBuffer.Length; i++)
//                {
//                    m_EntityListLHS.Add(lhsObjectBuffer[i].DomainObjectEntity);
//                }
//
//                for (int i = 0; i < rhsObjectBuffer.Length; i++)
//                {
//                    m_EntityListRHS.Add(rhsObjectBuffer[i].DomainObjectEntity);
//                }
//
//                var LocalizedLookup = GetComponentDataFromEntity<Localized>(true);
//                var nestedTraitLookup = GetComponentDataFromEntity<NestedTrait>(true);
//
//                while (m_EntityListLHS.Count > 0)
//                {
//                    var entityLHS = m_EntityListLHS[0];
//
//                    var hasNested = EntityManager.HasComponent(entityLHS, m_NestedTraitType);
//                    var hasLocalized = EntityManager.HasComponent(entityLHS, m_LocalizedType);
//
//                    var foundMatch = false;
//                    var lhsTypes = EntityManager.GetComponentTypes(entityLHS);
//                    for (var rhsIndex = 0; rhsIndex < m_EntityListRHS.Count; rhsIndex++)
//                    {
//                        var entityRHS = m_EntityListRHS[rhsIndex];
//
//                        var rhsTypes = EntityManager.GetComponentTypes(entityRHS);
//                        if (!TraitBasedDomain.ContainsRequiredComponentTypes(lhsTypes, rhsTypes))
//                        {
//                            rhsTypes.Dispose();
//                            continue;
//                        }
//
//                        rhsTypes.Dispose();
//
//                        if (hasNested && !nestedTraitLookup[entityLHS].Equals(nestedTraitLookup[entityRHS]))
//                            continue;
//
//                        if (hasLocalized && !LocalizedLookup[entityLHS].Equals(LocalizedLookup[entityRHS]))
//                            continue;
//
//                        m_EntityListLHS.RemoveAt(0);
//                        m_EntityListRHS.RemoveAt(rhsIndex);
//                        foundMatch = true;
//                        break;
//                    }
//
//                    lhsTypes.Dispose();
//
//                    if (!foundMatch)
//                        return false;
//                }
//
//                return true;
//            }
//        }
//
//        [Test]
//        public void AddTraitsToDomainObject()
//        {
//            var stateEntity = TraitBasedDomain.CreateState(m_Manager);
//
//            var domainObjectEntity = TraitBasedDomain.CreateDomainObject(m_Manager, stateEntity);
//
//            var Localized = new Localized();
//            m_Manager.AddComponentData(domainObjectEntity, Localized);
//
//            Assert.AreEqual(Localized, m_Manager.GetComponentData<Localized>(domainObjectEntity));
//        }
//
//        [Test]
//        public void CompareDifferentTraits()
//        {
//            var Localized = new Localized();
//
//            var stateEntity = TraitBasedDomain.CreateState(m_Manager);
//
//            var domainObject1Entity = TraitBasedDomain.CreateDomainObject(m_Manager, stateEntity);
//            var domainObject1ID = m_Manager.GetComponentData<DomainObjectTrait>(domainObject1Entity).ID;
//            m_Manager.AddComponentData(domainObject1Entity, Localized);
//
//            var Carrier1 = new Carrier { CarriedObject = domainObject1ID };
//
//            var domainObject2Entity = TraitBasedDomain.CreateDomainObject(m_Manager, stateEntity);
//            var domainObject2ID = m_Manager.GetComponentData<DomainObjectTrait>(domainObject2Entity).ID;
//            m_Manager.AddComponentData(domainObject2Entity, Localized);
//
//            var Carrier2 = new Carrier { CarriedObject = domainObject2ID };
//
//            Assert.AreNotEqual(Carrier1, Carrier2);
//        }
//
//        [Test]
//        public void CompareEqualTraits()
//        {
//            var stateEntity = TraitBasedDomain.CreateState(m_Manager);
//            var domainObjectEntity = TraitBasedDomain.CreateDomainObject(m_Manager, stateEntity);
//            var domainObjectID = m_Manager.GetComponentData<DomainObjectTrait>(domainObjectEntity).ID;
//
//            var Carrier = new Carrier { CarriedObject = domainObjectID };
//            var Carrier2 = new Carrier { CarriedObject = domainObjectID };
//
//            Assert.AreEqual(Carrier, Carrier2);
//        }
//
//        [Test]
//        public void CopyDomainObject()
//        {
//            var stateEntity = TraitBasedDomain.CreateState(m_Manager);
//
//            var Localized = new Localized();
//            var nestedTrait = new NestedTrait { MyTrait = Localized };
//
//            var domainObjectEntity = TraitBasedDomain.CreateDomainObject(m_Manager, stateEntity);
//            m_Manager.AddComponentData(domainObjectEntity, Localized);
//            m_Manager.AddComponentData(domainObjectEntity, nestedTrait);
//
//            var domainObjectCopyEntity = m_Manager.Instantiate(domainObjectEntity);
//
//            Assert.AreEqual(m_Manager.GetComponentData<Localized>(domainObjectCopyEntity),
//                m_Manager.GetComponentData<Localized>(domainObjectEntity));
//
//            Assert.AreEqual(m_Manager.GetComponentData<NestedTrait>(domainObjectCopyEntity),
//                m_Manager.GetComponentData<NestedTrait>(domainObjectEntity));
//        }
//    }
}
