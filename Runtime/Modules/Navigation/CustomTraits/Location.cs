using System;
using Unity.Semantic.Traits;
using Unity.Entities;
using UnityEngine;

namespace Generated.Semantic.Traits
{
    /// <summary>
    /// Component representing the Location trait.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Semantic/Traits/Location (Trait)")]
    [RequireComponent(typeof(SemanticObject))]
    public partial class Location : MonoBehaviour, ITrait
    {
        /// <summary>
        /// The transform of the object.
        /// </summary>
        public UnityEngine.Transform Transform
        {
            get { return m_p338023941; }
            set
            {
                LocationData data = default;
                var dataActive = m_EntityManager != default && m_EntityManager.HasComponent<LocationData>(m_Entity);
                if (dataActive)
                    data = m_EntityManager.GetComponentData<LocationData>(m_Entity);
                m_p338023941 = value;
                data.Transform = transform;
                if (dataActive)
                    m_EntityManager.SetComponentData(m_Entity, data);

                Position = value.position;
                Forward = value.forward;
            }
        }

        /// <summary>
        /// The position of the object.
        /// </summary>
        public UnityEngine.Vector3 Position
        {
            get
            {
                if (m_EntityManager != default && m_EntityManager.HasComponent<LocationData>(m_Entity))
                {
                    m_p2084774077 = m_EntityManager.GetComponentData<LocationData>(m_Entity).Position;
                }

                return m_p2084774077;
            }
            set
            {
                LocationData data = default;
                var dataActive = m_EntityManager != default && m_EntityManager.HasComponent<LocationData>(m_Entity);
                if (dataActive)
                    data = m_EntityManager.GetComponentData<LocationData>(m_Entity);
                Transform.position = data.Position = m_p2084774077 = value;
                if (dataActive)
                    m_EntityManager.SetComponentData(m_Entity, data);
            }
        }

        /// <summary>
        /// The forward vector of the object.
        /// </summary>
        public UnityEngine.Vector3 Forward
        {
            get
            {
                if (m_EntityManager != default && m_EntityManager.HasComponent<LocationData>(m_Entity))
                {
                    m_p2006904664 = m_EntityManager.GetComponentData<LocationData>(m_Entity).Forward;
                }

                return m_p2006904664;
            }
            set
            {
                LocationData data = default;
                var dataActive = m_EntityManager != default && m_EntityManager.HasComponent<LocationData>(m_Entity);
                if (dataActive)
                    data = m_EntityManager.GetComponentData<LocationData>(m_Entity);
                Transform.forward = data.Forward = m_p2006904664 = value;
                if (dataActive)
                    m_EntityManager.SetComponentData(m_Entity, data);
            }
        }

        /// <summary>
        /// The component data representation of the trait.
        /// </summary>
        public LocationData Data
        {
            get => m_World != null && m_World.IsCreated && m_EntityManager != default && m_EntityManager.HasComponent<LocationData>(m_Entity)
                ? m_EntityManager.GetComponentData<LocationData>(m_Entity) : GetData();
            set
            {
                if (m_World != null && m_World.IsCreated && m_EntityManager != default && m_EntityManager.HasComponent<LocationData>(m_Entity))
                    m_EntityManager.SetComponentData(m_Entity, value);
            }
        }

#pragma warning disable 649
        [SerializeField]
        [InspectorName("Transform")]
        UnityEngine.Transform m_p338023941 = default;
        [SerializeField]
        [HideInInspector]
        UnityEngine.Vector3 m_p2084774077 = default;
        [SerializeField]
        [HideInInspector]
        UnityEngine.Vector3 m_p2006904664 = default;
#pragma warning restore 649

        EntityManager m_EntityManager;
        World m_World;
        Entity m_Entity;

        LocationData GetData()
        {
            LocationData data = default;
            data.Transform = m_p338023941;

            return data;
        }


        void OnEnable()
        {
            // Handle the case where this trait is added after conversion
            var semanticObject = GetComponent<SemanticObject>();
            if (semanticObject && !semanticObject.Entity.Equals(default))
                Convert(semanticObject.Entity, semanticObject.EntityManager, null);

            Transform = gameObject.transform;
        }

        /// <summary>
        /// Converts and assigns the monobehaviour trait component data to the entity representation.
        /// </summary>
        /// <param name="entity">The entity on which the trait data is to be assigned.</param>
        /// <param name="destinationManager">The entity manager for the given entity.</param>
        /// <param name="_">An unused GameObjectConversionSystem parameter, needed for IConvertGameObjectToEntity.</param>
        public void Convert(Entity entity, EntityManager destinationManager, GameObjectConversionSystem _)
        {
            m_Entity = entity;
            m_EntityManager = destinationManager;
            m_World = destinationManager.World;

            if (!destinationManager.HasComponent(entity, typeof(LocationData)))
            {
                destinationManager.AddComponentData(entity, GetData());
            }
        }

        void OnDestroy()
        {
            if (m_World != default && m_World.IsCreated)
            {
                m_EntityManager.RemoveComponent<LocationData>(m_Entity);
                if (m_EntityManager.GetComponentCount(m_Entity) == 0)
                    m_EntityManager.DestroyEntity(m_Entity);
            }
        }

        void OnValidate()
        {

            // Commit local fields to backing store
            Data = GetData();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            TraitGizmos.DrawGizmoForTrait(nameof(LocationData), gameObject, Data);
        }
#endif
    }
}
