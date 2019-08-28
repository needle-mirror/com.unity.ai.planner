using System.Collections;
using KeyDomain;
using NUnit.Framework;
using Unity.AI.Planner.Tests;
using Unity.AI.Planner.Tests.Performance;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
#if ENABLE_PERFORMANCE_TESTS
using Unity.PerformanceTesting;
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
        bool CompareObjectsAcrossStates(DomainObject object1, StateData state1, DomainObject object2,
            StateData state2)
        {
            if (!object1.HasSameTraits(object2))
                return false;

            if (object1.CarrierIndex != DomainObject.Unset)
            {
                if (!state1.CarrierBuffer[object1.CarrierIndex].Equals(state2.CarrierBuffer[object2.CarrierIndex]))
                    return false;
            }
            if (object1.CarriableIndex != DomainObject.Unset)
            {
                if (!state1.CarriableBuffer[object1.CarriableIndex].Equals(state2.CarriableBuffer[object2.CarriableIndex]))
                    return false;
            }
            if (object1.ColoredIndex != DomainObject.Unset)
            {
                if (!state1.ColoredBuffer[object1.ColoredIndex].Equals(state2.ColoredBuffer[object2.ColoredIndex]))
                    return false;
            }
            if (object1.LocalizedIndex != DomainObject.Unset)
            {
                if (!state1.LocalizedBuffer[object1.LocalizedIndex].Equals(state2.LocalizedBuffer[object2.LocalizedIndex]))
                    return false;
            }
            if (object1.LockableIndex != DomainObject.Unset)
            {
                if (!state1.LockableBuffer[object1.LockableIndex].Equals(state2.LockableBuffer[object2.LockableIndex]))
                    return false;
            }
            if (object1.EndIndex != DomainObject.Unset)
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
            for (int i = 0; i < stateCopy.DomainObjects.Length; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.DomainObjects[i], stateCopy, originalState.DomainObjects[i], originalState));
            }

            Assert.IsTrue(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void StatesWithDifferentPropertyValuesFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var startRoom = stateCopy.DomainObjects[0];
            stateCopy.SetTraitOnObject(new Colored { Color = ColorValue.White }, ref startRoom);

            // First object has now been changed
            Assert.IsFalse(CompareObjectsAcrossStates(startRoom, stateCopy, originalState.DomainObjects[0], originalState));

            // All other objects remain identical
            for (int i = 1; i < stateCopy.DomainObjects.Length; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.DomainObjects[i], stateCopy, originalState.DomainObjects[i], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void RemovingTraitShouldFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var domainObject = stateCopy.DomainObjects[0];
            stateCopy.RemoveTraitOnObject<Colored>(ref domainObject);

            // First object has now been changed
            Assert.IsFalse(CompareObjectsAcrossStates(domainObject, stateCopy, originalState.DomainObjects[0], originalState));

            // All other objects remain identical
            for (int i = 1; i < stateCopy.DomainObjects.Length; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.DomainObjects[i], stateCopy, originalState.DomainObjects[i], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(originalState, stateCopy));
        }

        [Test]
        public void AdditionOfTraitsShouldFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var agentObject = stateCopy.DomainObjects[4];
            stateCopy.SetTraitOnObject(new Colored(), ref agentObject);

            // Agent object has now been changed
            Assert.IsFalse(CompareObjectsAcrossStates(agentObject, stateCopy, originalState.DomainObjects[4], originalState));

            // All other objects remain identical
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.DomainObjects[i], stateCopy, originalState.DomainObjects[i], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void RemovalOfObjectsShouldFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            var firstDomainObject = stateCopy.DomainObjects[0];
            stateCopy.RemoveDomainObject(firstDomainObject);

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.DomainObjects[i], stateCopy, originalState.DomainObjects[i+1], originalState));
            }

            Assert.IsFalse(m_StateManager.Equals(KeyDomainUtility.InitialState, stateCopy));
        }

        [Test]
        public void AdditionOfObjectsShouldFailEquality()
        {
            var stateCopy = m_StateManager.CopyStateData(KeyDomainUtility.InitialState);
            var originalState = KeyDomainUtility.InitialState;

            stateCopy.AddDomainObject(new[] { (ComponentType) typeof(Colored) });

            for (int i = 0; i < originalState.DomainObjects.Length; i++)
            {
                Assert.IsTrue(CompareObjectsAcrossStates(stateCopy.DomainObjects[i], stateCopy, originalState.DomainObjects[i], originalState));
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
            move.Schedule(statesToExpand, 0).Complete();

            moveActionECB.Playback(m_StateManager.EntityManager);
            moveActionECB.Dispose();
            statesToExpand.Dispose();

            var moveActionTransitions = m_StateManager.EntityManager.GetBuffer<MoveAction.FixupReference>(KeyDomainUtility.InitialStateKey.Entity);

            Assert.AreEqual(1, moveActionTransitions.Length);

            var initialState = KeyDomainUtility.InitialState;
            var firstRoomIndex = initialState.GetDomainObjectIndex(KeyDomainUtility.FirstRoom);
            var actionKey = moveActionTransitions[0].TransitionInfo.Item2;

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
            unlockRoomAction.Schedule(statesToExpand, 0).Complete();

            unlockRoomECB.Playback(m_StateManager.EntityManager);
            unlockRoomECB.Dispose();
            statesToExpand.Dispose();

            var unlockRoomTransitions = m_StateManager.EntityManager.GetBuffer<UnlockRoomAction.FixupReference>(KeyDomainUtility.InitialStateKey.Entity);

            Assert.AreEqual(0, unlockRoomTransitions.Length);
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

        [SetUp]
        public override void Setup()
        {
           base.Setup();
           m_LargeStateKey = m_StateManager.CopyState(KeyDomainUtility.InitialStateKey);
           Add500Rooms(m_StateManager.GetStateData(m_LargeStateKey, readWrite:true));
        }

        void Add500Rooms(StateData stateData)
        {
            var roomType = new ComponentType[] { typeof(Lockable), typeof(Colored) };
            for (int i = 0; i < 500; i++)
            {
                stateData.AddDomainObject(roomType);
            }
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

            PerformanceUtility.AssertRange(0.27, 0.41);
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
            DomainObject domainObject = default;
            Measure.Method(() =>
            {
                stateData.RemoveTraitOnObject<Colored>(ref domainObject);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                for (int i = 0; i < stateData.DomainObjects.Length; i++)
                {
                    domainObject = stateData.DomainObjects[i];
                    var coloredIndex = domainObject.ColoredIndex;

                    if (coloredIndex != DomainObject.Unset)
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
            DomainObject domainObject = default;
            Measure.Method(() =>
            {
                stateData.RemoveDomainObject(domainObject);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                domainObject = stateData.DomainObjects[0];
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.11, 0.145);
        }

        [Test, Performance]
        public void TestRemoveLastObject500Rooms()
        {
            StateData stateData = default;
            DomainObject domainObject = default;
            Measure.Method(() =>
            {
                stateData.RemoveDomainObject(domainObject);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                domainObject = stateData.DomainObjects[stateData.DomainObjects.Length-1];
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.43, 0.50);
        }

        [Test, Performance]
        public void TestRemoveLastObjectTrait500Rooms()
        {
            StateData stateData = default;
            DomainObject domainObject = default;
            Measure.Method(() =>
            {
                stateData.RemoveTraitOnObject<Colored>(ref domainObject);
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                domainObject = stateData.DomainObjects[stateData.DomainObjects.Length-1];
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.16, 0.19);
        }

        [Test, Performance]
        public void TestObjectFiltering500Rooms()
        {
            StateData stateData = default;
            var objects = new NativeList<(DomainObject, int)>(Allocator.Temp);
            Measure.Method(() =>
            {
                stateData.GetDomainObjects(objects, typeof(Colored), typeof(Lockable));
            }).SetUp(() =>
            {
                stateData = m_StateManager.CopyStateData(m_LargeStateData);
                objects.Clear();
            }).WarmupCount(1).MeasurementCount(30).IterationsPerMeasurement(1).Run();

            PerformanceUtility.AssertRange(0.11, 0.175);
        }
    }
#endif
}

