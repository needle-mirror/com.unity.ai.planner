#if !UNITY_DOTSPLAYER
using Unity.AI.Planner.DomainLanguage.TraitBased;
using System;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class FieldValue
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

        public FieldValue(string name, FieldValue value)
        {
            m_Name = name;
            m_BoolValue = value.BoolValue;
            m_FloatValue = value.FloatValue;
            m_IntValue = value.IntValue;
            m_StringValue = value.StringValue;
            m_ObjectValue = value.ObjectValue;
        }

        public object GetValue(Type fieldType)
        {
            if (fieldType == typeof(bool))
                return BoolValue;
            if (fieldType == typeof(float))
                return FloatValue;
            if (fieldType == typeof(int))
                return (int)IntValue;
            if (fieldType == typeof(long))
                return IntValue;
            if (fieldType == typeof(string))
                return StringValue;
            if (fieldType.IsEnum)
                return IntValue;
            if (typeof(TraitBasedObjectId).IsAssignableFrom(fieldType))
                return StringValue;

            return ObjectValue;
        }
    }
}
#endif
