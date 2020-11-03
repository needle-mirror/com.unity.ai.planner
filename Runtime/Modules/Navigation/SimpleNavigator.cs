using System;
using System.Collections;
using Unity.AI.Planner.Controller;
using UnityEngine;

namespace Unity.AI.Planner.Navigation
{
    [AddComponentMenu("AI/Planner/Simple Navigator")]
    class SimpleNavigator : MonoBehaviour
    {
        GameObject m_Target;
        Coroutine m_NavigateTo;

        public IEnumerator NavigateTo(GameObject target)
        {
            if (m_NavigateTo != null)
                yield break;

            m_Target = target;

            while (m_Target != null && !IsTargetReachable())
            {
                transform.position = Vector3.Lerp(transform.position, m_Target.transform.position, 0.1f);
                transform.LookAt(m_Target.transform.position);
                yield return null;
            }
            transform.position = m_Target.transform.position;

            m_NavigateTo = null;
        }

        public void TeleportTo(GameObject target)
        {
            transform.position = target.transform.position;
            m_Target = null;
        }

        bool IsTargetReachable()
        {
            return Vector3.Distance(transform.position, m_Target.transform.position) < 0.1f;
        }
    }
}
