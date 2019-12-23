using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.AI.Planner.CodeGen;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Tests
{
    class CodeGeneratorTestFixture
    {
        protected const string k_AssetsPath = "Assets/Temp/";
        protected const string k_TraitAssetsPath = k_AssetsPath + "Traits/";
        protected const string k_EnumAssetsPath = k_AssetsPath + "Enums/";

        protected const string k_OutputPath = "Temp/TestPlannerAssembly/";

        protected TraitDefinition m_TraitDefinition;
        protected EnumDefinition m_EnumDefinition;
        protected ActionDefinition m_ActionDefinition;
        protected PlanDefinition m_PlanDefinition;
        protected StateTerminationDefinition m_StateTerminationDefinition;

        protected CodeGenerator m_CodeGenerator = new CodeGenerator();

        [OneTimeSetUp]
        public virtual void Setup()
        {
            m_TraitDefinition = ScriptableObject.CreateInstance<TraitDefinition>();
            m_TraitDefinition.Fields = new[]
            {
                new TraitDefinitionField()
                {
                    Name = "FieldA",
                    FieldType = typeof(int)
                }
            };
            SaveAsset(m_TraitDefinition, $"{k_TraitAssetsPath}TraitA.asset");

            m_EnumDefinition = ScriptableObject.CreateInstance<EnumDefinition>();
            m_EnumDefinition.Values = new[]
            {
                "ValueA",
                "ValueB",
                "ValueC"
            };
            SaveAsset(m_EnumDefinition, $"{k_EnumAssetsPath}EnumA.asset");

            m_StateTerminationDefinition = ScriptableObject.CreateInstance<StateTerminationDefinition>();
            m_StateTerminationDefinition.Parameters = new[]
            {
                new ParameterDefinition()
                {
                    Name = "ParameterA",
                    RequiredTraits = new []
                    {
                        m_TraitDefinition
                    }
                }
            };
            SaveAsset(m_StateTerminationDefinition, $"{k_AssetsPath}TerminationA.asset");

            m_ActionDefinition = ScriptableObject.CreateInstance<ActionDefinition>();
            m_ActionDefinition.Parameters = new[]
            {
                new ParameterDefinition()
                {
                    Name = "ParameterA",
                    RequiredTraits = new []
                    {
                        m_TraitDefinition
                    }
                }
            };
            m_ActionDefinition.CustomRewards = new List<CustomRewardData>();
            SaveAsset(m_ActionDefinition, $"{k_AssetsPath}ActionA.asset");

            m_PlanDefinition = ScriptableObject.CreateInstance<PlanDefinition>();
            m_PlanDefinition.ActionDefinitions = new[]
            {
                m_ActionDefinition
            };
            m_PlanDefinition.StateTerminationDefinitions = new List<StateTerminationDefinition>();

            SaveAsset(m_PlanDefinition, $"{k_AssetsPath}PlanA.asset");
        }

        void SaveAsset(Object asset, string path)
        {
            Debug.Log(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
        }

        [OneTimeTearDown]
        public virtual void TearDown()
        {
            CleanupFiles();
            AssetDatabase.Refresh();
        }

        static void CleanupFiles()
        {
            if (Directory.Exists(k_AssetsPath))
                Directory.Delete(k_AssetsPath, true);

            if (Directory.Exists(k_OutputPath))
                Directory.Delete(k_OutputPath, true);
        }
    }

    [TestFixture]
    class CodeGeneratorTests : CodeGeneratorTestFixture
    {
        [Test]
        public void TraitIsGenerated()
        {
            m_CodeGenerator.GenerateDomain(k_OutputPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Domains/Traits/TraitA.cs"));
        }

        [Test]
        public void EnumIsGenerated()
        {
            m_CodeGenerator.GenerateDomain(k_OutputPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Domains/Traits/EnumA.cs"));
        }


        [Test]
        public void StateTerminationIsGenerated()
        {
            m_CodeGenerator.GenerateDomain(k_OutputPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Domains/TerminationA.cs"));
        }

        [Test]
        public void PlanningDataAreGenerated()
        {
            m_CodeGenerator.GenerateDomain(k_OutputPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Domains/PlanningDomainData.cs"));

            m_CodeGenerator.GeneratePlans(k_OutputPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Actions/PlanA/ActionScheduler.cs"));
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Actions/PlanA/ActionA.cs"));
        }

        [Test]
        public void PlannerAssemblyCompilation()
        {
            var paths = m_CodeGenerator.GenerateDomain(k_OutputPath);
            var domainAssemblyPath = $"{k_OutputPath}AI.Planner.Domains.dll";
            Assert.IsTrue(PlannerAssemblyBuilder.BuildAssembly(paths.ToArray(), domainAssemblyPath));
            paths = m_CodeGenerator.GeneratePlans(k_OutputPath);
            Assert.IsTrue(PlannerAssemblyBuilder.BuildAssembly(paths.ToArray(), $"{k_OutputPath}AI.Planner.Actions.dll",
                additionalReferences: new [] { domainAssemblyPath }));
        }
    }
}
