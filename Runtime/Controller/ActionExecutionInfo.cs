using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Planner.Traits;
using UnityEngine;

namespace Unity.AI.Planner.Controller
{
    /// <summary>
    /// Serialized data pertaining to the execution of plan actions.
    /// </summary>
    [Serializable]
    public class ActionExecutionInfo
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

        /// <summary>
        /// An enum defining whether the plan executor should be updated with world state data or with the predicted
        /// state from the current plan.
        /// </summary>
        public PlanExecutorStateUpdateMode PlanExecutorStateUpdateMode
        {
            get => m_PlanExecutorStateUpdateMode;
            set => m_PlanExecutorStateUpdateMode = value;
        }

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

        internal bool IsValidForAction(string actionName)
        {
            return actionName == m_ActionName;
        }

        internal Type GetParameterType(int parameterIndex)
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

        internal IEnumerable<string> GetArgumentValues()
        {
            return m_Arguments.Select(a => a.ToString());
        }

        internal object InvokeMethod(object[] arguments)
        {
            return MethodInfo?.Invoke(m_Source, arguments);
        }
    }
}
