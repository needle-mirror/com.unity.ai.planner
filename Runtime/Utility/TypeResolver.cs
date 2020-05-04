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
        public const string CustomAssemblyName = "AI.Planner.Custom";

        public const string StateRepresentationQualifier = "Generated.AI.Planner.StateRepresentation";
        public const string PlansQualifier = "Generated.AI.Planner.Plans";

        public const string TraitEnumsNamespace = StateRepresentationQualifier + ".Enums.";

        static Dictionary<string, Type> s_TypeCache = new Dictionary<string, Type>();

        public static bool TryGetType(string qualifiedTypeName, out Type type)
        {
            if (s_TypeCache.TryGetValue(qualifiedTypeName, out type))
                return true;

            if (type == null)
                type = Type.GetType($"{qualifiedTypeName},Assembly-CSharp");
            if (type == null)
                type = Type.GetType($"{qualifiedTypeName},{StateRepresentationQualifier}");
            if (type == null)
                type = Type.GetType($"{qualifiedTypeName},{PlansQualifier}");
            if (type == null)
                type = Type.GetType($"{qualifiedTypeName},{PlannerAssemblyName}");
            if (type == null)
                type = Type.GetType($"{qualifiedTypeName},UnityEngine");
            if (type == null)
                type = Type.GetType(qualifiedTypeName);

            if (type != null)
                s_TypeCache.Add(qualifiedTypeName, type);

            return type != null;
        }

        public static string ToTypeNameCase(string name)
        {
            return Regex.Replace(name, k_ValidateTypePattern, string.Empty);
        }
    }
}
