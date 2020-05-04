using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [QueryFilter("From GameObject", ParameterTypes.GameObject)]
    class FromGameObjectQuery : BaseQueryFilter
    {
        internal override bool IsValid(GameObject source, ITraitBasedObjectData traitBasedObject, QueryFilterHolder holder)
        {
            var parent = traitBasedObject.ParentObject as GameObject;
            return parent != null && holder.ParameterGameObject == parent;
        }
    }
}
