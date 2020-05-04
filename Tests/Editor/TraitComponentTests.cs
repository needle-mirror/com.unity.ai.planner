using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.TestTools;

namespace UnityEditor.AI.Planner.Tests
{
    public class TraitComponentTests
    {
        const string k_LocationAssetPath = "Packages/com.unity.ai.planner/Runtime/Modules/Navigation/Location.asset";

        TraitDefinition m_LocationDefinition;

        [OneTimeSetUp]
        public void SetUp()
        {
            m_LocationDefinition = AssetDatabase.LoadAssetAtPath<TraitDefinition>(k_LocationAssetPath);
        }

        [UnityTest]
        public IEnumerator CanAddTrait()
        {
            yield return new EnterPlayMode();
            var traitObject = new GameObject("TraitObject");

            var traitComponent = traitObject.AddComponent<TraitComponent>();
            traitComponent.AddDefaultTrait(m_LocationDefinition);

            Assert.IsTrue(traitComponent.HasTraitData<Location>());

            Object.DestroyImmediate(traitObject);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CanRemoveTrait()
        {
            yield return new EnterPlayMode();
            var traitObject = new GameObject("TraitObject");

            var traitComponent = traitObject.AddComponent<TraitComponent>();
            traitComponent.AddDefaultTrait(m_LocationDefinition);
            traitComponent.RemoveTraitData<Location>();

            Assert.IsFalse(traitComponent.HasTraitData<Location>());

            Object.DestroyImmediate(traitObject);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CanModifyTrait()
        {
            yield return new EnterPlayMode();
            var traitObject = new GameObject("TraitObject");

            var traitComponent = traitObject.AddComponent<TraitComponent>();
            traitComponent.AddDefaultTrait(m_LocationDefinition);

            var locationTrait = traitComponent.GetTraitData<Location>();
            locationTrait.SetValue("Transform", traitObject.transform);

            var transformValue = (Transform)locationTrait.GetValue("Transform");
            Assert.AreSame(transformValue, traitObject.transform);

            Object.DestroyImmediate(traitObject);
            yield return new ExitPlayMode();
        }
    }
}
