using NUnit.Framework;
using Unity.Entities;

namespace Unity.AI.Planner.Tests
{
    abstract class ECSTestsFixture
    {
        protected World m_PreviousWorld;
        protected World World;
        protected EntityManager m_Manager;
        protected EntityManager.EntityManagerDebug m_ManagerDebug;

        [SetUp]
        public virtual void Setup()
        {
            // Redirect Log messages in NUnit which get swallowed (from GC invoking destructor in some cases)
            // System.Console.SetOut(NUnit.Framework.TestContext.Out);

            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
#if !UNITY_DOTSPLAYER
            World = World.DefaultGameObjectInjectionWorld = new World("Test World");
#else
            Unity.Burst.DotsRuntimeInitStatics.Init();
            World = DefaultTinyWorldInitialization.Initialize("Test World");
#endif

            m_Manager = World.EntityManager;
            m_ManagerDebug = new EntityManager.EntityManagerDebug(m_Manager);

#if !UNITY_DOTSPLAYER
#if !UNITY_2019_2_OR_NEWER
            // Not raising exceptions can easily bring unity down with massive logging when tests fail.
            // From Unity 2019.2 on this field is always implicitly true and therefore removed.

            UnityEngine.Assertions.Assert.raiseExceptions = true;
#endif  // #if !UNITY_2019_2_OR_NEWER
#endif  // #if !UNITY_DOTSPLAYER
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (m_Manager != null && m_Manager.IsCreated)
            {
                // Clean up systems before calling CheckInternalConsistency because we might have filters etc
                // holding on SharedComponentData making checks fail
                while (World.Systems.Count > 0)
                {
                    World.DestroySystem(World.Systems[0]);
                }
                m_ManagerDebug.CheckInternalConsistency();

                World.Dispose();
                World = null;

                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = null;
            }

#if UNITY_DOTSPLAYER
            // TODO https://unity3d.atlassian.net/browse/DOTSR-119
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.FreeTempMemory();
#endif

            // Restore output
            var standardOutput = new System.IO.StreamWriter(System.Console.OpenStandardOutput()) { AutoFlush = true };
            System.Console.SetOut(standardOutput);
        }
    }
}
