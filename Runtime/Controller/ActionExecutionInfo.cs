using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Planner.Controller;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.Serialization;

namespace UnityEngine.AI.Planner.Controller
{
    [Serializable]
    class ActionExecutionInfo : IActionExecutionInfo
    {
#pragma warning disable 0649
        [SerializeField]
        string m_ActionName;

        [SerializeField]
        PlanExecutorStateUpdateMode m_PlanExecutorStateUpdateMode;

        [SerializeField]
        string m_Method;

#if UNITY_EDITOR
        // This is a helper field to narrow the search of components to a single game object
        [SerializeField]
        GameObject m_SourceGameObject;
#endif
        [SerializeField]
        Component m_Source;

        [SerializeField]
        List<OperandValue> m_Arguments;
#pragma warning restore 0649

        MethodInfo m_methodInfo;

        public PlanExecutorStateUpdateMode PlanExecutorStateUpdateMode => m_PlanExecutorStateUpdateMode;

        MethodInfo MethodInfo
        {
            get
            {
                if (m_methodInfo == null)
                {
                    var sourceType = m_Source.GetType();
                    m_methodInfo = sourceType.GetMethod(m_Method);
                }

                return m_methodInfo;
            }
        }

        public bool IsValidForAction(string actionName)
        {
            return actionName == m_ActionName;
        }

        public Type GetParameterType(int parameterIndex)
        {
            var method = MethodInfo;
            if (method == null)
            {
                return null;
            }

            var parameters = method.GetParameters();
            if (parameterIndex >= parameters.Length)
            {
                Debug.LogError($"{m_Method} method doesn't take {parameterIndex} arguments");
                return null;
            }

            return parameters[parameterIndex].ParameterType;
        }

        public IEnumerable<string> GetArgumentValues()
        {
            return m_Arguments.Select(a => a.ToString());
        }

        public object InvokeMethod(object[] arguments)
        {
            return MethodInfo?.Invoke(m_Source, arguments);
        }
    }
}
