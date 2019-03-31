using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Entities;
using Unity.Properties;
#if UNITY_EDITOR
using System.Dynamic;
using System.Text;
using Unity.Properties.Codegen;
using Unity.Properties.Codegen.CSharp;
using Unity.Properties.Serialization;
using UnityEditor;
#endif
using UnityEngine;
using UnityObject = UnityEngine.Object;

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

        public UnityObject ObjectValue
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
        UnityObject m_ObjectValue;

        public object GetValue(Type fieldType)
        {
            if (fieldType == typeof(Bool))
                return (Bool)BoolValue;
            if (fieldType == typeof(bool))
                return (Bool)BoolValue;
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

    [Serializable]
    class AliasDefinition : INamedData
    {
        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public IEnumerable<string> TraitTypes
        {
            get => m_TraitTypes;
            set => m_TraitTypes = value.ToList();
        }

        [SerializeField]
        string m_Name;

        // These fields are assigned in the editor, so ignore the warning that they are never assigned to
        #pragma warning disable 0649

        [UsesTraitDefinition]
        [SerializeField]
        List<string> m_TraitTypes;

        #pragma warning restore 0649
    }

    [Serializable]
    class EnumDefinition : INamedData
    {
        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public IEnumerable<string> Values
        {
            get => m_Values;
            set => m_Values = value.ToList();
        }

        [SerializeField]
        string m_Name;

        // These fields are assigned in the editor, so ignore the warning that they are never assigned to
        #pragma warning disable 0649

        [SerializeField]
        List<string> m_Values;

        #pragma warning restore 0649
    }

    [CreateAssetMenu(fileName = "New Domain", menuName = "AI/Domain Definition")]
    [Serializable]
    class DomainDefinition : ScriptableObject
    {
        public new string name => base.name.Replace(" ", string.Empty);

        public string PolicyGraphUpdateSystemName => $"{name}UpdateSystem";

        public event Action definitionChanged;

        public string GeneratedClassDirectory => $"{GeneratedDirectory}{name}/";

        internal string BaseDirectory { get; set; } = "Assets/AI.Planner/";

        string GeneratedDirectory => $"{BaseDirectory}Generated/";

        public const string AssemblyName = "AI.Planner.Domains";

        public IEnumerable<EnumDefinition> EnumDefinitions
        {
            get => m_EnumDefinitions;
            set => m_EnumDefinitions = value.ToList();
        }

        public IEnumerable<TraitDefinition> TraitDefinitions
        {
            get => m_TraitDefinitions;
            set => m_TraitDefinitions = value.ToList();
        }

        public IEnumerable<AliasDefinition> AliasDefinitions
        {
            get => m_AliasDefinitions;
            set => m_AliasDefinitions = value.ToList();
        }

        [SerializeField]
        List<EnumDefinition> m_EnumDefinitions = new List<EnumDefinition>();

        [SerializeField]
        List<AliasDefinition> m_AliasDefinitions = new List<AliasDefinition>();

        [SerializeField]
        List<TraitDefinition> m_TraitDefinitions = new List<TraitDefinition>();

        public void OnValidate()
        {
            definitionChanged?.Invoke();
        }

        public Type GetType(string typeName)
        {
            var plannerNamespace = typeof(PlannerSystem).Namespace;
            var type = Type.GetType($"{name}.{typeName},{AssemblyName}");
            if (type == null)
                type = Type.GetType($"{typeName},Assembly-CSharp");
            if (type == null)
                type = Type.GetType($"{typeof(TraitBasedDomain).Namespace}.{typeName},{plannerNamespace}");
            if (type == null)
                type = Type.GetType($"{plannerNamespace}.{typeName},{plannerNamespace}");
            if (type == null)
                type = Type.GetType(typeName);

            return type;
        }

#if UNITY_EDITOR
        public void GenerateClasses()
        {
            AssetDatabase.StartAssetEditing();
            GenerateAssemblyDefinition();

            foreach (var e in EnumDefinitions)
            {
                GenerateEnum(e);
            }

            foreach (var t in TraitDefinitions)
            {
                GenerateTraitClass(t);
            }

            GenerateECSSystem();

            GenerateDefines();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        void GenerateAssemblyDefinition()
        {
#if NET_4_6
            var asmdefPath = $"{BaseDirectory}{AssemblyName}.asmdef";
            Directory.CreateDirectory(Path.GetDirectoryName(asmdefPath));

            if (!File.Exists(asmdefPath))
            {
                dynamic asmdef = new ExpandoObject();
                asmdef.name = AssemblyName;
                asmdef.references = new[]
                {
                    typeof(PlannerSystem).Namespace,
                    typeof(PropertyBag).Namespace,
                    typeof(Entity).Namespace
                };
                var json = Json.SerializeObject(asmdef);
                File.WriteAllText(asmdefPath, json);
            }
#endif
        }

        public void GenerateDefines()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GeneratedDirectory));

            var defines = new StringBuilder();
            var definesPath = $"{BaseDirectory}csc.rsp";
            var domainDirectories = Directory.GetDirectories(GeneratedDirectory);
            foreach (var domainDirectory in domainDirectories)
            {
                var planDirectories = Directory.GetDirectories(domainDirectory);
                foreach (var planDirectory in planDirectories)
                {
                    var planName = new DirectoryInfo(planDirectory).Name;
                    defines.AppendLine($"-define:{planName.ToUpperInvariant()}_GENERATED");
                }

                var domainName = new DirectoryInfo(domainDirectory).Name;
                defines.AppendLine($"-define:{domainName.ToUpperInvariant()}_GENERATED");
            }

            File.WriteAllText(definesPath, defines.ToString());

            var projectDefinesPath = "Assets/csc.rsp";
            if (definesPath.StartsWith("Assets/"))
            {
                var definesReference = $"@{definesPath}";
                if (File.Exists(projectDefinesPath))
                {
                    var projectDefines = File.ReadAllText(projectDefinesPath);
                    if (!projectDefines.Contains(definesPath))
                        File.AppendAllText(projectDefinesPath, $"\n{definesReference}");
                }
                else
                {
                    File.WriteAllText(projectDefinesPath, definesReference);
                }
            }
        }

        void GenerateEnum(EnumDefinition enumDefinition)
        {
            if (enumDefinition == null)
                return;

            var path = $"{GeneratedClassDirectory}{enumDefinition.Name}.cs";
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var cw = new CodeWriter();
            using (cw.Scope($"namespace {name}"))
            {
                using (cw.Scope($"public enum {enumDefinition.Name}"))
                {
                    foreach (var value in enumDefinition.Values)
                    {
                        cw.Line($"{value},");
                    }
                }
            }

            File.WriteAllText(path, cw.ToString());
        }

        void GenerateTraitClass(TraitDefinition traitDefinition)
        {
            if (traitDefinition == null || !traitDefinition.Dynamic)
                return;


            var path = $"{GeneratedClassDirectory}{traitDefinition.Name}.cs";
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var className = traitDefinition.Name;
            var cw = new CodeWriter();
            cw.Line("using System;");
            cw.Line("using Unity.AI.Planner;");
            cw.Line("using Unity.AI.Planner.DomainLanguage.TraitBased;");
            cw.Line("using Unity.Entities;");
            cw.Line();

            using (cw.Scope($"namespace {name}"))
            {
                cw.Line("[Serializable]");
                using (cw.Scope($"public partial struct {className} : ITrait<{className}>, IEquatable<{className}>"))
                {
                    foreach (var field in traitDefinition.Fields)
                    {
                        cw.Line($"public {field.Type} {field.Name};");
                    }
                    cw.Line();

                    using (cw.Scope($"public bool Equals({className} other)"))
                    {
                        var firstFieldWritten = false;
                        foreach (var field in traitDefinition.Fields)
                        {
                            var fieldName = field.Name;
                            if (!firstFieldWritten)
                            {
                                cw.WriteIndent();
                                cw.Write($"return {fieldName} == other.{fieldName}");
                                cw.IncrementIndent();
                                firstFieldWritten = true;
                            }
                            else
                            {
                                cw.Line();
                                cw.WriteIndent();
                                cw.Write($"&& {fieldName} == other.{fieldName}");
                            }
                        }

                        if (firstFieldWritten)
                        {
                            cw.DecrementIndent();
                        }
                        else
                        {
                            // This class has no fields
                            cw.WriteIndent();
                            cw.Write("return true");
                        }

                        cw.Write(";");
                        cw.Line();
                    }
                    cw.Line();

                    using (cw.Scope("public override int GetHashCode()"))
                    {
                        var firstFieldWritten = false;
                        foreach (var field in traitDefinition.Fields)
                        {
                            var fieldName = field.Name;
                            if (!firstFieldWritten)
                            {
                                cw.Line("return 397");
                                cw.IncrementIndent();
                                cw.WriteIndent();
                                cw.Write($"^ {fieldName}.GetHashCode()");
                                firstFieldWritten = true;
                            }
                            else
                            {
                                cw.Line();
                                cw.WriteIndent();
                                cw.Write($"^ {fieldName}.GetHashCode()");
                            }
                        }

                        if (firstFieldWritten)
                        {
                            cw.DecrementIndent();
                        }
                        else
                        {
                            // This class has no fields
                            cw.WriteIndent();
                            cw.Write("return GetType().GetHashCode()");
                        }
                        cw.Write(";");
                        cw.Line();
                    }
                    cw.Line();

                    cw.Line("public object Clone() { return MemberwiseClone(); }");
                    cw.Line();

                    using (cw.Scope("public void SetField(string fieldName, object value)"))
                    {
                        if (traitDefinition.Fields.Any())
                        {
                            using (cw.Scope("switch (fieldName)"))
                            {
                                foreach (var field in traitDefinition.Fields)
                                {
                                    var fieldName = field.Name;
                                    var fieldType = field.Type;
                                    cw.Line($"case nameof({fieldName}):");
                                    cw.IncrementIndent();
                                    if (EnumDefinitions.Any(ed => ed.Name == fieldType))
                                        cw.Line($"{fieldName} = ({fieldType})Enum.ToObject(typeof({fieldType}), value);");
                                    else
                                        cw.Line($"{fieldName} = ({field.Type})value;");
                                    cw.Line("break;");
                                    cw.DecrementIndent();
                                }
                            }
                        }
                    }
                    cw.Line();

                    using (cw.Scope("public void SetComponentData(EntityManager entityManager, Entity domainObjectEntity)"))
                    {
                        cw.Line("SetTraitMask(entityManager, domainObjectEntity);");
                        cw.Line("entityManager.SetComponentData(domainObjectEntity, this);");
                    }
                    cw.Line();

                    using (cw.Scope("public void SetTraitMask(EntityManager entityManager, Entity domainObjectEntity)"))
                    {
                        cw.Line("var objectHash = entityManager.GetComponentData<HashCode>(domainObjectEntity);");
                        cw.Line($"objectHash.TraitMask = objectHash.TraitMask | (uint)TraitMask.{className};");
                        cw.Line("entityManager.SetComponentData(domainObjectEntity, objectHash);");
                    }
                }
            }
            File.WriteAllText(path, cw.ToString());
        }

        void GenerateECSSystem()
        {
            var cw = new CodeWriter();

            cw.Line("using System;");
            cw.Line("using System.Collections.Generic;");
            cw.Line("using Unity.AI.Planner;");
            cw.Line("using Unity.AI.Planner.DomainLanguage.TraitBased;");
            cw.Line("using Unity.Entities;");
            cw.Line();

            using (cw.Scope($"namespace {name}"))
            {
                // Trait mask enum definition
                cw.Line("[Flags]");
                using (cw.Scope("public enum TraitMask : uint  // Can change enum backing depending on number of traits."))
                {
                    cw.Line("None = 0,");
                    var flag = 1;
                    foreach (var trait in TraitDefinitions)
                    {
                        if (trait.Dynamic)
                        {
                            cw.Line($"{trait.Name} = {flag},");
                            flag *= 2;
                        }
                        else
                        {
                            var traitType = GetType(trait.Name);
                            var traitMaskField = traitType.GetField("TraitMask",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                            if (traitMaskField is null)
                            {
                                Debug.LogError($"Custom trait, {traitType}, does not contain field TraitMask.");
                                return;
                            }

                            cw.Line($"{trait.Name} = {traitMaskField.GetValue(null)},");
                        }
                    }
                }
                cw.Line();

                using (cw.Scope("public static class TraitMaskUtility"))
                {
                    using (cw.Scope("public static uint GetTraitMask(params Type[] traitFilter)"))
                    {
                        cw.Line("var traitMask = TraitMask.None;");

                        using (cw.Scope("foreach (var trait in traitFilter)"))
                        {
                            using (cw.Scope("switch (trait.Name)"))
                            {
                                foreach (var trait in TraitDefinitions)
                                {
                                    cw.Line($"case nameof({trait.Name}):");
                                    cw.IncrementIndent();
                                    cw.Line($"traitMask |= TraitMask.{trait.Name};");
                                    cw.Line("break;");
                                    cw.DecrementIndent();
                                    cw.Line();
                                }
                            }
                        }

                        cw.Line();
                        cw.Line("return (uint)traitMask;");
                    }
                }
                cw.Line();



                using (cw.Scope($"public class {name}UpdateSystem : PolicyGraphUpdateSystem"))
                {
                    cw.Line("List<Entity> m_EntityListLHS = new List<Entity>();");
                    cw.Line("List<Entity> m_EntityListRHS = new List<Entity>();");
                    cw.Line();

                    foreach (var trait in TraitDefinitions)
                    {
                        cw.Line($"ComponentType {trait.Name}Trait;");
                    }
                    cw.Line();

                    foreach (var trait in TraitDefinitions)
                    {
                        cw.Line($"bool zeroSized{trait.Name};");
                    }
                    cw.Line();

                    using (cw.Scope("protected override void OnCreateManager()"))
                    {
                        cw.Line("base.OnCreateManager();");
                        cw.Line();

                        foreach (var trait in TraitDefinitions)
                        {
                            cw.Line($"{trait.Name}Trait = ComponentType.ReadWrite<{trait.Name}>();");
                        }
                        cw.Line();

                        foreach (var trait in TraitDefinitions)
                        {
                            cw.Line($"zeroSized{trait.Name} = {trait.Name}Trait.IsZeroSized;");
                        }
                    }
                    cw.Line();

                    using (cw.Scope("internal override HashCode HashState(Entity stateEntity)"))
                    {
                        var template =
                            @"var domainObjectBuffer = EntityManager.GetBuffer<DomainObjectReference>(stateEntity);
                            var domainObjectHashCodes = GetComponentDataFromEntity<HashCode>();

                            // h = 3860031 + (h+y)*2779 + (h*y*2)   // from How to Hash a Set by Richard O’Keefe
                            var stateHashValue = 0;".Split(new[] { Environment.NewLine },
                                StringSplitOptions.None);

                        foreach (var line in template)
                        {
                            cw.Line(line.Trim());
                        }
                        cw.Line();

                        using(cw.Scope("for (var i = 0; i < domainObjectBuffer.Length; i++)"))
                        {
                            cw.Line("var objHash = domainObjectHashCodes[domainObjectBuffer[i].DomainObjectEntity].Value;");
                            cw.Line("stateHashValue = 3860031 + (stateHashValue + objHash) * 2779 + (stateHashValue * objHash * 2);");
                        }
                        cw.Line();

                        cw.Line("var stateHashCode = domainObjectHashCodes[stateEntity];");
                        cw.Line("var traitMask = (TraitMask) stateHashCode.TraitMask;");
                        cw.Line();

                        foreach (var trait in TraitDefinitions)
                        {
                            using (cw.Scope($"if ((traitMask & TraitMask.{trait.Name}) != 0)"))
                            {
                                cw.Line(
                                    $"var traitHash = EntityManager.GetComponentData<{trait.Name}>(stateEntity).GetHashCode();");
                                cw.Line(
                                    "stateHashValue = 3860031 + (stateHashValue + traitHash) * 2779 + (stateHashValue * traitHash * 2);");
                            }
                        }
                        cw.Line();

                        cw.Line("stateHashCode.Value = stateHashValue;");
                        cw.Line("return stateHashCode;");
                    }
                    cw.Line();

                    using (cw.Scope("protected override bool StateEquals(Entity lhsStateEntity, Entity rhsStateEntity)"))
                    {
                        cw.Line("m_EntityListLHS.Clear();");
                        cw.Line("m_EntityListRHS.Clear();");
                        cw.Line();

                        cw.Line("// Easy check is to make sure each state has the same number of domain objects");
                        cw.Line("var lhsObjectBuffer = EntityManager.GetBuffer<DomainObjectReference>(lhsStateEntity);");
                        cw.Line("var rhsObjectBuffer = EntityManager.GetBuffer<DomainObjectReference>(rhsStateEntity);");

                        using (cw.Scope("if (lhsObjectBuffer.Length != rhsObjectBuffer.Length)"))
                        {
                            cw.Line("return false;");
                        }
                        cw.Line();

                        using (cw.Scope("for (var i = 0; i < lhsObjectBuffer.Length; i++)"))
                        {
                            cw.Line("m_EntityListLHS.Add(lhsObjectBuffer[i].DomainObjectEntity);");
                            cw.Line("m_EntityListRHS.Add(rhsObjectBuffer[i].DomainObjectEntity);");
                        }
                        cw.Line();

                        cw.Line("// Next, check that each object has at least one match (by hash/checksum/trait mask)");
                        cw.Line("var entityHashCodes = GetComponentDataFromEntity<HashCode>();");
                        cw.Line("var lhsHashCode = entityHashCodes[lhsStateEntity];");
                        cw.Line("var rhsHashCode = entityHashCodes[rhsStateEntity];");

                        using (cw.Scope("if (lhsHashCode != rhsHashCode)"))
                        {
                            cw.Line("return false;");
                        }
                        cw.Line();

                        using (cw.Scope("for (var index = 0; index < m_EntityListLHS.Count; index++)"))
                        {
                            cw.Line("var entityLHS = m_EntityListLHS[index];");
                            cw.Line();
                            cw.Line("// Check for any objects with matching hash code.");
                            cw.Line("var hashLHS = entityHashCodes[entityLHS];");
                            cw.Line("var foundMatch = false;");
                            using (cw.Scope("for (var rhsIndex = 0; rhsIndex < m_EntityListRHS.Count; rhsIndex++)"))
                            {
                                cw.Line("var entityRHS = m_EntityListRHS[rhsIndex];");

                                using (cw.Scope("if (hashLHS == entityHashCodes[entityRHS])"))
                                {
                                    cw.Line("foundMatch = true;");
                                    cw.Line("break;");
                                }
                            }
                            cw.Line();

                            cw.Line("// No matching object found.");
                            using (cw.Scope("if (!foundMatch)"))
                            {
                                cw.Line("return false;");
                            }
                        }
                        cw.Line();

                        // todo do not need to grab zero-sized components
                        // GetComponentDataFromEntity for each trait
                        // Ex: var Locations = GetComponentDataFromEntity<Location>(true);
                        foreach (var trait in TraitDefinitions)
                        {
                            cw.Line($"var {trait.Name}s = GetComponentDataFromEntity<{trait.Name}>(true);");
                        }
                        cw.Line();

                        using (cw.Scope("while (m_EntityListLHS.Count > 0)"))
                        {
                            cw.Line("var entityLHS = m_EntityListLHS[0];");
                            cw.Line();
                            cw.Line("// Check for any objects with matching hash code.");
                            cw.Line("var hashLHS = entityHashCodes[entityLHS];");
                            cw.Line("var firstMatchIndex = -1;");
                            using (cw.Scope("for (var rhsIndex = 0; rhsIndex < m_EntityListRHS.Count; rhsIndex++)"))
                            {
                                cw.Line("var entityRHS = m_EntityListRHS[rhsIndex];");
                                using (cw.Scope("if (hashLHS == entityHashCodes[entityRHS])"))
                                {
                                    cw.Line("firstMatchIndex = rhsIndex;");
                                    cw.Line("break;");
                                }
                            }

                            using (cw.Scope("if (firstMatchIndex == -1)"))
                            {
                                cw.Line("return false;");
                            }
                            cw.Line();

                            cw.Line("var traitMask = (TraitMask)hashLHS.TraitMask;");
                            cw.Line();

                            // hasTrait checks
                            // Ex: var hasLocation = (traitMask & TraitMask.Location) != 0;
                            foreach (var trait in TraitDefinitions)
                            {
                                cw.Line($"var has{trait.Name} = (traitMask & TraitMask.{trait.Name}) != 0;");
                            }

                            cw.Line();
                            cw.Line("var foundMatch = false;");
                            using (cw.Scope(
                                "for (var rhsIndex = firstMatchIndex; rhsIndex < m_EntityListRHS.Count; rhsIndex++)"))
                            {
                                cw.Line("var entityRHS = m_EntityListRHS[rhsIndex];");
                                using (cw.Scope("if (hashLHS != entityHashCodes[entityRHS])"))
                                {
                                    cw.Line("continue;");
                                }
                                cw.Line();

                                // Equality checks per trait.
                                // Ex: if (hasLocation && !zeroSizedLocation && !Locations[entityLHS].Equals(Locations[entityRHS]))
                                //         continue;
                                foreach (var trait in TraitDefinitions)
                                {
                                    using (cw.Scope(
                                        $"if (has{trait.Name} && !zeroSized{trait.Name} && !{trait.Name}s[entityLHS].Equals({trait.Name}s[entityRHS]))")
                                    )
                                    {
                                        cw.Line("continue;");
                                    }
                                }
                                cw.Line();

                                cw.Line("m_EntityListLHS.RemoveAt(0);");
                                cw.Line("m_EntityListRHS.RemoveAt(rhsIndex);");
                                cw.Line("foundMatch = true;");
                                cw.Line("break;");
                            }

                            cw.Line();
                            using (cw.Scope("if (!foundMatch)"))
                            {
                                cw.Line("return false;");
                            }
                        }

                        cw.Line("return true;");
                    }
                }
            }

            var path = $"{GeneratedClassDirectory}{name}.cs";
            File.WriteAllText(path, cw.ToString());
        }
#endif
    }
}
