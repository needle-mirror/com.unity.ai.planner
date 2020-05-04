using UnityEngine;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [QueryFilter("In radius", ParameterTypes.Float)]
    class DistanceQueryFilter : BaseQueryFilter
    {
        internal override bool IsValid(GameObject source, ITraitBasedObjectData traitBasedObject, QueryFilterHolder holder)
        {
            var parent = traitBasedObject.ParentObject as GameObject;
            return parent != null &&  Vector3.Distance(source.transform.position, parent.transform.position) <= holder.ParameterFloat;
        }
    }
}
