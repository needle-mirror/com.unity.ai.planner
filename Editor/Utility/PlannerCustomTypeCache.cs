using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;

namespace UnityEditor.AI.Planner.Utility
{
    static class PlannerCustomTypeCache
    {
        static readonly Type[] k_ActionRewardInterfaces = { typeof(ICustomActionReward<>), typeof(ICustomTraitReward<>), typeof(ICustomTraitReward<,>), typeof(ICustomTraitReward<,,>)};

        static Type[] s_ActionEffectTypes;
        static Type[] s_ActionParameterComparerTypes;
        static Type[] s_ActionPreconditionTypes;
        static Type[] s_ActionRewardTypes;
        static Type[] s_HeuristicTypes;
        static Type[] s_TerminationRewardTypes;
        static Type[] s_TerminationPreconditionTypes;

        public static Type[] ActionRewardTypes => s_ActionRewardTypes;
        public static Type[] TerminationRewardTypes  => s_TerminationRewardTypes;
        public static Type[] TerminationPreconditionTypes  => s_TerminationPreconditionTypes;
        public static Type[] ActionPreconditionTypes => s_ActionPreconditionTypes;
        public static Type[] ActionEffectTypes  => s_ActionEffectTypes;
        public static Type[] ActionParameterComparerTypes => s_ActionParameterComparerTypes;
        public static Type[] HeuristicTypes  => s_HeuristicTypes;

        public static void Refresh()
        {
            s_ActionEffectTypes = GetCustomTypesDerivedFrom(typeof(ICustomActionEffect<>));
            s_ActionPreconditionTypes = GetCustomTypesDerivedFrom(typeof(ICustomActionPrecondition<>));
            s_ActionRewardTypes = GetCustomTypesDerivedFrom(k_ActionRewardInterfaces);
            s_ActionParameterComparerTypes = GetCustomTypesDerivedFrom(typeof(IParameterComparer<>));

            s_TerminationRewardTypes = GetCustomTypesDerivedFrom(typeof(ICustomTerminationReward<>));
            s_TerminationPreconditionTypes = GetCustomTypesDerivedFrom(typeof(ICustomTerminationPrecondition<>));

            s_HeuristicTypes = GetCustomTypesDerivedFrom(typeof(ICustomHeuristic<>));
        }

        static Type[] GetCustomTypesDerivedFrom(Type type)
        {
            return TypeCache.GetTypesDerivedFrom(type).Where(t => !t.IsGenericType && t.Assembly.GetName().Name == TypeResolver.CustomAssemblyName).ToArray();
        }

        static Type[] GetCustomTypesDerivedFrom(Type[] types)
        {
            List<Type> allTypes = new List<Type>();
            foreach (var type in types)
            {
                allTypes.AddRange(GetCustomTypesDerivedFrom(type));
            }

            return allTypes.ToArray();
        }

        public static bool IsActionRewardType(Type type)
        {
            var implementedInterface = type.GetInterfaces();
            foreach (var interfaceType in k_ActionRewardInterfaces)
            {
                if (implementedInterface.FirstOrDefault(i => i.Name == interfaceType.Name) != default)
                    return true;
            }

            return false;
        }
    }
}
