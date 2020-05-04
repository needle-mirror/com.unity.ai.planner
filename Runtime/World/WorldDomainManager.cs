using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    sealed class WorldDomainManager
    {
        static WorldDomainManager s_Instance;

        /// <summary>
        /// The current singleton instance of <see cref="WorldDomainManager"/>.
        /// </summary>
        public static WorldDomainManager Instance => s_Instance ?? (s_Instance = new WorldDomainManager());

        List<ITraitBasedObjectData> m_TraitBasedObjects;

        WorldDomainManager()
        {
            m_TraitBasedObjects = new List<ITraitBasedObjectData>();
        }

        internal List<ITraitBasedObjectData> GetTraitBasedObjects(GameObject controllerObject, TraitBasedObjectQuery objectQuery = null)
        {
            if (objectQuery == null)
            {
                return m_TraitBasedObjects;
            }

            var objectList = new List<ITraitBasedObjectData>();
            foreach (var traitBasedObjectData in m_TraitBasedObjects)
            {
                objectQuery.AddValidObjects(traitBasedObjectData, controllerObject, ref objectList);
            }

            return objectList;
        }

        public void Register(ITraitBasedObjectData objectData)
        {
            m_TraitBasedObjects.Add(objectData);
        }

        public void Unregister(ITraitBasedObjectData objectData)
        {
            m_TraitBasedObjects.Remove(objectData);
        }
    }
}
