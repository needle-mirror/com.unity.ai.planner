using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [QueryFilter("OR", ParameterTypes.None)]
    class LogicalOrFilter : BaseQueryFilter
    {
        internal override bool StartNewConditionBlock => true;

        internal override bool IsValid(GameObject source, ITraitBasedObjectData traitBasedObject, QueryFilterHolder holder)
        {
            return true;
        }
    }
}
