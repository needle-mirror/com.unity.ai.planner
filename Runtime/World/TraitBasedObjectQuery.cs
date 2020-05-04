using System;
using System.Collections.Generic;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    abstract class BaseQueryFilter
    {
        internal virtual bool StartNewConditionBlock => false;

        internal abstract bool IsValid(GameObject source, ITraitBasedObjectData provider, QueryFilterHolder holder);
    }

    [Serializable]
    sealed class QueryFilterHolder
    {
#pragma warning disable 0649
        [SerializeField]
        string m_Name;

        [SerializeField]
        string m_TypeName;

        [SerializeField]
        int m_ParameterInt;

        [SerializeField]
        float m_ParameterFloat;

        [SerializeField]
        string m_ParameterString;

        [SerializeField]
        List<TraitDefinition> m_ParameterTraits;

        [SerializeField]
        GameObject m_GameObject;
#pragma warning restore 0649

        BaseQueryFilter m_FilterTypeCache;

        public int ParameterInt => m_ParameterInt;
        public float ParameterFloat => m_ParameterFloat;
        public string ParameterString => m_ParameterString;
        public List<TraitDefinition> ParameterTraits => m_ParameterTraits;
        public GameObject ParameterGameObject => m_GameObject;

        public bool IsNewConditionBlock()
        {
            var query = GetQueryByType();
            return query != null && query.StartNewConditionBlock;
        }

        public bool IsValid(GameObject source, ITraitBasedObjectData traitBasedObject)
        {
            var query = GetQueryByType();
            return query != null && query.IsValid(source, traitBasedObject, this);
        }

        BaseQueryFilter GetQueryByType()
        {
            if (m_FilterTypeCache == null)
            {
                if (!TypeResolver.TryGetType(m_TypeName, out var type))
                   return null;

                m_FilterTypeCache = Activator.CreateInstance(type) as BaseQueryFilter;
            }

            return m_FilterTypeCache;
        }

        internal void ResetCache()
        {
            m_FilterTypeCache = null;
        }
    }

    [Serializable]
    class TraitBasedObjectQuery
    {
#pragma warning disable 0649
        [SerializeField]
        List<QueryFilterHolder> m_Filters;
#pragma warning restore 0649

        internal List<QueryFilterHolder> Filters => m_Filters;

        internal void AddValidObjects(ITraitBasedObjectData traitHolder, GameObject agentObject, ref List<ITraitBasedObjectData> objectList)
        {
            if (!objectList.Contains(traitHolder) && IsValid(agentObject, traitHolder))
                objectList.Add(traitHolder);
        }

        internal bool IsValid(GameObject agentObject, ITraitBasedObjectData objectData)
        {
            if (m_Filters == null || m_Filters.Count == 0)
                return true;

            bool firstBlock = true;
            bool currentBlockValidity = false;
            foreach (var filter in m_Filters)
            {
                // Check if new block
                if (filter.IsNewConditionBlock())
                {
                    if (currentBlockValidity) // resolve previous block
                        return true;

                    currentBlockValidity = true;
                    continue;
                }

                // Handle first block case (in the event a new condition block---"OR"---has not been started).
                if (firstBlock)
                {
                    currentBlockValidity = true;
                    firstBlock = false;
                }

                // Check if block has already failed
                if (!currentBlockValidity)
                    continue;

                // Update filters for current block
                currentBlockValidity &= filter.IsValid(agentObject, objectData);
            }

            return currentBlockValidity;
        }
    }
}
