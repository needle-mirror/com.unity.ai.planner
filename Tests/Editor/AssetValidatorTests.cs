using NUnit.Framework;
using UnityEditor.AI.Planner.CodeGen;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.TestTools;

namespace UnityEditor.AI.Planner.Tests
{
    class AssetValidatorTestFixture
    {
        protected AssetValidator m_AssetValidator = new AssetValidator();
    }

    [TestFixture]
    class AssetValidatorTests : AssetValidatorTestFixture
    {
        [Test]
        public void ActionDuplicatedNameChecked()
        {
            var actionDefinition = ScriptableObject.CreateInstance<ActionDefinition>();
            actionDefinition.Parameters = new[]
            {
                new ParameterDefinition() { Name = "ObjectNameA" },
                new ParameterDefinition() { Name = "ObjectNameA" }
            };

            Assert.IsFalse(m_AssetValidator.IsActionAssetValid(actionDefinition));

            actionDefinition = ScriptableObject.CreateInstance<ActionDefinition>();
            actionDefinition.Parameters = new[]
            {
                new ParameterDefinition() { Name = "ObjectNameA" },
            };

            actionDefinition.CreatedObjects = new[]
            {
                new ParameterDefinition() { Name = "ObjectNameA" },
            };

            Assert.IsFalse(m_AssetValidator.IsActionAssetValid(actionDefinition));
        }

        [Test]
        public void ActionInvalidComparerChecked()
        {
            LogAssert.ignoreFailingMessages = true;
            var actionDefinition = ScriptableObject.CreateInstance<ActionDefinition>();
            actionDefinition.Parameters = new[]
            {
                new ParameterDefinition()
                {
                    Name = "ParameterA",
                    LimitComparerType = "UnknownType"
                },
            };

            Assert.IsFalse(m_AssetValidator.IsActionAssetValid(actionDefinition));
        }

        [Test]
        public void ActionInvalidCustomRewardChecked()
        {
            var actionDefinition = ScriptableObject.CreateInstance<ActionDefinition>();
            actionDefinition.CustomRewards = new[]
            {
                new CustomRewardData()
                {
                    Typename = "UnknownType"
                },
            };

            Assert.IsFalse(m_AssetValidator.IsActionAssetValid(actionDefinition));
        }
    }
}
