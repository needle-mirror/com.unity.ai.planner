﻿#if !UNITY_DOTSPLAYER
using System;
using System.Linq;
using System.Collections.Generic;
using Unity.AI.Planner.Utility;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityObject = UnityEngine.Object;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    [Serializable]
    class TraitData : ITraitData
    {
        internal TraitDefinition TraitDefinition => m_TraitDefinition;
        public string TraitDefinitionName => TraitDefinition != null ? TraitDefinition.Name : string.Empty;

#pragma warning disable 0649
        [SerializeField]
        TraitDefinition m_TraitDefinition;

        [SerializeField]
        List<FieldValue> m_FieldValues;
#pragma warning restore 0649

        Dictionary<string, FieldValue> m_Fields = new Dictionary<string, FieldValue>();
        Dictionary<string, Type> m_FieldTypes = new Dictionary<string, Type>();

        public void InitializeFieldValues()
        {
            if (m_TraitDefinition == null)
                return;

            m_FieldTypes.Clear();
            m_Fields.Clear();

            if (m_FieldValues == null)
                m_FieldValues = new List<FieldValue>();

            foreach (var f in m_TraitDefinition.Fields)
            {
                m_FieldTypes.Add(f.Name, TypeResolver.GetType(f.Type));

                if (!m_FieldValues.Any(v => v.Name == f.Name))
                    m_FieldValues.Add(new FieldValue(f.Name, f.DefaultValue));
            }

            foreach (var fv in m_FieldValues)
                m_Fields.Add(fv.Name, fv);
        }

        T GetValue<T>(string fieldName)
        {
            return (T)GetValue(fieldName);
        }

        public bool TryGetValue<T>(string fieldName, out T value) where T: class
        {
            if (m_FieldTypes.TryGetValue(fieldName, out var fieldType))
            {
                if (fieldType == typeof(T))
                {
                    value = GetValue<T>(fieldName);
                    return true;
                }

                if (m_Fields.TryGetValue(fieldName, out var fv))
                {
                    value = (T)fv.GetValue(typeof(T));
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void SetValue(string fieldName, object value)
        {
            if (value == null)
                return;

            var fieldType = m_FieldTypes[fieldName];

            if (value.GetType() != fieldType)
            {
                if (typeof(TraitBasedObjectId).IsAssignableFrom(fieldType))
                {
                    m_Fields[fieldName].StringValue = (string)value;
                    return;
                }
                throw new InvalidCastException(fieldName);
            }

            var fieldValue = m_Fields[fieldName];
            fieldValue.Name = fieldName;
            if (fieldType.IsEnum)
                fieldValue.IntValue = (int)value;
            else if (fieldType == typeof(bool))
                fieldValue.BoolValue = (bool)value;
            else if (fieldType == typeof(float))
                fieldValue.FloatValue = (float)value;
            else if (fieldType == typeof(long))
                fieldValue.IntValue = (long)value;
            else if (fieldType == typeof(string))
                fieldValue.StringValue = (string)value;
            else
                fieldValue.ObjectValue = (UnityObject)value;
        }

        public object GetValue(string fieldName)
        {
            object value = null;
            if (m_FieldTypes.TryGetValue(fieldName, out Type fieldType))
            {
                if (fieldType == null)
                    return default;

                if (m_Fields.TryGetValue(fieldName, out var fieldValue))
                    value = fieldValue.GetValue(fieldType);
            }
#if UNITY_EDITOR
            else
            {
                fieldType = m_TraitDefinition.Fields.FirstOrDefault(t => t.Name == fieldName)?.FieldType;
                value = m_FieldValues?.FirstOrDefault(v => v.Name == fieldName)?.GetValue(fieldType);
            }
#endif

            return value;
        }
    }
}
#endif
