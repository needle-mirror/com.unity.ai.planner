#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.Utility;
using Unity.Semantic.Traits.Utility;
using Unity.Collections;
using Unity.Entities;
using Unity.Semantic.Traits;
using UnityObject = UnityEngine.Object;
using UnityEngine;

namespace Unity.AI.Planner.Traits
{
    class PlannerStateConverter<TObject, TStateKey, TStateData, TStateDataContext, TStateManager> : ITraitBasedStateConverter, IDisposable
        where TObject : struct, ITraitBasedObject
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct, ITraitBasedStateData<TObject>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
        where TStateManager : ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext>
    {
        NativeHashMap<TraitBasedObjectId, Entity> m_ObjectIdToEntity;
        TStateManager m_StateManager;

        // Used locally for caching purposes
        Dictionary<ObjectId, TObject> m_ObjectIdToObject = new Dictionary<ObjectId, TObject>();
        Dictionary<Entity, TraitBasedObjectId> m_EntityToObjectId = new Dictionary<Entity, TraitBasedObjectId>();
        NativeHashMap<ComponentType, ComponentType> m_TypeLookup;
        public PlannerStateConverter(ProblemDefinition problemDefinition, TStateManager stateManager)
        {
            m_StateManager = stateManager;
            m_ObjectIdToEntity = new NativeHashMap<TraitBasedObjectId, Entity>(1, Allocator.Persistent);

            BuildTypeCorrespondence(problemDefinition);
        }

        void BuildTypeCorrespondence(ProblemDefinition problemDefinition)
        {
            var traitDefinitions = problemDefinition.GetTraitsUsed().ToArray();

            m_TypeLookup = new NativeHashMap<ComponentType, ComponentType>(traitDefinitions.Length, Allocator.Persistent);

            foreach (var traitDefinition in traitDefinitions)
            {
                // find {Trait}Data type
                if (!TypeResolver.TryGetType($"{traitDefinition.name}Data", out var dataType))
                    continue;

                // find {Trait} type (planner version)
                if (!(TypeResolver.TryGetType($"{TypeHelper.StateRepresentationQualifier}.{traitDefinition.name}", out var plannerType)
                    || TypeResolver.TryGetType($"{TypeHelper.IncludedModulesQualifier}.{traitDefinition.name}", out plannerType)))
                    continue;

                m_TypeLookup.Add(dataType, plannerType);
            }
        }

        public IStateKey CreateState(Entity planningAgent, IEnumerable<Entity> traitBasedObjects = null)
        {
            var stateData = CreateStateData(planningAgent, traitBasedObjects);
            return m_StateManager.GetStateDataKey(stateData) as IStateKey;
        }

        public TStateData CreateStateData(Entity planningAgent, IEnumerable<Entity> traitBasedObjects = null)
        {
            m_ObjectIdToObject.Clear();
            m_EntityToObjectId.Clear();

            var sourceEntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Retrieve known ObjectId for a specific trait-based object data
            if (traitBasedObjects != null)
            {
                foreach (var objectData in traitBasedObjects)
                {
                    foreach (var kvp in m_ObjectIdToEntity)
                    {
                        if (kvp.Value == objectData)
                        {
                            m_EntityToObjectId[objectData] = kvp.Key;
                            break;
                        }
                    }
                }
            }
            m_ObjectIdToEntity.Clear();

            var state = m_StateManager.CreateStateData();
            NativeArray<Entity> entities;
            if (traitBasedObjects != null && traitBasedObjects.Any())
            {
                entities = new NativeArray<Entity>(traitBasedObjects.ToArray(), Allocator.TempJob);
            }
            else
            {
                entities = sourceEntityManager
                    .CreateEntityQuery(ComponentType.ReadOnly<SemanticObjectData>())
                    .ToEntityArray(Allocator.TempJob);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == default)
                    continue;

                var sourceTypes = sourceEntityManager.GetComponentTypes(entity);

                var plannerTypes = new NativeList<ComponentType>(sourceTypes.Length, Allocator.TempJob);
                for (int j = 0; j < sourceTypes.Length; j++)
                {
                    if (m_TypeLookup.TryGetValue(sourceTypes[j], out var plannerType))
                        plannerTypes.Add(plannerType);
                }

                if (entity == planningAgent)
                    plannerTypes.Add(typeof(PlanningAgent));

                TObject traitBasedObject;
                FixedString64 entityName = default;
#if UNITY_EDITOR
                entityName= sourceEntityManager.GetName(entity);
#endif
                if (m_EntityToObjectId.TryGetValue(entity, out var traitBasedObjectId))
                {
                    state.AddObject(plannerTypes, out traitBasedObject, traitBasedObjectId, entityName);
                }
                else
                {
                    state.AddObject(plannerTypes, out traitBasedObject, out traitBasedObjectId, entityName);
                    m_EntityToObjectId[entity] = traitBasedObjectId;
                }
                plannerTypes.Dispose();

                m_ObjectIdToObject[traitBasedObjectId.Id] = traitBasedObject;
                m_ObjectIdToEntity[traitBasedObjectId] = entity;
            }

            // Second pass - set all properties
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == default)
                    continue;

                var sourceTraitTypes = sourceEntityManager.GetComponentTypes(entity);

                var traitBasedObjectId = m_EntityToObjectId[entity];
                var traitBasedObject = m_ObjectIdToObject[traitBasedObjectId.Id];
                state.ConvertAndSetPlannerTrait(entity, sourceEntityManager, sourceTraitTypes,
                    m_EntityToObjectId, ref traitBasedObject);
            }
            entities.Dispose();


            return state;
        }

        public GameObject GetDataSource(TraitBasedObjectId traitBasedObjectId)
        {
            if (m_ObjectIdToEntity.TryGetValue(traitBasedObjectId, out var entity))
            {
                var sourceEntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                if (sourceEntityManager.Exists(entity))
                    return sourceEntityManager.GetComponentObject<Transform>(entity).gameObject;
            }

            return null;
        }

        public void Dispose()
        {
            if (m_TypeLookup.IsCreated)
                m_TypeLookup.Dispose();
            if (m_ObjectIdToEntity.IsCreated)
                m_ObjectIdToEntity.Dispose();
        }
    }
}
#endif
