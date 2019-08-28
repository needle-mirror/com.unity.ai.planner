#if !UNITY_DOTSPLAYER
using System;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    internal class FieldValue
    {
        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public bool BoolValue
        {
            get => m_BoolValue;
            set => m_BoolValue = value;
        }

        public float FloatValue
        {
            get => m_FloatValue;
            set => m_FloatValue = value;
        }

        public long IntValue
        {
            get => m_IntValue;
            set => m_IntValue = value;
        }

        public string StringValue
        {
            get => m_StringValue;
            set => m_StringValue = value;
        }

        public Object ObjectValue
        {
            get => m_ObjectValue;
            set => m_ObjectValue = value;
        }

        [SerializeField]
        string m_Name;

        [SerializeField]
        bool m_BoolValue;

        [SerializeField]
        float m_FloatValue;

        [SerializeField]
        long m_IntValue;

        [SerializeField]
        string m_StringValue;

        [SerializeField]
        Object m_ObjectValue;

        public object GetValue(Type fieldType)
        {
            if (fieldType == typeof(bool))
                return BoolValue;
            if (fieldType == typeof(float))
                return FloatValue;
            if (fieldType == typeof(long))
                return IntValue;
            if (fieldType == typeof(string))
                return StringValue;
            if (fieldType.IsEnum)
                return IntValue;

            return ObjectValue;
        }
    }
}
#endif
