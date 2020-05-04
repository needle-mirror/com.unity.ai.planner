using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.AI.Planner.Utility;
using UnityEditor.AI.Planner.Utility;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;

namespace UnityEditor.AI.Planner.CodeGen
{
    class PlannerAssemblyBuilder
    {
        const string k_TitleBuildPopup = "Build Planner";

        const string k_BuildMenuTitle = "AI/Planner/Generate assemblies";

        const string k_StateRepresentationAssemblyFileName = TypeResolver.StateRepresentationQualifier + ".dll";
        const string k_PlansAssemblyFileName =  TypeResolver.PlansQualifier + ".dll";
        const string k_CustomCodeAssemblyFileName =  TypeResolver.CustomAssemblyName + ".dll";

        static readonly string k_TempOutputPath = Path.Combine("Temp", "PlannerAssembly");
        static readonly string k_TempOutputStateRepresentationAssembly = Path.Combine(k_TempOutputPath, k_StateRepresentationAssemblyFileName);
        static readonly string k_TempOutputActionsAssembly = Path.Combine(k_TempOutputPath, k_PlansAssemblyFileName);
        static readonly string k_TempOutputCustomCodeAssembly = Path.Combine(k_TempOutputPath, k_CustomCodeAssemblyFileName);

        internal static readonly string[] predefinedReferences = {
                "Library/ScriptAssemblies/Unity.Collections.dll",
                "Library/ScriptAssemblies/Unity.Entities.dll",
                "Library/ScriptAssemblies/Unity.Properties.dll",
                "Library/ScriptAssemblies/Unity.Jobs.dll",
                "Library/ScriptAssemblies/Unity.AI.Planner.dll"
            };

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

                if (playMode == PlayModeStateChange.ExitingEditMode && !EditorApplication.isCompiling)
                {
                    if (!PlannerAssetDatabase.HasValidPlanDefinition())
                        return;

                    var assetChangedPath = string.Empty;

                    bool stateRepresentationNeedBuild = !PlannerAssetDatabase.StateRepresentationPackageExists();
                    if (!stateRepresentationNeedBuild)
                    {
                        DateTime lastStateRepresentationBuildTime = GetAssemblyBuildTime(TypeResolver.StateRepresentationQualifier);

                        stateRepresentationNeedBuild = PlannerAssetDatabase.TryFindNewerAsset(PlannerAssetDatabase.stateRepresentationAssetTypeNames, lastStateRepresentationBuildTime, ref assetChangedPath);
                        if (stateRepresentationNeedBuild)
                            Debug.Log($"Rebuilding AI Planner State Representation assembly because {assetChangedPath} is newer");
                    }
                    else
                    {
                        Debug.Log($"Rebuilding AI Planner assemblies because AI Planner State Representation package cannot be found");
                    }

                    bool planNeedBuild = !PlannerAssetDatabase.PlansPackageExists();
                    if (!planNeedBuild)
                    {
                        DateTime lastPlanBuildTime = GetAssemblyBuildTime(TypeResolver.PlansQualifier);
                        planNeedBuild = PlannerAssetDatabase.TryFindNewerAsset(PlannerAssetDatabase.planAssetTypeNames, lastPlanBuildTime, ref assetChangedPath);
                        if (planNeedBuild)
                            Debug.Log($"Rebuilding AI Plan assembly because {assetChangedPath} is newer");
                    }
                    else
                    {
                        Debug.Log($"Rebuilding AI Planner assemblies because AI Plan package cannot be found");
                    }

                    if (stateRepresentationNeedBuild || planNeedBuild)
                    {
                        EditorApplication.ExitPlaymode();

                        try
                        {
                            if (Build(stateRepresentationNeedBuild))
                                CompilationPipeline.compilationFinished += context => EditorApplication.EnterPlaymode();
                        }
                        finally
                        {
                            EditorUtility.ClearProgressBar();
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
            try
            {
                Build(true);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static bool Build(bool rebuildStateRepresentation)
        {
            EditorUtility.DisplayProgressBar(k_TitleBuildPopup, "Refresh Planner database", 0.1f);
            PlannerAssetDatabase.Refresh();

            var codeGenerator = new CodeGenerator();
            var validator = new AssetValidator();
            validator.errorLogged += (errorMessage, asset) => Debug.LogError($"<b>{AssetDatabase.GetAssetPath(asset)}</b>: {errorMessage}");

            bool stateRepresentationAssemblyValid = true;
            if (rebuildStateRepresentation || !File.Exists(k_TempOutputStateRepresentationAssembly))
            {
                EditorUtility.DisplayProgressBar(k_TitleBuildPopup, "State Representation assembly compilation", 0.4f);
                if (!validator.CheckStateRepresentationAssetsValidity())
                {
                    Debug.LogError("All Planner asset errors have to be fixed to generate AI State Representation assembly.");
                    return false;
                }

                stateRepresentationAssemblyValid = CreateStateRepresentationPackage(codeGenerator);
            }

            if (stateRepresentationAssemblyValid)
            {
                EditorUtility.DisplayProgressBar(k_TitleBuildPopup, "Plan assembly compilation", 0.6f);

                var customAssembly = BuildCustomCodeAssembly();
                if (customAssembly == null)
                {
                    Debug.LogError("Custom code errors have to be fixed to generate AI Plans assembly.");
                    return false;
                }


                if (!validator.CheckPlansAssetsValidity(customAssembly))
                {
                    Debug.LogError("All Planner asset errors have to be fixed to generate AI Plans assembly.");
                    return false;
                }

                CreatePlansPackage(codeGenerator, customAssembly);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
                return true;
            }

            return false;
        }

        static System.Reflection.Assembly BuildCustomCodeAssembly()
        {
            var currentLoadedAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).FirstOrDefault(a => a.name == TypeResolver.CustomAssemblyName);

            var excludeReferences = new List<Assembly>();
            if (currentLoadedAssembly != null)
                excludeReferences.Add(currentLoadedAssembly);

            var customCodeFilesPath = new List<string>();
            string tempCustomDirectory = Path.Combine(k_TempOutputPath, TypeResolver.CustomAssemblyName);
            Directory.CreateDirectory(tempCustomDirectory);
            var assemblyInfoPath = Path.Combine(tempCustomDirectory, "AssemblyInfo.cs");
            File.WriteAllText(assemblyInfoPath, $"using System.Runtime.CompilerServices; [assembly: InternalsVisibleTo(\"{TypeResolver.PlansQualifier}\")]");
            customCodeFilesPath.Add(assemblyInfoPath);

            // Reference .cs files from all AsmRef pointing to AI.Planner.Custom assembly
            ReferenceSourceFromAsmRef(TypeResolver.CustomAssemblyName, customCodeFilesPath);

            // Add a reference to the previously generated StateRepresentation Assembly
            var additionalReferences = new[] { k_TempOutputStateRepresentationAssembly };

            if (BuildAssembly(customCodeFilesPath.ToArray(), k_TempOutputCustomCodeAssembly,  excludeReferences.Select(a => a.outputPath).ToArray(), additionalReferences))
            {
                // Pre-load referenced assemblies in the reflection context
                System.Reflection.Assembly.ReflectionOnlyLoadFrom(k_TempOutputStateRepresentationAssembly);
                foreach (var reference in predefinedReferences)
                {
                    System.Reflection.Assembly.ReflectionOnlyLoadFrom(reference);
                }

                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveReflectionAssemblyDependency;
                return System.Reflection.Assembly.ReflectionOnlyLoadFrom(k_TempOutputCustomCodeAssembly);
            }

            return null;
        }

        static void ReferenceSourceFromAsmRef(string assemblyName, List<string> sourcePaths)
        {
            var assemblyDefinitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);

            var assemblyReference = AssetDatabase.FindAssets($"t: {nameof(AssemblyDefinitionReferenceAsset)}");
            foreach (var asmRefGuid in assemblyReference)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(asmRefGuid);

                // Skip sources from generated package
                if (assetPath.Contains(TypeResolver.PlansQualifier.ToLower()) || assetPath.Contains(TypeResolver.StateRepresentationQualifier.ToLower()))
                    continue;

                var reference = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionReferenceAsset>(assetPath);
                if (reference.text.Contains($"reference\": \"{assemblyName}\"")
                    || reference.text.Contains($"reference\": \"GUID:{AssetDatabase.AssetPathToGUID(assemblyDefinitionPath)}\""))
                {
                    ReferenceSourceFromAssetPath(assetPath, sourcePaths);
                }
            }
        }

        internal static void ReferenceSourceFromAssetPath(string path, List<string> sourcePaths)
        {
            var dir = new DirectoryInfo(Path.Combine(Application.dataPath, $"../{Path.GetDirectoryName(path)}"));
            var info = dir.GetFiles("*.cs", SearchOption.AllDirectories);

            foreach (var file in info)
            {
                sourcePaths.Add(file.FullName);
            }
        }

        static bool CreateStateRepresentationPackage(CodeGenerator codeGenerator)
        {
            var assemblyName = TypeResolver.StateRepresentationQualifier;
            var currentLoadedAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).FirstOrDefault(a => a.name == assemblyName);

            var excludeReferences = new List<Assembly>();
            if (currentLoadedAssembly != null)
                excludeReferences.Add(currentLoadedAssembly);

            var generatedFilesPath = codeGenerator.GenerateStateRepresentation(k_TempOutputPath);

            var sourcePaths = new List<string>();
            sourcePaths.AddRange(generatedFilesPath);

            // Reference .cs files from all AsmRef pointing to Generated.AI.Planner.StateRepresentation assembly
            ReferenceSourceFromAsmRef(TypeResolver.StateRepresentationQualifier, sourcePaths);

            var assemblyBuilt = BuildAssembly(sourcePaths.ToArray(), k_TempOutputStateRepresentationAssembly, excludeReferences.Select(a => a.outputPath).ToArray());
            if (assemblyBuilt)
            {
                var generatedStateRepresentationPath = PlannerAssetDatabase.stateRepresentationPackagePath;

                // Copy generated files for the StateRepresentation over to the packages folder
                var sourceDir = Path.Combine(generatedStateRepresentationPath, TypeResolver.StateRepresentationQualifier);
                if (Directory.Exists(sourceDir))
                {
                    Directory.Delete(sourceDir, true);
                    File.Delete($"{sourceDir}.asmref");
                }
                Directory.CreateDirectory(generatedStateRepresentationPath);

                foreach (string file in generatedFilesPath)
                {
                    var newFilePath = file.Replace(k_TempOutputPath, generatedStateRepresentationPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                    File.Copy(file, newFilePath, true);
                }
                codeGenerator.GenerateAsmRef(generatedStateRepresentationPath, TypeResolver.StateRepresentationQualifier);
                codeGenerator.GeneratePackage(generatedStateRepresentationPath, TypeResolver.StateRepresentationQualifier);
            }

            return assemblyBuilt;
        }

        static void CreatePlansPackage(CodeGenerator codeGenerator, System.Reflection.Assembly customAssembly)
        {
            var assemblyName = TypeResolver.PlansQualifier;
            var currentLoadedAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).FirstOrDefault(a => a.name == assemblyName);

            var excludeReferences = new List<Assembly>();
            if (currentLoadedAssembly != null)
                excludeReferences.Add(currentLoadedAssembly);

            var generatedFilesPath = codeGenerator.GeneratePlans(k_TempOutputPath, customAssembly);

            var sourcePaths = new List<string>();
            sourcePaths.AddRange(generatedFilesPath);

            // Reference .cs files from all AsmRef pointing to Generated.AI.Planner.Plans assembly
            ReferenceSourceFromAsmRef(TypeResolver.PlansQualifier, sourcePaths);

            var additionalReferences = new List<string>();
            additionalReferences.Add(k_TempOutputStateRepresentationAssembly);
            additionalReferences.Add(k_TempOutputCustomCodeAssembly);

            var assemblyBuilt = BuildAssembly(sourcePaths.ToArray(), k_TempOutputActionsAssembly,
                excludeReferences.Select(a => a.outputPath).ToArray(), additionalReferences.ToArray());

            if (assemblyBuilt)
            {
                // Copy generated files for the action over to the packages folder
                var generatedPlansPath = PlannerAssetDatabase.plansPackagePath;
                var actionsSourceDir = $"{Path.Combine(generatedPlansPath, assemblyName)}";
                if (Directory.Exists(actionsSourceDir))
                {
                    Directory.Delete(actionsSourceDir, true);
                    File.Delete($"{actionsSourceDir}.asmref");
                }

                Directory.CreateDirectory(generatedPlansPath);

                foreach (string file in generatedFilesPath)
                {
                    var newFilePath = file.Replace(k_TempOutputPath, generatedPlansPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                    File.Copy(file, newFilePath, true);
                }
                codeGenerator.GenerateAsmRef(generatedPlansPath, TypeResolver.PlansQualifier);
                codeGenerator.GeneratePackage(generatedPlansPath, TypeResolver.PlansQualifier);
            }
        }

        internal static bool BuildAssembly(string[] paths, string outputPath, string[] excludeReferences = null,
            string[] additionalReferences = null, string[] additionalDefines = null)
        {
            if (paths == null || paths.Length == 0)
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

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


        static DateTime GetAssemblyBuildTime(string assemblyName)
        {
            var assembly = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).FirstOrDefault(a => a.name == assemblyName);
            return assembly != null ? File.GetLastWriteTimeUtc(assembly.outputPath) : DateTime.MinValue;
        }

        internal static System.Reflection.Assembly ResolveReflectionAssemblyDependency(object sender, ResolveEventArgs args)
        {
            return System.Reflection.Assembly.ReflectionOnlyLoad(args.Name);
        }
    }
}
