using System;
using System.Text.RegularExpressions;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine;

namespace Unity.AI.Planner.Utility
{
    static class TypeResolver
    {
        private const string k_ValidateTypePattern = @"^[0-9]+|\s";

        public const string PlannerAssemblyName = "Unity.AI.Planner";
        public const string DomainsAssemblyName = "AI.Planner.Domains";
        public const string ActionsAssemblyName = "AI.Planner.Actions";
        public const string DomainsNamespace = "AI.Planner.Domains";
        public const string ActionsNamespace = "AI.Planner.Actions";

        public static Type GetType(string typeName)
        {
            // TODO add cache or replace with 2019.3 TypeCache system
            Type type = null;

            if (!typeName.Contains("."))
            {
                type = Type.GetType($"{DomainsNamespace}.{typeName},{DomainsAssemblyName}");
                if (type == null)
                    type = Type.GetType($"{ActionsNamespace}.{typeName},{ActionsAssemblyName}");
                if (type == null)
                    type = Type.GetType($"{typeof(ICustomTrait).Namespace}.{typeName},{PlannerAssemblyName}");
            }

            if (type == null)
                type = Type.GetType($"{typeName},Assembly-CSharp");
            if (type == null)
                type = Type.GetType($"{typeName},{DomainsAssemblyName}");
            if (type == null)
                type = Type.GetType($"{typeName},UnityEngine");
            if (type == null)
                type = Type.GetType(typeName);

            return type;
        }

        public static string ToTypeNameCase(string name)
        {
            return Regex.Replace(name, k_ValidateTypePattern,string.Empty);
        }
    }
}
