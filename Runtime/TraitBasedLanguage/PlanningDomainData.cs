#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.Agent;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;
using Unity.Entities;
using UnityObject = UnityEngine.Object;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    class PlanningDomainData<TObject, TStateKey, TStateData, TStateDataContext, TStateManager>
        where TObject : struct, IDomainObject
        where TStateKey : struct, IEquatable<TStateKey>, IStateKey
        where TStateData : struct, ITraitBasedStateData<TObject>
        where TStateDataContext : struct, ITraitBasedStateDataContext<TObject, TStateKey, TStateData>
        where TStateManager : JobComponentSystem, ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext>
    {
        public string Name { get; private set; }

        public Dictionary<string, IOperationalAction> ActionMapping = new Dictionary<string, IOperationalAction>();

        Dictionary<GameObject, DomainObjectID> m_InitialStateSourceObjects = new Dictionary<GameObject, DomainObjectID>();

        public IDictionary<GameObject, DomainObjectID> GetSourceObjects() => m_InitialStateSourceObjects;


        ITraitBasedStateManager<TObject, TStateKey, TStateData, TStateDataContext> m_StateManager;

        IEnumerable<IDomainObjectData> m_InitialDomainObjects;
        PlanningDomainDefinition m_Definition;

        internal void Initialize(string name, PlanningDomainDefinition domainDefinition, IEnumerable<IDomainObjectData> initialDomainObjects)
        {
            Name = name;
            m_Definition = domainDefinition;
            m_InitialDomainObjects = initialDomainObjects;
            InitializeActionMapping();
        }

        void InitializeActionMapping()
        {
            foreach (var actionDefinition in m_Definition.ActionDefinitions)
            {
                IOperationalAction operationalAction = null;
                Type gameLogicType = TypeResolver.GetType(actionDefinition.OperationalActionType);
                if (gameLogicType != null)
                    operationalAction = (IOperationalAction) Activator.CreateInstance(gameLogicType);

                ActionMapping[actionDefinition.Name] = operationalAction;
            }
        }

        public TStateData GetInitialState(EntityManager entityManager, List<TraitObjectData> initialStateData)
        {
            if (m_StateManager == null)
                m_StateManager = entityManager.World.GetExistingSystem<TStateManager>();

            var state = m_StateManager.CreateStateData();

            if (m_InitialDomainObjects == null)
            {
                return state;
            }

            var objectIDToObject = new Dictionary<ObjectID, TObject>();
            var objectIDLookup = new Dictionary<string, DomainObjectID>();

            var stateTraitTypes = initialStateData
                .Select(t => new ComponentType(TypeResolver.GetType(t.TraitDefinition.Name)))
                .ToArray();
            var (stateObject, stateDomainObjectID) = state.AddDomainObject(stateTraitTypes, "State Traits");
            objectIDLookup["State"] = stateDomainObjectID;

            // First pass - initialize objects (for linking in second pass)
            foreach (DomainObjectData objectData in m_InitialDomainObjects)
            {
                var traits = objectData.TraitData;
                var traitTypes = traits
                    .Select(t => new ComponentType(TypeResolver.GetType(t.TraitDefinition.Name)))
                    .ToArray();

                var (domainObject, domainObjectID) = state.AddDomainObject(traitTypes, objectData.Name);
                objectIDLookup[objectData.Name] = domainObjectID;
                objectIDToObject[domainObjectID.ID] = domainObject;

                var sourceObject = objectData.SourceObject;
                if (sourceObject)
                {
                    m_InitialStateSourceObjects[sourceObject] = domainObjectID;
                }
            }

            // Second pass - set all properties
            foreach (DomainObjectData objectData in m_InitialDomainObjects)
            {
                var domainObjectID = objectIDLookup[objectData.Name];
                var domainObject = objectIDToObject[domainObjectID.ID];
                foreach (var traitData in objectData.TraitData)
                {
                    var trait = InitializeTrait(traitData, objectIDLookup);
                    if (trait != null)
                        state.SetTraitOnObject(trait, ref domainObject);
                }
            }

            // Initialize state traits
            foreach (var traitData in initialStateData)
            {
                var trait = InitializeTrait(traitData, objectIDLookup);
                if (trait != null)
                    state.SetTraitOnObject(trait, ref stateObject);
            }

            return state;
        }

        ITrait InitializeTrait(TraitObjectData traitData, Dictionary<string, DomainObjectID> objectIDLookup)
        {
            var traitMatch = m_Definition.GetTrait(traitData.TraitDefinition.Name);
            if (traitMatch == null)
            {
                return null;
            }

            var trait = (ITrait)Activator.CreateInstance(TypeResolver.GetType(traitMatch.Name));
            foreach (var field in traitMatch.Fields)
            {
                var fieldName = field.Name;
                var fieldType = TypeResolver.GetType(field.Type);

                if (fieldType == null)
                    continue;

                if (fieldType == typeof(DomainObjectID))
                {
                    // Lookup domain objects by name
                    if (traitData.TryGetValue(fieldName, out string objectName) && objectName != null)
                    {
                        objectIDLookup.TryGetValue(objectName, out var id);
                        trait.SetField(fieldName, id);
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

                    if (value != null)
                    {
                        trait.SetField(fieldName, value);
                    }
                    else
                    {
                        trait.SetField(fieldName, field.DefaultValue?.GetValue(fieldType));
                    }
                }
            }
            return trait;
        }
    }
}
#endif
