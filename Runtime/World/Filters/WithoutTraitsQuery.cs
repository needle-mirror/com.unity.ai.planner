using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [QueryFilter("Without Traits", ParameterTypes.TraitsProhibited)]
    class WithoutTraitsQuery : BaseQueryFilter
    {
        internal override bool IsValid(GameObject source, ITraitBasedObjectData traitBasedObject, QueryFilterHolder holder)
        {
            return !holder.ParameterTraits.Any(trait => traitBasedObject.TraitData.Any(data => data.TraitDefinitionName == trait.Name));
        }
    }
}
