#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;
using Unity.Collections;
using Unity.Entities;
using System.Runtime.Serialization;
using UnityObject = UnityEngine.Object;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    class PlannerStateConverter<TObject, TStateKey, TStateData, TStateDataContext, TStateManager> : ITraitBasedStateConverter
        where TObject : struct, ITraitBasedObject
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct, ITraitBasedStateData<TObject>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
        where TStateManager : JobComponentSystem, ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext>
    {
        Dictionary<TraitBasedObjectId, ITraitBasedObjectData> m_DataSources = new Dictionary<TraitBasedObjectId, ITraitBasedObjectData>();
        TStateManager m_StateManager;

        PlanDefinition m_Definition;

        Dictionary<ObjectId, TObject> m_ObjectIdToObject = new Dictionary<ObjectId, TObject>();
        Dictionary<ITraitBasedObjectData, TraitBasedObjectId> m_ObjectIdLookup = new Dictionary<ITraitBasedObjectData, TraitBasedObjectId>();

        public PlannerStateConverter(PlanDefinition planDefinition, TStateManager stateManager)
        {
            m_Definition = planDefinition;
            m_StateManager = stateManager;
        }

        public IStateKey CreateStateFromObjectData(IList<ITraitBasedObjectData> traitBasedObjects)
        {
            var stateData = CreateStateData(traitBasedObjects);
            return m_StateManager.GetStateDataKey(stateData) as IStateKey;
        }


        public TStateData CreateStateData(IList<ITraitBasedObjectData> traitBasedObjects)
        {
            m_ObjectIdToObject.Clear();
            m_ObjectIdLookup.Clear();

            // Retrieve known ObjectId for a specific trait-based object data
            for (int i = 0; i < traitBasedObjects.Count; i++)
            {
                var objectData = traitBasedObjects[i];
                foreach (var kvp in m_DataSources)
                {
                    if (kvp.Value == objectData)
                    {
                        m_ObjectIdLookup[objectData] = kvp.Key;
                        break;
                    }
                }
            }
            m_DataSources.Clear();

            var state = m_StateManager.CreateStateData();
            for (int i = 0; i < traitBasedObjects.Count; i++)
            {
                var objectData = traitBasedObjects[i];
                var traitTypes = new NativeArray<ComponentType>(objectData.TraitData.Count(), Allocator.TempJob);
                int index = 0;
                for (int j = 0; j < objectData.TraitData.Count; j++)
                {
                    var traitData = objectData.TraitData[j];

                    var traitDefinition = m_Definition.GetTrait(traitData.TraitDefinitionName);
                    if (traitDefinition == null)
                    {
                        // Ignore Traits not used in this Plan
                        continue;
                    }

                    if (TypeResolver.TryGetType(traitDefinition.FullyQualifiedName, out var traitType))
                        traitTypes[index++] = new ComponentType(traitType);
                }

                TraitBasedObjectId traitBasedObjectId;
                TObject traitBasedObject;
                if (m_ObjectIdLookup.ContainsKey(objectData))
                {
                    traitBasedObjectId = m_ObjectIdLookup[objectData];
                    state.AddObject(traitTypes, out traitBasedObject, traitBasedObjectId, objectData.Name);
                }
                else
                {
                    state.AddObject(traitTypes, out traitBasedObject, out traitBasedObjectId, objectData.Name);
                    m_ObjectIdLookup[objectData] = traitBasedObjectId;
                }
                traitTypes.Dispose();

                m_ObjectIdToObject[traitBasedObjectId.Id] = traitBasedObject;
                m_DataSources[traitBasedObjectId] = objectData;
            }

            // Second pass - set all properties
            for (int i = 0; i < traitBasedObjects.Count; i++)
            {
                var objectData = traitBasedObjects[i];
                var traitBasedObjectId = m_ObjectIdLookup[objectData];
                var traitBasedObject = m_ObjectIdToObject[traitBasedObjectId.Id];
                var objectIndex = state.GetTraitBasedObjectIndex(traitBasedObject);
                for (int j = 0; j < objectData.TraitData.Count; j++)
                {
                    var traitData = objectData.TraitData[j];
                    var traitDefinition = m_Definition.GetTrait(traitData.TraitDefinitionName);
                    if (traitDefinition == null)
                    {
                        // Ignore Traits not used in this Plan
                        continue;
                    }

                    var trait = InitializeTrait(traitData, traitDefinition, m_ObjectIdLookup);
                    state.SetTraitOnObjectAtIndex(trait, objectIndex);
                }
            }
            return state;
        }

        static ITrait InitializeTrait(ITraitData traitData, TraitDefinition definition, Dictionary<ITraitBasedObjectData, TraitBasedObjectId> objectIdLookup)
        {
            if (!TypeResolver.TryGetType(definition.FullyQualifiedName, out var traitType))
                throw new ArgumentException($"{definition.FullyQualifiedName} trait type cannot be found.");

            var trait = (ITrait)FormatterServices.GetUninitializedObject(traitType);
            for (int i = 0; i < definition.Fields.Count; i++)
            {
                var field = definition.Fields[i];
                var fieldName = field.Name;
                var fieldType = field.FieldType;

                if (fieldType == null)
                    continue;

                if (field.Restriction == TraitDefinitionField.FieldRestriction.NotInitializable)
                    continue;

                if (fieldType == typeof(TraitBasedObjectId))
                {
                    // Lookup state objects by name
                    if (traitData.TryGetValue(fieldName, out string objectName) && objectName != null)
                    {
                        // Link to first trait-based object with this name
                        var keys = objectIdLookup.Keys;
                        var values = objectIdLookup.Values.GetEnumerator();
                        foreach (var key in keys)
                        {
                            values.MoveNext();
                            if (key.Name == objectName)
                            {
                                trait.SetField(fieldName, values.Current);
                                break;
                            }
                        }
                        values.Dispose();
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
            m_DataSources.TryGetValue(traitBasedObjectId, out var obj);
            return obj;
        }
    }
}
#endif
