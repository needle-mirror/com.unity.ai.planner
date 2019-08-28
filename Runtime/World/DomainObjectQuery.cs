using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.AI.Planner.Utility;
using UnityEngine.Events;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class DomainObjectQuery
    {
        // TODO add editable query filters

        internal void AddValidObjects(IDomainObjectProvider provider, GameObject agentObject, ref List<IDomainObjectData> objectList)
        {
            foreach (var objectData in provider.DomainObjects)
            {
                if (!objectList.Contains(objectData) && IsValid(agentObject, objectData))
                    objectList.Add(objectData);
            }
        }

        bool IsValid(GameObject agentObject, IDomainObjectData objectData)
        {
            return true;
        }
    }
}
