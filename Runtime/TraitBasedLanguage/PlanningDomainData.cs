#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;
using Unity.Collections;
using Unity.Entities;
using UnityObject = UnityEngine.Object;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    class PlanningDomainData<TObject, TStateKey, TStateData, TStateDataContext, TStateManager>
        where TObject : struct, ITraitBasedObject
        where TStateKey : struct, IEquatable<TStateKey>, IStateKey
        where TStateData : struct, ITraitBasedStateData<TObject>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
        where TStateManager : JobComponentSystem, ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext>
    {
        Dictionary<TraitBasedObjectId, ITraitBasedObjectData> m_DomainDataSources = new Dictionary<TraitBasedObjectId, ITraitBasedObjectData>();
        ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext> m_StateManager;

        PlanDefinition m_Definition;

        public PlanningDomainData(PlanDefinition planDefinition)
        {
            m_Definition = planDefinition;
        }

        public TStateData CreateStateData(EntityManager entityManager, IEnumerable<ITraitBasedObjectData> traitBasedObjects)
        {
            var objectIdToObject = new Dictionary<ObjectId, TObject>();
            var objectIdLookup = new Dictionary<ITraitBasedObjectData, TraitBasedObjectId>();

            if (m_StateManager == null)
            {
                m_StateManager = entityManager.World.GetExistingSystem<TStateManager>();
            }

            // Retrieve known ObjectId for a specific trait-based object data
            foreach (var objectData in traitBasedObjects)
            {
                foreach (var kvp in m_DomainDataSources)
                {
                    if (kvp.Value == objectData)
                    {
                        objectIdLookup[objectData] = kvp.Key;
                        break;
                    }
                }
            }

            m_DomainDataSources.Clear();

            var state = m_StateManager.CreateStateData();

            foreach (var objectData in traitBasedObjects)
            {
                var traitTypes = new NativeArray<ComponentType>(objectData.TraitData.Count(), Allocator.TempJob);
                int index = 0;
                foreach (var type in objectData.TraitData)
                {
                    traitTypes[index++] = new ComponentType(TypeResolver.GetType(type.TraitDefinitionName));
                }

                TraitBasedObjectId traitBasedObjectId;
                TObject traitBasedObject;
                if (objectIdLookup.ContainsKey(objectData))
                {
                    traitBasedObjectId = objectIdLookup[objectData];
                    state.AddObject(traitTypes, out traitBasedObject, traitBasedObjectId, objectData.Name);
                }
                else
                {
                    state.AddObject(traitTypes, out traitBasedObject, out traitBasedObjectId, objectData.Name);
                    objectIdLookup[objectData] = traitBasedObjectId;
                }
                traitTypes.Dispose();

                objectIdToObject[traitBasedObjectId.Id] = traitBasedObject;
                m_DomainDataSources[traitBasedObjectId] = objectData;
            }

            // Second pass - set all properties
            foreach (ITraitBasedObjectData objectData in traitBasedObjects)
            {
                var traitBasedObjectId = objectIdLookup[objectData];
                var traitBasedObject = objectIdToObject[traitBasedObjectId.Id];
                foreach (var traitData in objectData.TraitData)
                {
                    var traitDefinition = m_Definition.GetTrait(traitData.TraitDefinitionName);
                    if (traitDefinition == null)
                    {
                        // Ignore Traits not used in this Plan
                        continue;
                    }

                    var trait = InitializeTrait(traitData, traitDefinition, objectIdLookup);
                    state.SetTraitOnObject(trait, ref traitBasedObject);
                }
            }
            return state;
        }

        static ITrait InitializeTrait(ITraitData traitData, TraitDefinition definition, Dictionary<ITraitBasedObjectData, TraitBasedObjectId> objectIdLookup)
        {
            var trait = (ITrait)Activator.CreateInstance(TypeResolver.GetType(definition.Name));
            foreach (var field in definition.Fields)
            {
                var fieldName = field.Name;
                var fieldType = TypeResolver.GetType(field.Type);

                if (fieldType == null)
                    continue;

                if (field.Restriction == TraitDefinitionField.FieldRestriction.NotInitializable)
                    continue;

                if (fieldType == typeof(TraitBasedObjectId))
                {
                    // Lookup domain objects by name
                    if (traitData.TryGetValue(fieldName, out string objectName) && objectName != null)
                    {
                        // Link to first trait-based object with this name
                        foreach (var kvp in objectIdLookup)
                        {
                            if (kvp.Key.Name == objectName)
                            {
                                trait.SetField(fieldName, kvp.Value);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // NOTE: GetValue returns a boxed object and a UnityObject for any null values (even if the field
                    // type is Transform), so we have to check for "fake null" UnityObjects that won't properly downcast
                    // to a more specific type (e.g. Transform)
                    var value = traitData.GetValue(field.Name);
                    var unityObject = value as UnityObject;
                    if (value is UnityObject && !unityObject)
                        continue;

                    trait.SetField(fieldName, value ?? field.DefaultValue?.GetValue(fieldType));
                }
            }
            return trait;
        }

        public ITraitBasedObjectData GetDataSource(TraitBasedObjectId traitBasedObjectId)
        {
            m_DomainDataSources.TryGetValue(traitBasedObjectId, out var obj);
            return obj;
        }
    }
}
#endif
