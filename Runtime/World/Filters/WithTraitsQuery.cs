using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [QueryFilter("With Traits", ParameterTypes.TraitsRequired)]
    class WithTraitsQuery : BaseQueryFilter
    {
        internal override bool IsValid(GameObject source, ITraitBasedObjectData traitBasedObject, QueryFilterHolder holder)
        {
            return holder.ParameterTraits.All(trait => traitBasedObject.TraitData.Any(data => data.TraitDefinitionName == trait.Name));
        }
    }
}
