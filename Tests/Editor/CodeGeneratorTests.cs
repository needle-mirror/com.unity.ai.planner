using System.Data;
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
        protected AgentDefinition m_AgentDefinition;

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
            SaveAsset(m_ActionDefinition, $"{k_AssetsPath}ActionA.asset");

            m_AgentDefinition = ScriptableObject.CreateInstance<AgentDefinition>();
            m_AgentDefinition.ActionDefinitions = new[]
            {
                m_ActionDefinition
            };
            SaveAsset(m_AgentDefinition, $"{k_AssetsPath}AgentA.asset");
        }

        private void SaveAsset(Object asset, string path)
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
            m_CodeGenerator.GenerateDomain(k_OutputPath, k_TraitAssetsPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Domains/Traits/TraitA.cs"));
        }

        [Test]
        public void EnumIsGenerated()
        {
            m_CodeGenerator.GenerateDomain(k_OutputPath, k_EnumAssetsPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Domains/Traits/EnumA.cs"));
        }

        [Test]
        public void PlanningDataAreGenerated()
        {
            m_CodeGenerator.GenerateDomain(k_OutputPath, k_AssetsPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Domains/PlanningDomainData.cs"));

            m_CodeGenerator.GenerateActions(k_OutputPath, k_AssetsPath);
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Actions/AgentA/ActionScheduler.cs"));
            Assert.IsTrue(File.Exists($"{k_OutputPath}AI.Planner.Actions/AgentA/ActionA.cs"));
        }

        [Test]
        public void DomainAssemblyCompilation()
        {
            var paths = m_CodeGenerator.GenerateDomain(k_OutputPath, k_AssetsPath);
            var domainAssemblyPath = $"{k_OutputPath}AI.Planner.Domains.dll";
            Assert.IsTrue(DomainAssemblyBuilder.BuildAssembly(paths.ToArray(), domainAssemblyPath));
            paths = m_CodeGenerator.GenerateActions(k_OutputPath, k_AssetsPath);
            Assert.IsTrue(DomainAssemblyBuilder.BuildAssembly(paths.ToArray(), $"{k_OutputPath}AI.Planner.Actions.dll",
                additionalReferences: new [] { domainAssemblyPath }));
        }
    }
}
