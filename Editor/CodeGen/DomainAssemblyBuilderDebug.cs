using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Planner.Utility;
using UnityEditor.Compilation;

namespace UnityEditor.AI.Planner.CodeGen
{
    /// <summary>
    /// Note: Generate code and copy files in the project even when the compilation failed for debug purpose
    /// </summary>
    static class DomainAssemblyBuilderDebug
    {
        const string k_DebugBuildMenuTitle = "AI/Planner/Build as files in project (Debug)";

        internal static readonly string k_GeneratedPath = $"{DomainAssemblyBuilder.k_PlannerProjectPath}Generated/";
        static readonly string k_AdditionalReferences = "ADDITIONAL_REFERENCES";
        static string s_DomainsAssemblyDefinitionContent = $"{{\"name\": \"{TypeResolver.DomainsNamespace}\", \"references\": [\"{TypeResolver.PlannerAssemblyName}\",\"Unity.Entities\",\"Unity.Jobs\",\"Unity.Collections\", \"Unity.FullDotNet\"]}}";
        static string s_ActionsAssemblyDefinitionContent = $"{{\"name\": \"{TypeResolver.ActionsNamespace}\", \"references\": [\"{TypeResolver.PlannerAssemblyName}\",\"Unity.Entities\",\"Unity.Jobs\",\"Unity.Collections\", \"Unity.FullDotNet\", \"{TypeResolver.DomainsNamespace}\"{k_AdditionalReferences}]}}";

        [MenuItem(k_DebugBuildMenuTitle, true)]
        public static bool BuildMenuValidate()
        {
            return !EditorApplication.isCompiling;
        }

        [MenuItem(k_DebugBuildMenuTitle)]
        public static void BuildDomain()
        {
            var codeGenerator = new CodeGenerator();

            var domainsNamespace = TypeResolver.DomainsNamespace;
            var domainsAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).FirstOrDefault(a => a.name == domainsNamespace);

            // Build domain DLL first
            var paths = codeGenerator.GenerateDomain(DomainAssemblyBuilder.k_OutputPath, "Assets/").ToList();
            DomainAssemblyBuilder.BuildAssembly(paths.ToArray(), DomainAssemblyBuilder.k_OutputDomainsAssembly,
                domainsAssembly != null ? new [] { domainsAssembly.outputPath } : null);

            var generatedDomainsPath = $"{k_GeneratedPath}{domainsNamespace}";
            if (Directory.Exists(generatedDomainsPath))
                Directory.Delete(generatedDomainsPath, true);
            Directory.CreateDirectory(generatedDomainsPath);

            foreach (string file in paths)
            {
                var newFilePath = file;
                if (newFilePath.StartsWith(DomainAssemblyBuilder.k_OutputPath))
                {
                    newFilePath = newFilePath.Remove(0, DomainAssemblyBuilder.k_OutputPath.Length - 1);
                }

                newFilePath = $"{k_GeneratedPath}{newFilePath}";

                Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                File.Copy(file, newFilePath, true);

                File.WriteAllText($"{generatedDomainsPath}/{domainsNamespace}.asmdef", s_DomainsAssemblyDefinitionContent);
            }

            // Now build actions DLL, which will depend on domain DLL
            var actionsNamespace = TypeResolver.ActionsNamespace;
            paths = codeGenerator.GeneratePlans(DomainAssemblyBuilder.k_OutputPath, "Assets/").ToList();

            var dependentAssemblies = new List<string>();
            var additionalReferences = new List<string>();
            additionalReferences.Add(DomainAssemblyBuilder.k_OutputDomainsAssembly);

            // Collect any custom user assemblies
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (var assembly in assemblies)
            {
                if (assembly.name.Contains("CSharp") || assembly.name == actionsNamespace)
                    continue;

                var assemblyReferences = assembly.assemblyReferences;
                if (assemblyReferences.Any(a => a.name == DomainAssemblyBuilder.k_CustomAssemblyReference))
                {
                    additionalReferences.Add(assembly.outputPath);
                    dependentAssemblies.Add(assembly.name);
                }
            }

            DomainAssemblyBuilder.BuildAssembly(paths.ToArray(), DomainAssemblyBuilder.k_OutputActionsAssembly,
                additionalReferences: additionalReferences.ToArray());

            var generatedActionsPath = $"{k_GeneratedPath}{actionsNamespace}";
            if (Directory.Exists(generatedActionsPath))
                Directory.Delete(generatedActionsPath, true);
            Directory.CreateDirectory(generatedActionsPath);

            foreach (string file in paths)
            {
                var newFilePath = file;
                if (newFilePath.StartsWith(DomainAssemblyBuilder.k_OutputPath))
                {
                    newFilePath = newFilePath.Remove(0, DomainAssemblyBuilder.k_OutputPath.Length - 1);
                }

                newFilePath = $"{k_GeneratedPath}{newFilePath}";

                Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                File.Copy(file, newFilePath, true);
            }
            var asmDefContent = s_ActionsAssemblyDefinitionContent.Replace(k_AdditionalReferences,
                dependentAssemblies.Count == 0 ? string.Empty :
                    ", " + string.Join(",", dependentAssemblies.Select(a => $"\"{a}\"")));
            File.WriteAllText($"{generatedActionsPath}/{actionsNamespace}.asmdef", asmDefContent);

            File.Delete(DomainAssemblyBuilder.k_DomainsAssemblyProjectPath);
            File.Delete(DomainAssemblyBuilder.k_ActionsAssemblyProjectPath);

            AssetDatabase.Refresh();
        }
    }
}
