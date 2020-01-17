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
    class PlannerAssemblyBuilder
    {
        const string k_BuildMenuTitle = "AI/Planner/Generate assemblies";

        const string k_DomainsAssemblyName = "AI.Planner.Domains.dll";
        const string k_ActionsAssemblyName = "AI.Planner.Actions.dll";

        const string k_CustomAssemblyReference = "Unity.AI.Planner.Custom";

        const string k_TempOutputPath = "Temp/PlannerAssembly/";
        const string k_TempOutputDomainsAssembly = k_TempOutputPath + k_DomainsAssemblyName;
        const string k_TempOutputActionsAssembly = k_TempOutputPath + k_ActionsAssemblyName;

        const string k_GeneratedProjectPath = "Packages";
        static readonly string k_GeneratedPackagesPath = Path.Combine(k_GeneratedProjectPath, "com.");

        [InitializeOnLoadMethod]
        public static void AttachAutoBuild()
        {
            if (!Application.isBatchMode)
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange playMode)
        {
            if (AIPlannerPreferences.GetOrCreatePreferences().AutoCompile)
            {
                if (playMode == PlayModeStateChange.EnteredPlayMode && EditorApplication.isCompiling)
                {
                    // If we're still compiling the domain will reload and cause an error, so as a safeguard simply exit play mode
                    EditorApplication.ExitPlaymode();
                    return;
                }

                if (playMode == PlayModeStateChange.ExitingEditMode)
                {
                    var actionsAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).FirstOrDefault(a => a.name == TypeResolver.ActionsNamespace);
                    DateTime lastBuildTime = DateTime.MinValue;
                    if (actionsAssembly != null)
                        lastBuildTime = File.GetLastWriteTimeUtc(actionsAssembly.outputPath);

                    var assetTypes = new[]
                    {
                        nameof(TraitDefinition),
                        nameof(EnumDefinition),
                        nameof(ActionDefinition),
                        nameof(StateTerminationDefinition),
                        nameof(PlanDefinition),
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
                            EditorApplication.ExitPlaymode();
                            Build();
                            CompilationPipeline.compilationFinished += context =>  EditorApplication.EnterPlaymode();
                            break;
                        }
                    }
                }
            }
        }

        [MenuItem(k_BuildMenuTitle, true)]
        public static bool BuildMenuValidate()
        {
            return !EditorApplication.isCompiling;
        }

        [MenuItem(k_BuildMenuTitle)]
        public static void Build()
        {
            var codeGenerator = new CodeGenerator();

            var domainsNamespace = TypeResolver.DomainsNamespace;
            var domainsAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).FirstOrDefault(a => a.name == domainsNamespace);

            var excludeReferences = new List<Assembly>();
            if (domainsAssembly != null)
                excludeReferences.Add(domainsAssembly);

            // Build domains assembly first, since others depend on it
            var domainPaths = codeGenerator.GenerateDomain(k_TempOutputPath);
            var domainAssemblyBuilt = BuildAssembly(domainPaths.ToArray(), k_TempOutputDomainsAssembly,
                excludeReferences.Select(a => a.outputPath).ToArray());

            if (domainAssemblyBuilt)
            {
                // Now build actions assembly, which will depend on the domains assembly
                var actionsNamespace = TypeResolver.ActionsNamespace;
                var actionsAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).FirstOrDefault(a => a.name == actionsNamespace);
                if (actionsAssembly != null)
                    excludeReferences.Add(actionsAssembly);

                var planPaths = codeGenerator.GeneratePlans(k_TempOutputPath);

                var dependentAssemblies = new List<string>();
                var additionalReferences = new List<string>();
                additionalReferences.Add(k_TempOutputDomainsAssembly);

                // Collect any custom user assemblies
                var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
                foreach (var assembly in assemblies)
                {
                    if (assembly.name.Contains("CSharp") || assembly.name == actionsNamespace)
                        continue;

                    var assemblyReferences = assembly.assemblyReferences;
                    if (assemblyReferences.Any(a => a.name == k_CustomAssemblyReference))
                    {
                        additionalReferences.Add(assembly.outputPath);
                        dependentAssemblies.Add(assembly.name);
                    }
                }

                var actionsAssemblyBuilt = BuildAssembly(planPaths.ToArray(), k_TempOutputActionsAssembly,
                    excludeReferences.Select(a => a.outputPath).ToArray(), additionalReferences.ToArray());

                var generatedDomainsPath = $"{k_GeneratedPackagesPath}{domainsNamespace.ToLower()}.generated";
                if (actionsAssemblyBuilt || !Directory.Exists(generatedDomainsPath))
                {
                    // Copy generated files for the domain over to the packages folder
                    var domainsSourceDir = Path.Combine(generatedDomainsPath, TypeResolver.DomainsNamespace);
                    if (Directory.Exists(domainsSourceDir))
                    {
                        Directory.Delete(domainsSourceDir, true);
                        File.Delete($"{domainsSourceDir}.asmdef");
                    }
                    Directory.CreateDirectory(generatedDomainsPath);

                    var codeRenderer = new CodeRenderer();
                    foreach (string file in domainPaths)
                    {
                        var newFilePath = file.Replace(k_TempOutputPath, generatedDomainsPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                        File.Copy(file, newFilePath, true);
                    }
                    File.WriteAllText($"{Path.Combine(generatedDomainsPath, domainsNamespace)}.asmdef", codeRenderer.RenderTemplate(
                        PlannerResources.instance.TemplateDomainsAsmDef, new
                        {
                            @namespace = domainsNamespace
                        }));
                    File.WriteAllText(Path.Combine(generatedDomainsPath,"package.json"), codeRenderer.RenderTemplate(
                        PlannerResources.instance.TemplatePackage, new
                        {
                            assembly = domainsNamespace.Split('.').Last()
                        }));

                    if (actionsAssemblyBuilt)
                    {
                        // Copy generated files for the action over to the packages folder
                        var generatedActionsPath = $"{k_GeneratedPackagesPath}{actionsNamespace.ToLower()}.generated";
                        var actionsSourceDir = $"{Path.Combine(generatedActionsPath, TypeResolver.ActionsNamespace)}";
                        if (Directory.Exists(actionsSourceDir))
                        {
                            Directory.Delete(actionsSourceDir, true);
                            File.Delete($"{actionsSourceDir}.asmdef");
                        }
                        Directory.CreateDirectory(generatedActionsPath);

                        foreach (string file in planPaths)
                        {
                            var newFilePath = file.Replace(k_TempOutputPath, generatedActionsPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                            File.Copy(file, newFilePath, true);
                        }

                        File.WriteAllText($"{Path.Combine(generatedActionsPath, actionsNamespace)}.asmdef", codeRenderer.RenderTemplate(
                            PlannerResources.instance.TemplateActionsAsmDef, new
                            {
                                @namespace = actionsNamespace,
                                domains_namespace = domainsNamespace,
                                additional_references = dependentAssemblies
                            }));

                        File.WriteAllText(Path.Combine(generatedActionsPath, "package.json"), codeRenderer.RenderTemplate(
                            PlannerResources.instance.TemplatePackage, new
                            {
                                assembly = actionsNamespace.Split('.').Last()
                            }));
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
            }
        }

        internal static bool BuildAssembly(string[] paths, string outputPath, string[] excludeReferences = null,
            string[] additionalReferences = null, string[] additionalDefines = null)
        {
            if (paths == null || paths.Length == 0)
                return false;

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

                if (warningCount > 0 || errorCount > 0)
                    Debug.Log($"Assembly build finished for {assemblyPath} -- Warnings: {warningCount} - Errors: {errorCount}");

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
