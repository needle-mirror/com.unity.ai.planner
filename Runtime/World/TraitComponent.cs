using System;
using System.Collections.Generic;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Component that implements <see cref="ITraitBasedObjectData"/> to use traits on GameObjects
    /// </summary>
    [HelpURL(Help.BaseURL + "/manual/ConfigureScene.html")]
    [AddComponentMenu("AI/Trait/Trait Component")]
    public class TraitComponent : MonoBehaviour, ITraitBasedObjectData
    {
#pragma warning disable 0649
        [SerializeField]
        TraitBasedObjectData m_ObjectData;
#pragma warning restore 0649

        /// <summary>
        /// Name of the TraitBasedObject
        /// </summary>
        public string Name
        {
            get => m_ObjectData.Name;
            set => m_ObjectData.Name = value;
        }

        /// <summary>
        /// Object that holds this instance
        /// </summary>
        public object ParentObject => m_ObjectData.ParentObject;

        IList<TraitData> ITraitBasedObjectData.TraitData => m_ObjectData.TraitData;

        /// <summary>
        /// Get data for a given trait
        /// </summary>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>Initialization data</returns>
        public ITraitData GetTraitData<TTrait>() where TTrait : ITrait
        {
            return m_ObjectData.GetTraitData<TTrait>();
        }

        /// <summary>
        /// Remove data for a given trait
        /// </summary>
        /// <typeparam name="TTrait">Trait type</typeparam>
        public void RemoveTraitData<TTrait>() where TTrait : ITrait
        {
            m_ObjectData.RemoveTraitData<TTrait>();
        }

        /// <summary>
        /// Checks whether the component has a specific type of trait
        /// </summary>
        /// <typeparam name="TTrait">Trait type</typeparam>
        /// <returns>True, if the component has the trait</returns>
        public bool HasTraitData<TTrait>() where TTrait : ITrait
        {
            return m_ObjectData.HasTraitData<TTrait>();
        }

        /// <summary>
        /// Add a trait and initialize with default values
        /// </summary>
        /// <param name="definition">Definition of the Trait</param>
        public void AddDefaultTrait(TraitDefinition definition)
        {
            var traitData = new TraitData(definition);
            m_ObjectData.AddTraitData(traitData);
        }

        void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (m_ObjectData == null)
                m_ObjectData = new TraitBasedObjectData();

            m_ObjectData.Initialize(gameObject);
        }

        void OnEnable()
        {
            WorldDomainManager.Instance.Register(this);
        }

        void OnDisable()
        {
            WorldDomainManager.Instance.Unregister(this);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_ObjectData == null)
                m_ObjectData = new TraitBasedObjectData() { Name = name };

            m_ObjectData.OnValidate();
        }

        void OnDrawGizmos()
        {
            if (m_ObjectData == null || m_ObjectData.TraitData == null)
                return;

            bool isSelected = UnityEditor.Selection.activeGameObject == gameObject;
            foreach (var traitData in m_ObjectData.TraitData)
            {
                if (!traitData.IsInitialized)
                    continue;

                var gizmoMethod = TraitGizmos.GetGizmoMethod(traitData.TraitDefinitionName);
                gizmoMethod?.Invoke(gameObject, traitData, isSelected);
            }
        }
#endif
    }
}
