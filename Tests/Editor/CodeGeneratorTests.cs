using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Semantic.Traits;
using Unity.AI.Planner.Utility;
using Unity.Semantic.Traits.Utility;
using UnityEditor.AI.Planner.CodeGen;
using UnityEditor.AI.Planner.Utility;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;
using TraitsCodeGenerator = UnityEditor.Semantic.Traits.CodeGen.CodeGenerator;

namespace UnityEditor.AI.Planner.Tests
{
    #if false
    class CodeGeneratorTestFixture
    {
        static readonly string k_AssetsPath = Path.Combine("Assets", "Temp");
        static readonly string k_TraitAssetsPath = Path.Combine(k_AssetsPath, "Traits");
        static readonly string k_EnumAssetsPath = Path.Combine(k_AssetsPath, "Enums");
        protected static readonly string k_OutputPath = Path.Combine("Temp", "TestPlannerAssembly");

        protected CodeGenerator m_CodeGenerator = new CodeGenerator();
        protected TraitsCodeGenerator m_TraitsCodeGenerator = new TraitsCodeGenerator();

        protected TraitDefinition m_TraitDefinition;
        protected EnumDefinition m_EnumDefinition;

        ActionDefinition m_ActionDefinition;
        ProblemDefinition m_ProblemDefinition;
        StateTerminationDefinition m_StateTerminationDefinition;

        [OneTimeSetUp]
        public virtual void Setup()
        {
            m_TraitDefinition = DynamicStruct.Create<TraitDefinition>();
            m_TraitDefinition.CreateProperty<int>("FieldA");
            SaveAsset(m_TraitDefinition, Path.Combine(k_TraitAssetsPath, "TraitA.asset"));

            m_EnumDefinition = ScriptableObject.CreateInstance<EnumDefinition>();
            m_EnumDefinition.CreateProperty<string>("ValueA");
            m_EnumDefinition.CreateProperty<string>("ValueB");
            m_EnumDefinition.CreateProperty<string>("ValueC");
            SaveAsset(m_EnumDefinition, Path.Combine(k_EnumAssetsPath, "EnumA.asset"));

            SetupTerminationDefinition("TerminationA.asset");

            SetupActionDefinition("ActionA.asset");

            m_ProblemDefinition = ScriptableObject.CreateInstance<ProblemDefinition>();
            m_ProblemDefinition.ActionDefinitions = new[]
            {
                m_ActionDefinition
            };
            m_ProblemDefinition.StateTerminationDefinitions = new[]
            {
                m_StateTerminationDefinition
            };

            SaveAsset(m_ProblemDefinition, Path.Combine(k_AssetsPath, "PlanA.asset"));

            PlannerAssetDatabase.Refresh(new []{  Path.Combine("Assets", "Temp") });
        }

        void SetupTerminationDefinition(string name)
        {
            m_StateTerminationDefinition = ScriptableObject.CreateInstance<StateTerminationDefinition>();
            m_StateTerminationDefinition.Parameters = new[]
            {
                new ParameterDefinition()
                {
                    Name = "ParameterA",
                    RequiredTraits = new[]
                    {
                        m_TraitDefinition
                    }
                }
            };

            m_StateTerminationDefinition.Criteria = new[]
            {
                new Operation()
                {
                    Operator = nameof(Operation.SpecialOperators.Custom),
                    CustomOperatorType = "CustomTerminationPrecondition"
                }
            };

            SaveAsset(m_StateTerminationDefinition, Path.Combine(k_AssetsPath, name));
        }

        void SetupActionDefinition(string name)
        {
            m_ActionDefinition = ScriptableObject.CreateInstance<ActionDefinition>();
            m_ActionDefinition.Parameters = new[]
            {
                new ParameterDefinition()
                {
                    Name = "ParameterA",
                    RequiredTraits = new[]
                    {
                        m_TraitDefinition
                    }
                }
            };
            m_ActionDefinition.CustomRewards = new List<CustomRewardData>();

            m_ActionDefinition.Preconditions = new[]
            {
                new Operation()
                {
                    Operator = nameof(Operation.SpecialOperators.Custom),
                    CustomOperatorType = "CustomActionPrecondition"
                }
            };

            m_ActionDefinition.ObjectModifiers = new[]
            {
                new Operation()
                {
                    Operator = nameof(Operation.SpecialOperators.Custom),
                    CustomOperatorType = "CustomActionEffect"
                }
            };

            m_ActionDefinition.CustomRewards = new[]
            {
                new CustomRewardData()
                {
                    Operator = "+=",
                    Typename = "CustomActionReward",
                    Parameters = new string[0]
                }
            };

            SaveAsset(m_ActionDefinition, Path.Combine(k_AssetsPath, name));
        }

        void SaveAsset(Object asset, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
        }

        [OneTimeTearDown]
        public virtual void TearDown()
        {
            CleanupFiles();
            AssetDatabase.Refresh();
            PlannerAssetDatabase.Refresh();
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
            m_CodeGenerator.GenerateStateRepresentation(k_OutputPath);
            Assert.IsTrue(File.Exists(Path.Combine(k_OutputPath, TypeHelper.StateRepresentationQualifier, "Traits", "TraitA.cs")));
        }

        [Test]
        public void EnumIsGenerated()
        {
            m_TraitsCodeGenerator.Generate(k_OutputPath, m_EnumDefinition);
            m_CodeGenerator.GenerateStateRepresentation(k_OutputPath);
            Assert.IsTrue(File.Exists(Path.Combine(k_OutputPath, TypeResolver.TraitsQualifier, "Traits", "EnumA.cs")));
        }

        [Test, Order(1)]
        public void StateRepresentationAssemblyIsCompiled()
        {
            var paths = new List<string>();
            paths.AddRange(m_TraitsCodeGenerator.Generate(k_OutputPath, m_EnumDefinition));
            paths.AddRange(m_TraitsCodeGenerator.Generate(k_OutputPath, m_TraitDefinition));

            paths.AddRange(m_CodeGenerator.GenerateStateRepresentation(k_OutputPath));
            Assert.IsTrue(File.Exists(Path.Combine(k_OutputPath, TypeHelper.StateRepresentationQualifier, "PlanA", "PlanStateRepresentation.cs")));

            var stateRepresentationAssemblyPath = Path.Combine(k_OutputPath, $"{TypeHelper.StateRepresentationQualifier}.dll");
            Assert.IsTrue(PlannerAssemblyBuilder.BuildAssembly(paths.ToArray(), stateRepresentationAssemblyPath));
        }

        [Test, Order(2)]
        public void PlansAssemblyIsCompiled()
        {
            var stateRepresentationAssemblyPath = Path.Combine(k_OutputPath, $"{TypeHelper.StateRepresentationQualifier}.dll");

            // Reference .cs files from Unity.AI.Planner.Editor.CustomMethods.Tests assembly definition
            var assemblyReference = AssetDatabase.FindAssets($"t: {nameof(AssemblyDefinitionAsset)}");
            var paths = new List<string>();
            foreach (var asmRefGuid in assemblyReference)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(asmRefGuid);
                if (assetPath.Contains("Unity.AI.Planner.Editor.Tests.CustomMethods.asmdef"))
                {
                    PlannerAssemblyBuilder.ReferenceSourceFromAssetPath(assetPath, paths);
                    break;
                }
            }

            var additionalReferences = new List<string>();
            additionalReferences.Add(stateRepresentationAssemblyPath);

            var customAssemblyPath = Path.Combine(k_OutputPath, $"{TypeHelper.CustomAssemblyName}.dll");
            Assert.IsTrue(PlannerAssemblyBuilder.BuildAssembly(paths.ToArray(), customAssemblyPath,
                additionalReferences: additionalReferences.ToArray()));

            // Pre-load referenced assemblies in the reflection context
            System.Reflection.Assembly.ReflectionOnlyLoadFrom(stateRepresentationAssemblyPath);
            foreach (var reference in PlannerAssemblyBuilder.predefinedReferences)
            {
                System.Reflection.Assembly.ReflectionOnlyLoadFrom(reference);
            }

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += PlannerAssemblyBuilder.ResolveReflectionAssemblyDependency;
            var customAssembly = System.Reflection.Assembly.ReflectionOnlyLoadFrom(customAssemblyPath);

            additionalReferences.Add(customAssemblyPath);
            paths = m_CodeGenerator.GeneratePlans(k_OutputPath, customAssembly);

            Assert.IsTrue(File.Exists(Path.Combine(k_OutputPath, TypeHelper.PlansQualifier, "PlanA", "ActionScheduler.cs")));
            Assert.IsTrue(File.Exists(Path.Combine(k_OutputPath, TypeHelper.PlansQualifier, "PlanA", "ActionA.cs")));
            Assert.IsTrue(File.Exists(Path.Combine(k_OutputPath, TypeHelper.PlansQualifier, "PlanA", "TerminationA.cs")));

            Assert.IsTrue(PlannerAssemblyBuilder.BuildAssembly(paths.ToArray(), Path.Combine(k_OutputPath, $"{TypeHelper.PlansQualifier}.dll"),
                additionalReferences: additionalReferences.ToArray()));
        }
    }
    #endif
}
