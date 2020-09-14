#if !UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using Unity.AI.Planner.Traits;
using Unity.AI.Planner.Utility;
using Unity.Semantic.Traits;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using Unity.Semantic.Traits.Utility;
using Unity.DynamicStructs;
using UnityEditor;
#endif

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Definition of a trait that can be used to specify the quality of an object
    /// </summary>
    [Serializable]
    public class OldTraitDefinition : ScriptableObject
    {
        const int k_DefaultUniqueId = 100;

        internal string Name
        {
            get
            {
                if (string.IsNullOrEmpty(m_CachedName))
                {
                    m_CachedName = name;
                    m_CachedResolvedName = TypeResolver.ToTypeNameCase(m_CachedName);
                    return m_CachedResolvedName;
                }

                return string.IsNullOrEmpty(m_CachedResolvedName) ?
                    m_CachedResolvedName = TypeResolver.ToTypeNameCase(name)
                    : m_CachedResolvedName;
            }
        }

        string m_CachedName;
        string m_CachedResolvedName;

        /// <summary>
        /// Fully qualified name of the trait type
        /// </summary>
        public string FullyQualifiedName => string.IsNullOrEmpty(m_Namespace)
            ? $"{TypeHelper.StateRepresentationQualifier}.{Name}"
            : $"{m_Namespace}.{Name}";

        internal List<TraitDefinitionField> Fields
        {
            get => m_Fields;
            set => m_Fields = value.ToList();
        }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        [SerializeField]
        string m_Namespace;
#pragma warning restore CS0649

        [SerializeField]
        List<TraitDefinitionField> m_Fields = new List<TraitDefinitionField>();

        [SerializeField]
        int m_NextFieldUniqueId = k_DefaultUniqueId;

        /// <summary>
        /// Returns the name of the trait definition
        /// </summary>
        /// <returns>A string that represents the trait definition</returns>
        public override string ToString()
        {
            return Name;
        }

        internal TraitDefinitionField GetField(int fieldId)
        {
            return Fields.FirstOrDefault(f => f.UniqueId == fieldId);
        }

        internal string GetFieldName(int fieldId)
        {
            return Fields.FirstOrDefault(f => f.UniqueId == fieldId)?.Name;
        }

        #if UNITY_EDITOR
        internal TraitDefinition GetNewDefinition()
        {
            var oldPath = AssetDatabase.GetAssetPath(this);
            var newDir = Path.Combine(Path.GetDirectoryName(oldPath), "New");
            var newDefinition = AssetDatabase.LoadAssetAtPath<TraitDefinition>(Path.Combine(newDir, Path.GetFileName(oldPath)));
            if (newDefinition == null)
                Debug.LogError($"Missing new TraitDefinition for {name}");
            else
                return newDefinition;

            return null;
        }

        [ContextMenu("Convert")]
        void Convert()
        {
            var definition = DynamicStruct.Create<TraitDefinition>();

            // Have to save the asset first before adding properties
            var assetPath = AssetDatabase.GetAssetPath(this);
            var directory = Path.Combine(Path.GetDirectoryName(assetPath), "New");
            var fileName = Path.GetFileName(assetPath);

            var newPath = Path.Combine(directory, fileName);
            Directory.CreateDirectory(directory);
            AssetDatabase.CreateAsset(definition, newPath);

            foreach (var field in m_Fields)
            {
                var fieldType = field.FieldType != null ? field.FieldType :
                    TypeResolver.TryGetType(field.Type, out var lookupType) ? lookupType : null;
                if (fieldType == null)
                {
                    var fieldSplit = field.Type.Split('.');
                    TypeResolver.TryGetType(fieldSplit[fieldSplit.Length - 1], out fieldType);
                }

                if (fieldType != null)
                {
                    var type = fieldType == typeof(SemanticObjectData) || fieldType == typeof(TraitBasedObjectId) ?
                        typeof(GameObject) : fieldType;
                    var property = definition.CreateProperty(type, field.Name);
                    var defaultValue = field.DefaultValue.GetValue(type);
                    if (!defaultValue.Equals(null) && (defaultValue is long defaultNum) && defaultNum != 0)
                        property.SetValue(defaultValue);
                }
                else
                {
                    Debug.LogError($"Couldn't find type {field.Type}");
                }
            }
        }

        const string k_BuildMenuTitle = "AI/Planner/Upgrader/2. Convert Old Traits to New";
        [MenuItem(k_BuildMenuTitle, true)]
        public static bool ConvertAllValidate()
        {
            return !EditorApplication.isCompiling;
        }

        [MenuItem(k_BuildMenuTitle, priority = 2)]
        static void ConvertAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var oldDefinition in ConversionUtility.AllAssetsOfType<OldTraitDefinition>())
                {
                    oldDefinition.Convert();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        void OnValidate()
        {
            // Validate that field's unique Id are set correctly
            var usedId = new List<int>();
            foreach (var field in m_Fields)
            {
                if (field.UniqueId >= m_NextFieldUniqueId)
                {
                    m_NextFieldUniqueId = field.UniqueId + 1;
                }
                else if (field.UniqueId == 0)
                {
                    field.UniqueId = m_NextFieldUniqueId++;
                }

                if (usedId.Contains(field.UniqueId))
                {
                    field.UniqueId = m_NextFieldUniqueId++;
                }

                usedId.Add(field.UniqueId);
            }
        }
#endif
    }
}
#endif
