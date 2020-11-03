using Unity.Semantic.Traits.Utility;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Unity.AI.Planner
{
    static class AssemblyRegistration
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#endif
        static void RegisterResolver()
        {
            TypeResolver.AddResolver($"{{0}},{Utility.TypeHelper.StateRepresentationQualifier}");
            TypeResolver.AddResolver($"{{0}},{Utility.TypeHelper.IncludedModulesQualifier}");
            TypeResolver.AddResolver($"{{0}},{Utility.TypeHelper.PlansQualifier}");
            TypeResolver.AddResolver($"{{0}},{Utility.TypeHelper.PlannerAssemblyName}");
            TypeResolver.AddResolver($"{{0}},{Utility.TypeHelper.CustomAssemblyName}");
        }
    }
}
