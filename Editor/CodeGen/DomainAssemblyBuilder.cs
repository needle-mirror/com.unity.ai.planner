using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.AI.Planner.Utility;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.CodeGen
{
    class DomainAssemblyBuilder
    {
        const string k_DomainsAssemblyName = "AI.Planner.Domains.dll";
        const string k_ActionsAssemblyName = "AI.Planner.Actions.dll";

        internal const string k_CustomAssemblyReference = "Unity.AI.Planner.Custom";
        internal const string k_OutputDomainsAssembly = k_OutputPath + k_DomainsAssemblyName;
        internal const string k_OutputActionsAssembly = k_OutputPath + k_ActionsAssemblyName;
        internal const string k_OutputPath = "Temp/PlannerAssembly/";
        internal const string k_PlannerProjectPath = "Assets/AI.Planner/";
        internal const string k_DomainsAssemblyProjectPath = k_PlannerProjectPath + k_DomainsAssemblyName;
        internal const string k_ActionsAssemblyProjectPath = k_PlannerProjectPath + k_ActionsAssemblyName;

        [InitializeOnLoadMethod]
        public static void AttachAutoBuild()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange playMode)
        {
            if (playMode == PlayModeStateChange.ExitingEditMode)
            {
                var lastBuildTime = File.GetLastWriteTimeUtc(k_DomainsAssemblyProjectPath);

                var assetTypes = new[]
                {
                    nameof(TraitDefinition),
                    nameof(EnumDefinition),
                    nameof(ActionDefinition),
                    nameof(StateTerminationDefinition),
                    nameof(AgentDefinition),
                };

                var filter = string.Join(" ", assetTypes.Select(t => $"t:{t}"));
                var assets = AssetDatabase.FindAssets(filter);
                foreach (var a in assets)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(a);
                    var assetLastWriteTime = File.GetLastWriteTimeUtc(assetPath);
                    if (assetLastWriteTime.CompareTo(lastBuildTime) > 0)
                    {
                        Debug.Log($"Rebuilding AI Planner assemblies because {assetPath} is newer");
                        BuildDomainAssemblies();
                        break;
                    }
                }
            }
        }

        [MenuItem("AI/Build Assemblies")]
        public static void BuildDomainAssemblies()
        {
            var codeGenerator = new CodeGenerator();
            var paths = codeGenerator.GenerateDomain(k_OutputPath, "Assets/").ToList();

            // Build domains assembly first, since others depend on it
            var domainAssemblyBuilt = BuildAssembly(paths.ToArray(), k_OutputDomainsAssembly,
                new[] { k_DomainsAssemblyProjectPath });

            var actionsNamespace = TypeResolver.ActionsNamespace;
            var additionalReferences = new List<string>();
            additionalReferences.Add(k_OutputDomainsAssembly);
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            // Collect any custom user assemblies
            foreach (var assembly in assemblies)
            {
                if (assembly.name.Contains("CSharp") || assembly.name == actionsNamespace)
                    continue;

                var assemblyReferences = assembly.assemblyReferences;
                if (assemblyReferences.Any(a => a.name == k_CustomAssemblyReference))
                {
                    if (BuildAssembly(assembly.sourceFiles, assembly.outputPath,
                        excludeReferences: new [] { assembly.outputPath },
                        additionalReferences: new[] { k_OutputDomainsAssembly },
                        additionalDefines: new[] { "PLANNER_DOMAIN_GENERATED", "PLANNER_CUSTOM" }))
                    {
                        additionalReferences.Add(assembly.outputPath);
                    }
                }
            }

            paths = codeGenerator.GenerateActions(k_OutputPath, "Assets/").ToList();

            var actionsAssemblyBuilt = BuildAssembly(paths.ToArray(), k_OutputActionsAssembly,
                new[] { k_ActionsAssemblyProjectPath }, additionalReferences.ToArray());

            if (domainAssemblyBuilt && actionsAssemblyBuilt)
            {
                if (Directory.Exists($"{k_PlannerProjectPath}Generated/"))
                {
                    Directory.Delete($"{k_PlannerProjectPath}Generated/", true);
                    AssetDatabase.Refresh();
                }

                File.Copy(k_OutputDomainsAssembly, k_DomainsAssemblyProjectPath, true);
                File.Copy(k_OutputActionsAssembly, k_ActionsAssemblyProjectPath, true);
                AssetDatabase.ImportAsset(k_DomainsAssemblyProjectPath);
                AssetDatabase.ImportAsset(k_ActionsAssemblyProjectPath);
            }
        }

        internal static bool BuildAssembly(string[] paths, string outputPath, string[] excludeReferences = null,
            string[] additionalReferences = null, string[] additionalDefines = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var predefinedReferences = new[]
            {
                "Library/ScriptAssemblies/Unity.Collections.dll",
                "Library/ScriptAssemblies/Unity.Entities.dll",
                "Library/ScriptAssemblies/Unity.Properties.dll",
                "Library/ScriptAssemblies/Unity.Jobs.dll",
                "Library/ScriptAssemblies/Unity.AI.Planner.dll"
            };

            additionalReferences = additionalReferences == null ? predefinedReferences : predefinedReferences.Concat(additionalReferences).ToArray();
            var assemblyBuilder = new AssemblyBuilder(outputPath, paths)
            {
                additionalDefines = additionalDefines,
                excludeReferences = excludeReferences,
                referencesOptions = ReferencesOptions.UseEngineModules,
                additionalReferences = additionalReferences,
            };

            assemblyBuilder.buildStarted += delegate(string assemblyPath)
            {
                Debug.Log($"Assembly build started for {assemblyPath}");
            };

            bool buildSucceed = false;
            assemblyBuilder.buildFinished += delegate(string assemblyPath, CompilerMessage[] compilerMessages)
            {
                foreach (var m in compilerMessages)
                {
                    switch (m.type)
                    {
                        case CompilerMessageType.Warning:
                            Debug.LogWarning(m.message);
                            break;
                        case CompilerMessageType.Error:
                            Debug.LogError(m.message);
                            break;
                        default:
                            Debug.Log(m.message);
                            break;
                    }
                }

                var errorCount = compilerMessages.Count(m => m.type == CompilerMessageType.Error);
                var warningCount = compilerMessages.Count(m => m.type == CompilerMessageType.Warning);

                Debug.Log($"Assembly build finished for {assemblyPath}");
                Debug.Log($"Warnings: {warningCount} - Errors: {errorCount}");

                if (errorCount != 0)
                    return;

                buildSucceed = true;
            };

            if (!assemblyBuilder.Build())
            {
                Debug.LogErrorFormat("Failed to start build of assembly {0}!", assemblyBuilder.assemblyPath);
                return false;
            }

            while (assemblyBuilder.status != AssemblyBuilderStatus.Finished)
                Thread.Sleep(10);

            return buildSucceed;
        }
    }
}
