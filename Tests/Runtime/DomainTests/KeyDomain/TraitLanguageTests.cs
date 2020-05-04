using System.Collections;
using NUnit.Framework;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Tests;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using KeyDomain;

#if ENABLE_PERFORMANCE_TESTS
using Unity.PerformanceTesting;
using Unity.AI.Planner.Tests.Performance;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Unity.AI.DomainLanguage.TraitBased.Tests
{
    class KeyDomainTestFixture : ECSTestsFixture
    {
        protected StateManager m_StateManager;

        public override void Setup()
        {
            base.Setup();

            KeyDomainUtility.Initialize(World);
            m_StateManager = World.GetOrCreateSystem<StateManager>();

            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(World);
        }

        public override void TearDown()
        {
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(null);
            base.TearDown();
        }

        protected static IEnumerator UpdateSystems()
        {
#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
#endif
            yield return null;
        }
    }
}

namespace Unity.AI.DomainLanguage.TraitBased.Tests.Unit
{
    [Category("Unit")]
    class KeyDomainTests : KeyDomainTestFixture
    {
        bool CompareObjectsAcrossStates(TraitBasedObject object1, StateData state1, TraitBasedObject object2,
            StateData state2)
        {
            if (!object1.HasSameTraits(object2))
                return false;

            if (object1.CarrierIndex != TraitBasedObject.Unset)
            {
                if (!state1.CarrierBuffer[object1.CarrierIndex].Equals(state2.CarrierBuffer[object2.CarrierIndex]))
                    return false;
            }
            if (object1.CarriableIndex != TraitBasedObject.Unset)
            {
                if (!state1.CarriableBuffer[object1.CarriableIndex].Equals(state2.CarriableBuffer[object2.CarriableIndex]))
                    return false;
            }
            if (object1.ColoredIndex != TraitBasedObject.Unset)
            {
                if (!state1.ColoredBuffer[object1.ColoredIndex].Equals(state2.ColoredBuffer[object2.ColoredIndex]))
                    return false;
            }
            if (object1.LocalizedIndex != TraitBasedObject.Unset)
            {
                if (!state1.LocalizedBuffer[object1.LocalizedIndex].Equals(state2.LocalizedBuffer[object2.LocalizedIndex]))
                    return false;
            }
            if (object1.LockableIndex != TraitBasedObject.Unset)
            {
                if (!state1.LockableBuffer[object1.LockableIndex].Equals(state2.LockableBuffer[object2.LockableIndex]))
                    return false;
            }
            if (object1.EndIndex != TraitBasedObject.Unset)
            {
                if (!state1.EndBuffer[object1.EndIndex].Equals(state2.EndBuffer[object2.EndIndex]))
                    return false;
            }

            return true;
        }

        [Test]
        public void StatesWithIdenticalPropertyValuesSucceedEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            // All other objects remain identical
            for (int i = 0; i < stateCopy.TraitBasedObjects.Length; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.TraitBasedObjects[i], stateCopy, originalState.TraitBasedObjects[i], originalState));
            }

            Assert.IsTrue(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void StatesWithDifferentPropertyValuesFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var startRoom = stateCopy.TraitBasedObjects[0];
            stateCopy.SetTraitOnObject(new Colored { Color = ColorValue.White }, ref startRoom);

            // First object has now been changed
            Assert.IsFalse(CompareObjectsAcrossStates(startRoom, stateCopy, originalState.TraitBasedObjects[0], originalState));

            // All other objects remain identical
            for (int i = 1; i < stateCopy.TraitBasedObjects.Length; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.TraitBasedObjects[i], stateCopy, originalState.TraitBasedObjects[i], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void RemovingTraitShouldFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var traitBasedObject = stateCopy.TraitBasedObjects[0];
            stateCopy.RemoveTraitOnObject<Colored>(ref traitBasedObject);

            // First object has now been changed
            Assert.IsFalse(CompareObjectsAcrossStates(traitBasedObject, stateCopy, originalState.TraitBasedObjects[0], originalState));

            // All other objects remain identical
            for (int i = 1; i < stateCopy.TraitBasedObjects.Length; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.TraitBasedObjects[i], stateCopy, originalState.TraitBasedObjects[i], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(originalState, stateCopy));
        }

        [Test]
        public void AdditionOfTraitsShouldFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var agentObject = stateCopy.TraitBasedObjects[4];
            stateCopy.SetTraitOnObject(new Colored(), ref agentObject);

            // Agent object has now been changed
            Assert.IsFalse(CompareObjectsAcrossStates(agentObject, stateCopy, originalState.TraitBasedObjects[4], originalState));

            // All other objects remain identical
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.TraitBasedObjects[i], stateCopy, originalState.TraitBasedObjects[i], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void RemovalOfObjectsShouldFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var firstTraitBasedObject = stateCopy.TraitBasedObjects[0];
            stateCopy.RemoveObject(firstTraitBasedObject);

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.TraitBasedObjects[i], stateCopy, originalState.TraitBasedObjects[i+1], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void AdditionOfObjectsShouldFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var types = new NativeArray<ComponentType>(1, Allocator.TempJob) { [0] = ComponentType.ReadWrite<Colored>() };
            stateCopy.AddObject(types, out _, out _);
            types.Dispose();

            for (int i = 0; i < originalState.TraitBasedObjects.Length; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.TraitBasedObjects[i], stateCopy, originalState.TraitBasedObjects[i], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void ActionBindsCorrectArguments()
        {
            var statesToExpand = new NativeList<StateEntityKey>(1, Allocator.TempJob);
            statesToExpand.Add(KeyDomainUtility.InitialStateKey);

            var moveActionDataContext = m_StateManager.GetStateDataContext();
            var moveActionECB = new EntityCommandBuffer(Allocator.TempJob);
            moveActionDataContext.EntityCommandBuffer = moveActionECB.ToConcurrent();

            var move = new MoveAction(statesToExpand, moveActionDataContext);
            var jobHandle = JobHandle.CombineDependencies(move.Schedule(statesToExpand, default), m_StateManager.EntityManager.ExclusiveEntityTransactionDependency);
            jobHandle.Complete();

            moveActionECB.Playback(m_StateManager.ExclusiveEntityTransaction);
            moveActionECB.Dispose();
            statesToExpand.Dispose();

            var moveActionTransitions = m_StateManager.EntityManager.GetBuffer<MoveAction.FixupReference>(KeyDomainUtility.InitialStateKey.Entity);

            Assert.AreEqual(1, moveActionTransitions.Length);

            var initialState = KeyDomainUtility.InitialState;
            var firstRoomIndex = initialState.GetTraitBasedObjectIndex(KeyDomainUtility.FirstRoom);
            var actionKey = moveActionTransitions[0].TransitionInfo.StateTransition.ActionKey;

            Assert.AreEqual(firstRoomIndex, actionKey[MoveAction.k_RoomIndex]);
        }

        [Test]
        public void ActionFailsToBindArguments()
        {
            var statesToExpand = new NativeList<StateEntityKey>(1, Allocator.TempJob);
            statesToExpand.Add(KeyDomainUtility.InitialStateKey);

            var unlockRoomDataContext = m_StateManager.GetStateDataContext();
            var unlockRoomECB = new EntityCommandBuffer(Allocator.TempJob);
            unlockRoomDataContext.EntityCommandBuffer = unlockRoomECB.ToConcurrent();

            var unlockRoomAction = new UnlockRoomAction(statesToExpand, unlockRoomDataContext);
            var jobHandle = JobHandle.CombineDependencies(unlockRoomAction.Schedule(statesToExpand, default), m_StateManager.EntityManager.ExclusiveEntityTransactionDependency);
            jobHandle.Complete();

            unlockRoomECB.Playback(m_StateManager.ExclusiveEntityTransaction);
            unlockRoomECB.Dispose();
            statesToExpand.Dispose();

            var unlockRoomTransitions = m_StateManager.EntityManager.GetBuffer<UnlockRoomAction.FixupReference>(KeyDomainUtility.InitialStateKey.Entity);

            Assert.AreEqual(0, unlockRoomTransitions.Length);
        }

        [Test]
        public void ChangingIdShouldSucceedEquality()
        {
            var stateData = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);

            var agentObject = stateData.TraitBasedObjects[stateData.GetTraitBasedObjectIndex(KeyDomainUtility.Agent)];
            var carriedId = stateData.CarrierBuffer[agentObject.CarrierIndex].CarriedObject;
            Assert.IsFalse(carriedId == ObjectId.None);

            int keyIndex;
            for (keyIndex = 0; keyIndex < stateData.TraitBasedObjectIds.Length; keyIndex++)
            {
                if (stateData.TraitBasedObjectIds[keyIndex].Id.Equals(carriedId))
                    break;
            }

            var newId = ObjectId.GetNext();
            stateData.TraitBasedObjectIds[keyIndex] = new TraitBasedObjectId { Id = newId };
            stateData.CarrierBuffer[agentObject.CarrierIndex] = new Carrier { CarriedObject = newId };

            Assert.IsTrue(m_StateManager.Equals(KeyDomainUtility.InitialState, stateData));
        }

        [Test]
        public void StatesWithChangedRelationshipsShouldFailEquality()
        {
            var stateData = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);

            var agentObject = stateData.TraitBasedObjects[stateData.GetTraitBasedObjectIndex(KeyDomainUtility.Agent)];
            var carriedId = stateData.CarrierBuffer[agentObject.CarrierIndex].CarriedObject;
            Assert.IsFalse(carriedId == ObjectId.None);


            var keyIndices = new NativeList<int>(Allocator.TempJob);
            stateData.GetTraitBasedObjectIndices(keyIndices, ComponentType.ReadWrite<Colored>(), ComponentType.ReadWrite<Carriable>());

            for (int i = 0; i < keyIndices.Length; i++)
            {
                var keyIndex = keyIndices[i];
                var keyColor = stateData.GetTraitOnObjectAtIndex<Colored>(keyIndex);
                keyColor.Color = keyColor.Color == ColorValue.Black ? ColorValue.White : ColorValue.Black;
                stateData.SetTraitOnObjectAtIndex(keyColor, keyIndex);
            }

            keyIndices.Dispose();

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateData));
        }

        [Test]
        public void MultipleObjectTraversalWithChangedTraitsShouldFailEquality()
        {
            var stateData = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);

            int startRoomIndex;
            for (startRoomIndex = 0; startRoomIndex < stateData.TraitBasedObjectIds.Length; startRoomIndex++)
            {
                if (KeyDomainUtility.StartRoomId.Equals(stateData.TraitBasedObjectIds[startRoomIndex].Id))
                    break;
            }

            var colored = stateData.GetTraitOnObjectAtIndex<Colored>(startRoomIndex);
            colored.Color = colored.Color == ColorValue.Black ? ColorValue.White : ColorValue.Black;
            stateData.SetTraitOnObjectAtIndex(colored, startRoomIndex);

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateData));
        }
    }
}

namespace Unity.AI.DomainLanguage.TraitBased.Tests.Performance
{
#if ENABLE_PERFORMANCE_TESTS
    [Category("Performance")]
    class KeyDomainTests : KeyDomainTestFixture
    {
        StateEntityKey m_LargeStateKey;
        StateData m_LargeStateData => m_StateManager.GetStateData(m_LargeStateKey);
        NativeArray<ComponentType> m_RoomType;

        [SetUp]
        public override void Setup()
        {
           base.Setup();
           m_LargeStateKey = m_StateManager.CopyState(KeyDomainUtility.InitialStateKey);
           m_RoomType = new NativeArray<ComponentType>(2, Allocator.TempJob) {[0] = ComponentType.ReadWrite<Lockable>(), [1] = ComponentType.ReadWrite<Colored>()};
           Add500Rooms(m_StateManager.GetStateData(m_LargeStateKey, readWrite:true));
        }

        void Add500Rooms(StateData stateData)
        {
            for (int i = 0; i < 500; i++)
            {
                stateData.AddObject(m_RoomType, out _, out _);
            }
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            m_RoomType.Dispose();
        }

        [Test, Performance]
        public void TestStateEquality500Rooms()
        {
            var stateCopy = m_StateManager.CopyStateData(m_LargeStateData);
            bool areEqual = true;

            Measure.Method(() =>
            {
                areEqual |= m_StateManager.Equals(m_LargeStateData, stateCopy);
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            Assert.IsTrue(areEqual);

            PerformanceUtility.AssertRange(0.32, 0.46);
        }

        [Test, Performance]
        public void TestStateHashing500Rooms()
        {
            var stateData = m_LargeStateData;

            Measure.Method(() =>
            {
                m_StateManager.GetHashCode(stateData);
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.22, 0.41);
        }

        [Test, Performance]
        public void TestStateCopying500Rooms()
        {
            var stateData = m_LargeStateData;

            Measure.Method(() =>
            {
                m_StateManager.CopyStateData(stateData);
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.02, 0.06);
        }

        [Test, Performance]
        public void TestRemoveFirstObjectTrait500Rooms()
        {
            StateData stateData = default;
            TraitBasedObject traitBasedObject = default;

            Measure.Method(() =>
            {
                stateData.RemoveTraitOnObject<Colored>(ref traitBasedObject);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                for (int i = 0; i < stateData.TraitBasedObjects.Length; i++)
                {
                    traitBasedObject = stateData.TraitBasedObjects[i];
                    var coloredIndex = traitBasedObject.ColoredIndex;

                    if (coloredIndex != TraitBasedObject.Unset)
                    {
                        break;
                    }
                }
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.05, 0.075);
        }

        [Test, Performance]
        public void TestRemoveFirstObject500Rooms()
        {
            StateData stateData = default;
            TraitBasedObject traitBasedObject = default;

            Measure.Method(() =>
            {
                stateData.RemoveObject(traitBasedObject);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                traitBasedObject = stateData.TraitBasedObjects[0];
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.11, 0.145);
        }

        [Test, Performance]
        public void TestRemoveLastObject500Rooms()
        {
            StateData stateData = default;
            TraitBasedObject traitBasedObject = default;

            Measure.Method(() =>
            {
                stateData.RemoveObject(traitBasedObject);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                traitBasedObject = stateData.TraitBasedObjects[stateData.TraitBasedObjects.Length-1];
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.45, 0.55);
        }

        [Test, Performance]
        public void TestRemoveLastObjectTrait500Rooms()
        {
            StateData stateData = default;
            TraitBasedObject traitBasedObject = default;

            Measure.Method(() =>
            {
                stateData.RemoveTraitOnObject<Colored>(ref traitBasedObject);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                traitBasedObject = stateData.TraitBasedObjects[stateData.TraitBasedObjects.Length-1];
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.16, 0.19);
        }

        [Test, Performance]
        public void TestObjectFiltering500Rooms()
        {
            StateData stateData = default;
            var objects = new NativeList<int>(Allocator.Temp);
            var types = new NativeArray<ComponentType>(2, Allocator.TempJob) { [0] = ComponentType.ReadWrite<Colored>(), [1] = ComponentType.ReadWrite<Lockable>() };

            Measure.Method(() =>
            {
                stateData.GetTraitBasedObjectIndices(objects, types);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                objects.Clear();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            types.Dispose();
            objects.Dispose();

            PerformanceUtility.AssertRange(0.11, 0.19);
        }
    }
#endif
}

