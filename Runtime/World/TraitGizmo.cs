using System;
using Unity.AI.Planner.DomainLanguage.TraitBased;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    static class TraitGizmos
    {
        static Dictionary<string, Type> s_DrawGizmoType = new Dictionary<string, Type>();

        public static bool HasCustomGizmoType(string traitName)
        {
            if (s_DrawGizmoType.ContainsKey(traitName))
            {
                return true;
            }

            var types = TypeCache.GetTypesWithAttribute<TraitGizmoAttribute>();
            foreach (var type in types)
            {
                var attributes = type.GetCustomAttributes(typeof(TraitGizmoAttribute), false);
                foreach (TraitGizmoAttribute gizmo in attributes)
                {
                    if (gizmo.m_TraitType.Name == traitName)
                    {
                        s_DrawGizmoType.Add(traitName, type);
                        return true;
                    }
                }
            }

            return false;
        }

        public static void DrawGizmo(string traitName, GameObject gameObject, ITraitData traitData, bool isSelected)
        {
            if (s_DrawGizmoType.TryGetValue(traitName, out Type gizmoType))
            {
                System.Reflection.MethodInfo info = gizmoType.GetMethod("DrawGizmos");
                if (info == null)
                {
                    Debug.LogError("Fail to find - draw method");
                }

                var o = Activator.CreateInstance(gizmoType);
                info.Invoke(o, new object[]{ gameObject, traitData, isSelected} );
            }
        }
    }
}
#endif
