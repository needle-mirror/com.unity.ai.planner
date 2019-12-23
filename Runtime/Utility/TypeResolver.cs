using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine;

namespace Unity.AI.Planner.Utility
{
    static class TypeResolver
    {
        const string k_ValidateTypePattern = @"^[0-9]+|\s";

        public const string PlannerAssemblyName = "Unity.AI.Planner";
        public const string DomainsAssemblyName = "AI.Planner.Domains";
        public const string ActionsAssemblyName = "AI.Planner.Actions";
        public const string DomainsNamespace = "AI.Planner.Domains";
        public const string DomainEnumsNamespace = "AI.Planner.Domains.Enums.";
        public const string ActionsNamespace = "AI.Planner.Actions";

        static Dictionary<string, Type> m_TypeCache = new Dictionary<string, Type>();

        public static Type GetType(string typeName)
        {
            if (m_TypeCache.TryGetValue(typeName, out Type type))
                return type;

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
                type = Type.GetType($"{typeName},{PlannerAssemblyName}");
            if (type == null)
                type = Type.GetType($"{typeName},UnityEngine");
            if (type == null)
                type = Type.GetType(typeName);

            if (type != null)
                m_TypeCache.Add(typeName, type);

            return type;
        }

        public static string ToTypeNameCase(string name)
        {
            return Regex.Replace(name, k_ValidateTypePattern, string.Empty);
        }
    }
}
