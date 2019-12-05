using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [QueryFilter("From GameObject", ParameterTypes.GameObject)]
    class FromGameObjectQuery : BaseQueryFilter
    {
        internal override bool IsValid(GameObject source, ITraitBasedObjectData traitBasedObject, QueryFilterHolder holder)
        {
            return holder.ParameterGameObject == (traitBasedObject.ParentObject as GameObject);
        }
    }
}
