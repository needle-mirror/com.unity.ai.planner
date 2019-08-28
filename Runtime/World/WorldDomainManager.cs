using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    sealed class WorldDomainManager
    {
        static WorldDomainManager s_Instance;

        /// <summary>
        /// The current singleton instance of <see cref="WorldDomainManager"/>.
        /// </summary>
        public static WorldDomainManager Instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new WorldDomainManager();

                return s_Instance;
            }
        }

        List<IDomainObjectProvider> m_ObjectsProvider;

        WorldDomainManager()
        {
            m_ObjectsProvider = new List<IDomainObjectProvider>();
        }

        internal IEnumerable<IDomainObjectData> GetDomainObjects(GameObject agentObject, DomainObjectQuery objectQuery = null)
        {
            if (objectQuery == null)
            {
                return m_ObjectsProvider.SelectMany((o) => o.DomainObjects);
            }

            var objectList = new List<IDomainObjectData>();
            foreach (var provider in m_ObjectsProvider)
            {
                objectQuery.AddValidObjects(provider, agentObject, ref objectList);
            }

            return objectList;
        }

        public void Register(IDomainObjectProvider provider)
        {
            m_ObjectsProvider.Add(provider);
        }

        public void Unregister(IDomainObjectProvider provider)
        {
            m_ObjectsProvider.Remove(provider);
        }
    }
}
