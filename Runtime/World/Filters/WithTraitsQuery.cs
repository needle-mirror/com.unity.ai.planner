using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [QueryFilter("With Traits", ParameterTypes.TraitsRequired)]
    class WithTraitsQuery : BaseQueryFilter
    {
        internal override bool IsValid(GameObject source, ITraitBasedObjectData traitBasedObject, QueryFilterHolder holder)
        {
            var parameterTraits = holder.ParameterTraits;
            var traitData = traitBasedObject.TraitData;
            for (int i = 0; i < parameterTraits.Count; i++)
            {
                var trait = parameterTraits[i];

                bool found = false;
                for (int j = 0; j < traitData.Count; j++)
                {
                    if (traitData[j].TraitDefinitionName.Equals(trait.Name))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return false;
            }

            return true;
        }
    }
}
