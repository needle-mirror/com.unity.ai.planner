//using System.Collections;
//using KeyDomain;
//using NUnit.Framework;
//using Unity.AI.Planner.DomainLanguage.TraitBased;
//using Unity.Collections;
//using Unity.Entities;
//using UnityEngine.TestTools;
//using PGN = Unity.AI.Planner.PolicyGraphNode;
//
//namespace Unity.AI.Planner.Tests
//{
//    [TestFixture]
//    class StateTests : KeyDomainTestFixture
//    {
//        [UnityTest]
//        public IEnumerator RemoveObjectFromState()
//        {
//            yield return PrewarmSystems();
//
//            var policyGraphNodeEntity = m_PolicyGraph.PolicyGraphRootEntity;
//            var stateEntity = m_EntityManager.GetComponentData<PGN>(policyGraphNodeEntity).StateEntity;
//
//            Assert.IsTrue(TraitBasedDomain.RemoveDomainObject(m_EntityManager, stateEntity, KeyDomain.BlackKey));
//
//            var domainObjectReferences = m_EntityManager.GetBuffer<DomainObjectReference>(stateEntity);
//            Assert.AreEqual(4, domainObjectReferences.Length);
//        }
//
//        [UnityTest]
//        public IEnumerator CopyStateKeyDomain()
//        {
//            yield return PrewarmSystems();
//
//            var policyGraphNodeEntity = m_PolicyGraph.PolicyGraphRootEntity;
//            var stateEntity = m_EntityManager.GetComponentData<PGN>(policyGraphNodeEntity).StateEntity;
//
//            var stateCopyEntity = TraitBasedDomain.CopyState(m_EntityManager, stateEntity);
//            AssertStatesAreShallowEqual(stateEntity, stateCopyEntity);
//        }
//
//        [UnityTest]
//        public IEnumerator DeferredDestroy()
//        {
////            yield return PrewarmSystems();
//
//            var entityManager = World.EntityManager;
//            var entity = entityManager.CreateEntity(typeof(HashCode), typeof(DomainObjectReference));
//
//            var domainObjectEntity = entityManager.CreateEntity(typeof(HashCode));
//            var domainObjectBuffer = entityManager.GetBuffer<DomainObjectReference>(entity);
//            domainObjectBuffer.Add(new DomainObjectReference() { DomainObjectEntity = domainObjectEntity });
//
//            var ecb = new EntityCommandBuffer(Allocator.TempJob);
//            ecb.SetComponent(entity, new HashCode() { Value = int.MaxValue });
//            ecb.DestroyEntity(entity);
//
//            ecb.Playback(entityManager);
//            ecb.Dispose();
//
//            Assert.IsFalse(entityManager.Exists(entity));
//            yield break;
//        }
//
//        // TODO: There isn't a generic way to do deep state equality yet
////        [Test]
////        public void StateEqualityWithDifferentObjectOrder()
////        {
////            var keyDomain = new KeyDomain();
////            var state = (State)keyDomain.InitialState();
////            var copy = state;
////
////            var objects = new List<DomainObject>();
////            var objEnumerator = state.GetObjectsEnumerator(new AnyTraitFilter());
////            while (objEnumerator.MoveNext())
////            {
////                objects.Add(objEnumerator.Current);
////                copy.RemoveObject(objEnumerator.Current);
////            }
////
////            for (int i = objects.Count - 1; i >= 0; i--)
////                copy.AddObject(objects[i]);
////
////            Assert.AreEqual(state, copy);
////        }
//
//        // TODO: Update test for ECS to use BaseAgent.GetDomainObjects()
////        [Test]
////        public void FilterObjects()
////        {
////            var location = new Location();
////
////            var obj = new DomainObject();
////            obj.AddTrait(location);
////
////            var obj2 = new DomainObject();
////            obj2.AddTrait(location);
////
////            var obj3 = new DomainObject();
////
////            var state = new State();
////            state.AddObject(obj);
////            state.AddObject(obj2);
////            state.AddObject(obj3);
////
////            var enumerator = state.GetObjectsEnumerator(new TraitFilter<Location>());
////
////            enumerator.MoveNext();
////            Assert.AreSame(enumerator.Current, obj);
////
////            enumerator.MoveNext();
////            Assert.AreSame(enumerator.Current, obj2);
////
////            Assert.IsFalse(enumerator.MoveNext());
////        }
//
//        void AssertStatesAreShallowEqual(Entity x, Entity y)
//        {
//            Assert.AreNotEqual(x, y);
//
//            var xDomainObjectReferences = m_EntityManager.GetBuffer<DomainObjectReference>(x);
//            var yDomainObjectReferences = m_EntityManager.GetBuffer<DomainObjectReference>(y);
//
//            Assert.AreEqual(xDomainObjectReferences.Length, yDomainObjectReferences.Length);
//            for (var i = 0; i < xDomainObjectReferences.Length; i++) Assert.AreNotEqual(xDomainObjectReferences[i].DomainObjectEntity, yDomainObjectReferences[i].DomainObjectEntity);
//        }
//
//        [Test]
//        public void AddDomainObjectToState()
//        {
//            var stateEntity = TraitBasedDomain.CreateState(m_EntityManager);
//
//            var domainObjectEntity = TraitBasedDomain.CreateDomainObject(m_EntityManager, stateEntity,
//                typeof(Localized), typeof(Location));
//            var Localized = new Localized();
//            m_EntityManager.SetComponentData(domainObjectEntity, Localized);
//            m_EntityManager.SetComponentData(domainObjectEntity, new Location());
//
//            var foundObject = false;
//            var domainObjectReferences = m_EntityManager.GetBuffer<DomainObjectReference>(stateEntity);
//            for (var i = 0; i < domainObjectReferences.Length; i++)
//                if (domainObjectReferences[i].DomainObjectEntity == domainObjectEntity)
//                {
//                    foundObject = true;
//                    break;
//                }
//
//            Assert.IsTrue(foundObject);
//        }
//
//        [Test]
//        public void TestECBBuffers()
//        {
//            var entity = m_EntityManager.CreateEntity();
//            m_EntityManager.AddBuffer<DomainObjectReference>(entity);
//            var buffer = m_EntityManager.GetBuffer<DomainObjectReference>(entity);
//
//            buffer.Add(new DomainObjectReference());
//            Assert.AreEqual(1, buffer.Length);
//
//            var ecb = new EntityCommandBuffer(Allocator.TempJob);
//            var ecbBuffer = ecb.SetBuffer<DomainObjectReference>(entity);
//
//            ecbBuffer.Add(new DomainObjectReference());
//            ecbBuffer.Add(new DomainObjectReference());
//            Assert.AreEqual(2, ecbBuffer.Length);
//
//            ecb.Playback(m_EntityManager);
//
//            buffer = m_EntityManager.GetBuffer<DomainObjectReference>(entity);
//            Assert.AreEqual(2, buffer.Length);
//
//            ecb.Dispose();
//
//        }
//
//        [Test]
//        public void TestECBAddThenSetBuffers()
//        {
//            var entity = m_EntityManager.CreateEntity();
//
//            var ecb = new EntityCommandBuffer(Allocator.TempJob);
//            var ecbBuffer = ecb.AddBuffer<DomainObjectReference>(entity);
//
//            ecbBuffer.Add(new DomainObjectReference());
//
//            Assert.AreEqual(1, ecbBuffer.Length);
//
//            ecbBuffer = ecb.SetBuffer<DomainObjectReference>(entity);
//
//            ecbBuffer.Add(new DomainObjectReference());
//            ecbBuffer.Add(new DomainObjectReference());
//
//            Assert.AreEqual(2, ecbBuffer.Length);
//
//            ecb.Playback(m_EntityManager);
//
//            var buffer = m_EntityManager.GetBuffer<DomainObjectReference>(entity);
//            Assert.AreEqual(2, buffer.Length);
//
//            ecb.Dispose();
//
//        }
//
//        // TODO: We don't have a RemoveObjects method yet for ECS
////        [Test]
////        public void RemoveObjectsFromState()
////        {
////            var keyDomain = new KeyDomain();
////            var state = (State)keyDomain.InitialState();
////
////            state.RemoveObjects(keyDomain.BlackKey, keyDomain.Agent);
////
////            Assert.AreEqual(3, state.ObjectCount);
////        }
//
//        [Test]
//        public void CopyState()
//        {
//            var stateEntity = TraitBasedDomain.CreateState(m_EntityManager);
//
//            var domainObjectEntity = TraitBasedDomain.CreateDomainObject(m_EntityManager, stateEntity, typeof(Localized));
//            var Localized = new Localized();
//            m_EntityManager.SetComponentData(domainObjectEntity, Localized);
//
//            var stateCopyEntity = TraitBasedDomain.CopyState(m_EntityManager, stateEntity);
//            AssertStatesAreShallowEqual(stateEntity, stateCopyEntity);
//        }
//    }
//}
