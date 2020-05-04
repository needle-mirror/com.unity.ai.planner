using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Utility;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.CodeGen
{
    class CodeGenerator
    {
        CodeRenderer m_CodeRenderer = new CodeRenderer();
        List<string> m_GeneratedFilePaths = new List<string>();

        internal List<string> GenerateStateRepresentation(string outputPath)
        {
            m_GeneratedFilePaths.Clear();

            bool anyEnums = false;
            foreach (var e in PlannerAssetDatabase.EnumDefinitions)
            {
                GenerateEnum(e, outputPath);
                anyEnums = true;
            }

            foreach (var trait in PlannerAssetDatabase.TraitDefinitions)
            {
                if (TypeResolver.TryGetType(trait.FullyQualifiedName, out var traitType) && typeof(ICustomTrait).IsAssignableFrom(traitType)) // No codegen needed
                    continue;

                GenerateTrait(trait, outputPath, anyEnums);
            }

            foreach (var plan in PlannerAssetDatabase.PlanDefinitions)
            {
                if (plan.ActionDefinitions == null || !plan.ActionDefinitions.Any())
                {
                    Debug.LogWarning($"Skipping {plan.Name} generation that doesn't contain any action.");
                    continue;
                }

                GeneratePlanStateRepresentation(outputPath, plan);
            }

            SaveToFile(Path.Combine(outputPath, TypeResolver.StateRepresentationQualifier, "AssemblyInfo.cs"), $"using System.Runtime.CompilerServices; [assembly: InternalsVisibleTo(\"{TypeResolver.PlansQualifier}\")]");

            // Make a copy, so this is re-entrant
            return m_GeneratedFilePaths.ToList();
        }

        internal List<string> GeneratePlans(string outputPath, Assembly customAssembly)
        {
            m_GeneratedFilePaths.Clear();

            Type[] customTypes = ReflectionUtils.GetTypesFromAssembly(customAssembly);

            var anyEnums = PlannerAssetDatabase.EnumDefinitions.Any();
            foreach (var plan in PlannerAssetDatabase.PlanDefinitions)
            {
                if (plan.ActionDefinitions == null || !plan.ActionDefinitions.Any())
                {
                    continue;
                }

                foreach (var action in plan.ActionDefinitions)
                {
                    if (action != null)
                        GenerateAction(action, plan.Name, customTypes, outputPath, anyEnums);
                }

                foreach (var termination in plan.StateTerminationDefinitions)
                {
                    if (termination != null)
                        GenerateTermination(termination, plan.Name, customTypes, outputPath, anyEnums);
                }

                GenerateActionScheduler(plan, plan.Name, outputPath);

                GeneratePlanner(plan, plan.Name, outputPath, anyEnums);
            }

            // Make a copy, so this is re-entrant
            return m_GeneratedFilePaths.ToList();
        }

        void GenerateTrait(TraitDefinition trait, string outputPath, bool includeEnums = false)
        {
            var fields = trait.Fields.Select(p => new
            {
                field_type = p.Type,
                field_name = p.Name
            });

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateTrait, new
            {
                @namespace = TypeResolver.StateRepresentationQualifier,
                name = trait.Name,
                fields = fields,
                include_enums = includeEnums,
            });

            SaveToFile(Path.Combine(outputPath, TypeResolver.StateRepresentationQualifier, "Traits", $"{trait.Name}.cs"), result);
        }

        void GenerateEnum(EnumDefinition @enum, string outputPath)
        {
            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateEnum, new
            {
                @namespace = TypeResolver.StateRepresentationQualifier,
                Name = @enum.Name,
                Values = @enum.Values
            });

            SaveToFile(Path.Combine(outputPath, TypeResolver.StateRepresentationQualifier, "Traits", $"{@enum.Name}.cs"), result);
        }

        void GeneratePlanStateRepresentation(string outputPath, PlanDefinition plan)
        {
            var traits = plan.GetTraitsUsed().Select(p => new
            {
                name = p.Name,
                relations = p.Fields.Where(f => f.Type.EndsWith("ObjectId")).Select(f => new { name = f.Name }),
                attributes = p.Fields.Where(f => !f.Type.EndsWith("ObjectId")
                    && (f.FieldType == null // It's possible the type isn't available for reflection, so just assume it's blittable for now
                    || UnsafeUtility.IsUnmanaged(f.FieldType))).Select(t => new
                {
                    field_type = t.Type,
                    field_name = t.Name
                })
            });


            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateStateRepresentation, new
            {
                @namespace = $"{TypeResolver.StateRepresentationQualifier}.{plan.Name}",
                trait_list = traits,
                num_traits = traits.Count()
            });

            SaveToFile(Path.Combine(outputPath, TypeResolver.StateRepresentationQualifier, plan.Name, "PlanStateRepresentation.cs"), result);
        }

        void GenerateTermination(StateTerminationDefinition termination, string planName, Type[] customTypes, string outputPath, bool includeEnums)
        {
            var terminationName = termination.Name;

            var parameters = termination.Parameters.Select(p => new
            {
                @name = p.Name,
                required_traits = p.RequiredTraits,
                prohibited_traits = p.ProhibitedTraits,
            });

            var terminationCriteria = termination.Criteria.Where(p => !p.IsSpecialOperator(Operation.SpecialOperators.Custom));
            var criteriaTraits = terminationCriteria.Where(c => c.OperandA.Trait != null).Select(c => c.OperandA.Trait.Name)
                .Concat(terminationCriteria.Where(c => c.OperandB.Trait != null).Select(c => c.OperandB.Trait.Name))
                .Distinct();

            var customCriteriaList = termination.Criteria.Where(p => p.IsSpecialOperator(Operation.SpecialOperators.Custom));
            var customCriteria = customCriteriaList.Select(p => p.CustomOperatorType);

            var parameterNames = parameters.Select(p => p.@name).ToList();
            var criteria = terminationCriteria.Select(p => new
            {
                @operator = p.Operator,
                operand_a = GetPreconditionOperandString(p.OperandA, parameterNames),
                operand_b = GetPreconditionOperandString(p.OperandB, parameterNames),
                loop_index = Mathf.Max(parameterNames.FindIndex(name => name == p.OperandA.Parameter)
                    , parameterNames.FindIndex(name => name == p.OperandB.Parameter))
            });

            var customRewards = termination.CustomRewards.Select(c =>
            {
                var customRewardType = customTypes.FirstOrDefault(t => t.FullName == c.Typename);
                if (customRewardType != null)
                {
                    return new
                    {
                        @operator = c.Operator,
                        typename = customRewardType.FullName,
                        parameters = c.Parameters.Select((p, index) => new
                        {
                            index = parameterNames.IndexOf(p),
                            type = customRewardType.GetMethod("RewardModifier")?.GetParameters()[index].ParameterType
                        })
                    };
                }
                else
                {
                    Debug.LogError($"Couldn't resolve custom type {c.Typename} for termination {termination.Name}");
                }

                return null;
            }).Where(c => c != null);

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateTermination, new
            {
                @namespace = $"{TypeResolver.PlansQualifier}.{planName}",
                plan_name = planName,
                name = terminationName,
                parameter_list = parameters,
                criteria_traits = criteriaTraits.ToArray(),
                criteria_list = criteria.ToArray(),
                custom_criteria = customCriteria,
                reward_value = termination.TerminalReward,
                custom_rewards =  customRewards,
                include_enums = includeEnums,
                state_representation_qualifier = TypeResolver.StateRepresentationQualifier
            });
            SaveToFile(Path.Combine(outputPath, TypeResolver.PlansQualifier, planName, $"{terminationName}.cs"), result);
        }

        void GenerateActionScheduler(PlanDefinition definition, string planName, string outputPath)
        {
            int maxArgs = 0;
            foreach (var action in definition.ActionDefinitions)
            {
                if (action != null)
                    maxArgs = Math.Max(maxArgs, action.Parameters.Count());
            }

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateActionScheduler, new
            {
                @namespace = $"{TypeResolver.PlansQualifier}.{planName}",
                plan_name = definition.Name,
                actions = definition.ActionDefinitions,
                num_actions = definition.ActionDefinitions.Count(),
                num_args = maxArgs,
                state_representation_qualifier = TypeResolver.StateRepresentationQualifier
            });

            SaveToFile(Path.Combine(outputPath, TypeResolver.PlansQualifier, planName, "ActionScheduler.cs"), result);
        }

        void GeneratePlanner(PlanDefinition definition, string planName, string outputPath, bool includeEnums = false)
        {
            var customHeuristic = definition.CustomHeuristic;
            var heuristicTypeName = string.IsNullOrEmpty(customHeuristic) ? "DefaultHeuristic" : $"global::{customHeuristic}";

            var defaultHeuristic = new
            {
                lower = definition.DefaultHeuristicLower,
                avg = definition.DefaultHeuristicAverage,
                upper = definition.DefaultHeuristicUpper,
            };

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplatePlanExecutor, new
            {
                @namespace = $"{TypeResolver.PlansQualifier}.{planName}",
                plan_name = definition.Name,
                actions = definition.ActionDefinitions,
                traits = definition.GetTraitsUsed(),
                heuristic = heuristicTypeName,
                default_heuristic = defaultHeuristic,
                terminations = definition.StateTerminationDefinitions.Where(t => t != null).Select(t => t.Name),
                include_enums = includeEnums,
                state_representation_qualifier = TypeResolver.StateRepresentationQualifier
            });

            SaveToFile(Path.Combine(outputPath, TypeResolver.PlansQualifier, planName, $"{definition.name}Executor.cs"), result);
        }

        void GenerateAction(ActionDefinition action, string planName, Type[] customTypes, string outputPath, bool includeEnums = false)
        {
            var parameterNames = action.Parameters.Select(p => p.Name).ToList();
            var parameters = action.Parameters.Select((p, i) => new
            {
                @name = p.Name,
                required_traits = p.RequiredTraits,
                prohibited_traits = p.ProhibitedTraits,
                limit_count = p.LimitCount,
                limit_comparer = ExtractLimitComparerInformation(p.LimitComparerType, parameterNames.IndexOf(p.LimitComparerReference), customTypes)
            });

            var traitPreconditionList = action.Preconditions.Where(p => !p.IsSpecialOperator(Operation.SpecialOperators.Custom));
            var preconditions = traitPreconditionList.Select(p => new
            {
                @operator = p.Operator,
                operand_a = GetPreconditionOperandString(p.OperandA, parameterNames),
                operand_b = GetPreconditionOperandString(p.OperandB, parameterNames),
                loop_index = Mathf.Max(parameterNames.FindIndex(name => name == p.OperandA.Parameter)
                    , parameterNames.FindIndex(name => name == p.OperandB.Parameter))
            });

            var preconditionTraits = traitPreconditionList.Where(c => c.OperandA.Trait != null).Select(c => c.OperandA.Trait.Name)
                .Concat(traitPreconditionList.Where(c => c.OperandB.Trait != null).Select(c => c.OperandB.Trait.Name))
                .Distinct();

            var customPreconditionList = action.Preconditions.Where(p => p.IsSpecialOperator(Operation.SpecialOperators.Custom));
            var customPreconditions = customPreconditionList.Select(p => p.CustomOperatorType);

            var createdObjects = action.CreatedObjects.Select(c => new
            {
                name = c.Name,
                required_traits = c.RequiredTraits.Select(t => t.Name),
                prohibited_traits = c.ProhibitedTraits.Select(t => t.Name)
            });

            var requiredObjectBuffers = new HashSet<string>();
            var requiredTraitBuffers = new HashSet<string>();

            var objectModifiers = action.ObjectModifiers.Select(p => BuildModifierLines(action, p, ref requiredObjectBuffers, ref requiredTraitBuffers));

            var customRewards = action.CustomRewards.Select(c =>
            {
                var customRewardType = customTypes.FirstOrDefault(t => t.FullName == c.Typename);
                if (customRewardType != null)
                {
                    return new
                    {
                        @operator = c.Operator,
                        typename = c.Typename,
                        parameters = c.Parameters.Select((p, index) => new
                        {
                            index = parameterNames.IndexOf(p),
                            type = customRewardType.GetMethod("RewardModifier")?.GetParameters()[index].ParameterType
                        })
                    };
                }
                else
                {
                    Debug.LogError($"Couldn't resolve custom type {c.Typename} for action {action.Name}.");
                }

                return null;
            }).Where(c => c != null);

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateAction, new
            {
                @namespace = $"{TypeResolver.PlansQualifier}.{planName}",
                plan_name = planName,
                action_name = action.Name,
                parameter_list = parameters.ToList(),
                precondition_list = preconditions.ToList(),
                precondition_traits = preconditionTraits.ToList(),
                created_objects = createdObjects.ToArray(),
                created_object_names = createdObjects.Select(c => c.name),
                object_modifiers = objectModifiers.ToList(),
                reward_value = action.Reward,
                custom_preconditions = customPreconditions,
                custom_rewards =  customRewards,
                include_enums = includeEnums,
                removed_objects = action.RemovedObjects,
                required_object_buffers = requiredObjectBuffers.ToList(),
                required_trait_buffers = requiredTraitBuffers.ToList(),
                state_representation_qualifier = TypeResolver.StateRepresentationQualifier
            });

            SaveToFile(Path.Combine(outputPath, TypeResolver.PlansQualifier, planName, $"{action.Name}.cs"), result);
        }

        string[] BuildModifierLines(ActionDefinition action, Operation operation, ref HashSet<string> requiredObjectBuffers, ref HashSet<string> requiredTraitBuffers)
        {
            const string prefixNew = "new";
            const string prefixOriginal = "original";

            List<string> modifierLines = new List<string>();

            var @operator = operation.Operator;
            var operandA = operation.OperandA;
            var operandB = operation.OperandB;

            switch (@operator)
            {
                case nameof(Operation.SpecialOperators.Custom):
                {
                    modifierLines.Add($"new global::{operation.CustomOperatorType}().ApplyCustomActionEffectsToState(originalState, action, newState);");
                }
                    break;
                case nameof(Operation.SpecialOperators.Add):
                {
                    if ((string.IsNullOrEmpty(operandA.Parameter) || operandB.Trait == null))
                    {
                        throw new ArgumentException("Invalid operands for an Add trait operator");
                    }
                    var parameter = operandA.Parameter;
                    var trait = operandB.Trait.Name;

                    requiredObjectBuffers.Add(parameter);

                    modifierLines.Add($"newState.SetTraitOnObject<{trait}>(default({trait}), ref original{parameter}Object);");
                }
                    break;
                case nameof(Operation.SpecialOperators.Remove):
                {
                    if ((string.IsNullOrEmpty(operandA.Parameter) || operandB.Trait == null))
                    {
                        throw new ArgumentException("Invalid operands for a Remove trait operator");
                    }
                    var parameter = operandA.Parameter;
                    var trait = operandB.Trait.Name;

                    requiredObjectBuffers.Add(parameter);

                    modifierLines.Add($"newState.RemoveTraitOnObject<{trait}>(ref original{parameter}Object);");
                }
                    break;
                default:
                {
                    if (string.IsNullOrEmpty(operandA.Parameter) || operandA.Trait == null)
                    {
                        throw new ArgumentException("Invalid operands for a trait modifier");
                    }

                    var parameterNames = action.Parameters.Select(p => p.Name).ToList();

                    var paramA = operandA.Parameter;
                    var traitA = operandA.Trait.Name;
                    var fieldA = operandA.TraitFieldName;

                    requiredTraitBuffers.Add(traitA);

                    bool originalObject = parameterNames.Contains(paramA);
                    var objectAPrefix = originalObject ? prefixOriginal : prefixNew;

                    if (originalObject)
                    {
                        requiredObjectBuffers.Add(paramA);
                    }

                    modifierLines.Add($"var @{traitA} = new{traitA}Buffer[{objectAPrefix}{paramA}Object.{traitA}Index];");

                    if (operandB.Trait == null)
                    {
                        if (operandB.Enum != null)
                        {
                            modifierLines.Add($"@{traitA}.@{fieldA} {@operator} {operandB.Enum.Name}.{operandB.Value};");
                        }
                        else if (parameterNames.Contains(operandB.Parameter))
                        {
                            requiredObjectBuffers.Add(operandB.Parameter);
                            modifierLines.Add($"@{traitA}.@{fieldA} {@operator} originalState.GetTraitBasedObjectId(original{operandB.Parameter}Object);");
                        }
                        else if (action.CreatedObjects.Any(c => c.Name == operandB.Parameter))
                        {
                            modifierLines.Add($"@{traitA}.@{fieldA} {@operator} new{operandB.Parameter}ObjectId;");
                        }
                        else
                        {
                            modifierLines.Add($"@{traitA}.@{fieldA} {@operator} {operandB.Value};");
                        }
                    }
                    else
                    {
                        string traitB = operandB.Trait.Name;
                        string fieldB = operandB.TraitFieldName;

                        requiredTraitBuffers.Add(traitB);

                        var objectBPrefix = parameterNames.Contains(operandB.Parameter) ? prefixOriginal : prefixNew;
                        if (objectBPrefix == prefixOriginal)
                        {
                            requiredObjectBuffers.Add(operandB.Parameter);
                        }

                        modifierLines.Add($"@{traitA}.{fieldA} {@operator} new{traitB}Buffer[{objectBPrefix}{operandB.Parameter}Object.{traitB}Index].{fieldB};");
                    }

                    modifierLines.Add($"new{traitA}Buffer[{objectAPrefix}{paramA}Object.{traitA}Index] = @{traitA};");
                }
                    break;
            }

            return modifierLines.ToArray();
        }

        static object ExtractLimitComparerInformation(string comparerTypeName, int parameterReferenceIndex, Type[] customTypes)
        {
            if (string.IsNullOrEmpty(comparerTypeName))
                return null;

            var comparerType = customTypes.FirstOrDefault(t => t.FullName == comparerTypeName);
            if (comparerType == null)
                return null;

            var parameterComparerType = comparerType.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IParameterComparer<>).Name);
            if (parameterComparerType == null)
                return null;

            var parameterComparerWithReferenceType = comparerType.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IParameterComparerWithReference<,>).Name);
            if (parameterComparerWithReferenceType == null)
            {
                return new
                {
                    type = comparerTypeName,
                    trait = "",
                    reference_index = -1,
                };
            }

            if (parameterReferenceIndex < 0)
            {
                Debug.LogWarning($"Missing a reference for the parameter comparer type {comparerTypeName}.");
                return null;
            }

            return new
            {
                type = comparerTypeName,
                trait = parameterComparerWithReferenceType.GetGenericArguments()[1].Name,
                reference_index = parameterReferenceIndex,
            };
        }

        internal void GenerateAsmRef(string path, string assemblyName)
        {
            File.WriteAllText($"{Path.Combine(path, assemblyName)}.asmref", m_CodeRenderer.RenderTemplate(
                PlannerResources.instance.TemplateAsmRef, new
                {
                    assembly = assemblyName
                }));
        }

        internal void GeneratePackage(string path, string assemblyName)
        {
            File.WriteAllText(Path.Combine(path,"package.json"), m_CodeRenderer.RenderTemplate(
                PlannerResources.instance.TemplatePackage, new
                {
                    assembly = assemblyName.Split('.').Last()
                }));
        }

        void SaveToFile(string filePath, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, text);

            m_GeneratedFilePaths.Add(filePath);
        }

        static string GetPreconditionOperandString(OperandValue operand, List<string> parameterNames)
        {
            if (operand.Trait == null)
            {
                if (operand.Enum != null)
                    return $"{operand.Enum.Name}.{operand.Value}";

                if (parameterNames.Contains(operand.Parameter))
                    return $"stateData.GetTraitBasedObjectId({operand.Parameter}Index)";

                return operand.Value;
            }

            var precondition =  $"{operand.Trait.Name}Buffer[{operand.Parameter}Object.{operand.Trait.Name}Index]";
            if (!string.IsNullOrEmpty(operand.TraitFieldName))
                precondition +=  $".{operand.TraitFieldName}";

            return precondition;
        }
    }
}
