using System;
using System.Collections.Generic;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.Serialization;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    class DomainObjectProvider : MonoBehaviour, IDomainObjectProvider
    {
#pragma warning disable 0649
        [SerializeField]
        List<DomainObjectData> m_ObjectData;
#pragma warning restore 0649

        public IEnumerable<IDomainObjectData> DomainObjects => m_ObjectData;

        void Awake()
        {
            foreach (var data in m_ObjectData)
            {
                data.SourceObject = gameObject;
                data.InitializeTraitData();
            }
        }

        void OnEnable()
        {
            WorldDomainManager.Instance.Register(this);
        }

        void OnDisable()
        {
            WorldDomainManager.Instance.Unregister(this);
        }
    }
}
