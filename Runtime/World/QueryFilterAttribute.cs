using System;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    enum ParameterTypes
    {
        None,
        String,
        Int,
        Float,
        TraitsRequired,
        TraitsProhibited,
        GameObject
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    sealed class QueryFilterAttribute : Attribute
    {
        public QueryFilterAttribute(string name, ParameterTypes parameterType)
        {
            Name = name;
            ParameterType = parameterType;
        }

        public string Name { get; }
        public ParameterTypes ParameterType { get; }
    }
}
