using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.AI.Planner.Traits;
using Unity.AI.Planner.Utility;
using Unity.Semantic.Traits.Utility;
using Unity.Entities;
using Unity.Semantic.Traits;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using TraitDefinition = Unity.Semantic.Traits.TraitDefinition;

namespace UnityEditor.AI.Planner.CodeGen
{
    class CodeGenerator
    {
        CodeRenderer m_CodeRenderer = new CodeRenderer();
        List<string> m_GeneratedFilePaths = new List<string>();

        internal List<string> GenerateStateRepresentation(string outputPath)
        {
            m_GeneratedFilePaths.Clear();

            bool anyEnums = PlannerAssetDatabase.EnumDefinitions.Any();

            foreach (var trait in PlannerAssetDatabase.TraitDefinitions)
            {
                if (TypeResolver.TryGetType($"{TypeHelper.TraitBasedQualifier}.{trait.name}", out var traitType)
                    && typeof(ICustomTrait).IsAssignableFrom(traitType)) // No codegen needed
                    continue;

                GenerateTrait(trait, outputPath, anyEnums);
            }

            foreach (var plan in PlannerAssetDatabase.ProblemDefinitions)
            {
                if (plan.ActionDefinitions == null || !plan.ActionDefinitions.Any())
                {
                    Debug.LogWarning($"Skipping {plan.Name} generation that doesn't contain any action.");
                    continue;
                }

                GeneratePlanStateRepresentation(outputPath, plan);
            }

            SaveToFile(Path.Combine(outputPath, TypeHelper.StateRepresentationQualifier, "AssemblyInfo.cs"), $"using System.Runtime.CompilerServices; [assembly: InternalsVisibleTo(\"{TypeHelper.PlansQualifier}\")]");

            // Make a copy, so this is re-entrant
            return m_GeneratedFilePaths.ToList();
        }

        internal List<string> GeneratePlans(string outputPath, Assembly customAssembly)
        {
            m_GeneratedFilePaths.Clear();

            Type[] customTypes = ReflectionUtils.GetTypesFromAssembly(customAssembly);

            var anyEnums = PlannerAssetDatabase.EnumDefinitions.Any();
            foreach (var plan in PlannerAssetDatabase.ProblemDefinitions)
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

                GenerateSystemsProvider(plan, plan.Name, outputPath);
            }

            // Make a copy, so this is re-entrant
            return m_GeneratedFilePaths.ToList();
        }

        static string GetRuntimePropertyType(TraitPropertyDefinition property)
        {
            if (property.Type == typeof(GameObject) || property.Type == typeof(Entity))
                return TypeResolver.GetUnmangledName(typeof(TraitBasedObjectId));

            var descriptor = Semantic.Traits.CodeGen.CodeGenerator.GetTraitDescriptorData(property);
            return descriptor?.RuntimeType;
        }

        void GenerateTrait(TraitDefinition trait, string outputPath, bool includeEnums = false)
        {
            var fields = trait.Properties.Select(p => new
            {
                field_type = GetRuntimePropertyType(p),
                field_name = p.Name
            });

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateTrait, new
            {
                @namespace = TypeHelper.StateRepresentationQualifier,
                name = trait.name,
                fields = fields,
                include_enums = includeEnums,
            });

            SaveToFile(Path.Combine(outputPath, TypeHelper.StateRepresentationQualifier, "Traits", $"{trait.name}.cs"), result);
        }

        void GeneratePlanStateRepresentation(string outputPath, ProblemDefinition problemDefinition)
        {
            var traits = problemDefinition.GetTraitsUsed()
                    .Append(PlannerAssetDatabase.TraitDefinitions.FirstOrDefault(t => t.name == nameof(PlanningAgent)))
                    .Distinct()
                    .Where(t => t != null)
                    .Select(t => new
                    {
                        name = t.name,
                        relations = t.Properties.Where(p =>
                                p.Type == typeof(GameObject) || p.Type == typeof(Entity))
                            .Select(p => new { name = p.Name }),
                        attributes = t.Properties.Where(p =>
                                p.Type != typeof(GameObject) && p.Type != typeof(Entity) && GetRuntimePropertyType(p) != null)
                            .Select(p => new
                        {
                            field_type = GetRuntimePropertyType(p),
                            field_name = p.Name
                        })
                    });

            int numTraits = traits.Count();
            int traitsAlignmentSize = ((numTraits + 3) / 4) * 4;

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateStateRepresentation, new
            {
                @namespace = $"{TypeHelper.StateRepresentationQualifier}.{problemDefinition.Name}",
                trait_list = traits,
                num_traits = numTraits,
                alignment_size = traitsAlignmentSize
            });

            SaveToFile(Path.Combine(outputPath, TypeHelper.StateRepresentationQualifier, problemDefinition.Name, "PlanStateRepresentation.cs"), result);
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
            var criteriaTraits = terminationCriteria.Where(c => c.OperandA.Trait != null).Select(c => c.OperandA.Trait.name)
                .Concat(terminationCriteria.Where(c => c.OperandB.Trait != null).Select(c => c.OperandB.Trait.name))
                .Distinct();

            var customCriteriaList = termination.Criteria.Where(p => p.IsSpecialOperator(Operation.SpecialOperators.Custom));
            var customCriteria = customCriteriaList.Select(p => p.CustomOperatorType);

            var parameterNames = parameters.Select(p => p.@name).ToList();
            var criteria = terminationCriteria.Select(p => new
            {
                @operator = p.Operator,
                operand_a = GetPreconditionOperandString(p.OperandA, p.Operator, parameterNames),
                operand_b = GetPreconditionOperandString(p.OperandB, p.Operator, parameterNames),
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
                @namespace = $"{TypeHelper.PlansQualifier}.{planName}",
                plan_name = planName,
                name = terminationName,
                parameter_list = parameters,
                criteria_traits = criteriaTraits.ToArray(),
                criteria_list = criteria.ToArray(),
                custom_criteria = customCriteria,
                reward_value = termination.TerminalReward,
                custom_rewards =  customRewards,
                include_enums = includeEnums,
                state_representation_qualifier = TypeHelper.StateRepresentationQualifier
            });
            SaveToFile(Path.Combine(outputPath, TypeHelper.PlansQualifier, planName, $"{terminationName}.cs"), result);
        }

        void GenerateActionScheduler(ProblemDefinition definition, string planName, string outputPath)
        {
            int maxArgs = 0;
            foreach (var action in definition.ActionDefinitions)
            {
                if (action != null)
                    maxArgs = Math.Max(maxArgs, action.Parameters.Count());
            }

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateActionScheduler, new
            {
                @namespace = $"{TypeHelper.PlansQualifier}.{planName}",
                plan_name = definition.Name,
                actions = definition.ActionDefinitions,
                num_actions = definition.ActionDefinitions.Count(),
                num_args = maxArgs,
                state_representation_qualifier = TypeHelper.StateRepresentationQualifier
            });

            SaveToFile(Path.Combine(outputPath, TypeHelper.PlansQualifier, planName, "ActionScheduler.cs"), result);
        }

        void GeneratePlanner(ProblemDefinition definition, string planName, string outputPath, bool includeEnums = false)
        {
            var customCumulativeRewardEstimator = definition.CustomCumulativeRewardEstimator;
            var rewardEstimatorTypeName = string.IsNullOrEmpty(customCumulativeRewardEstimator) ? "DefaultCumulativeRewardEstimator" : $"global::{customCumulativeRewardEstimator}";

            var defaultCumulativeRewardEstimator = new
            {
                lower = definition.DefaultEstimateLower,
                avg = definition.DefaultEstimateAverage,
                upper = definition.DefaultEstimateUpper,
            };

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplatePlanExecutor, new
            {
                @namespace = $"{TypeHelper.PlansQualifier}.{planName}",
                plan_name = definition.Name,
                actions = definition.ActionDefinitions,
                traits = definition.GetTraitsUsed(),
                reward_estimator = rewardEstimatorTypeName,
                default_reward_estimate = defaultCumulativeRewardEstimator,
                terminations = definition.StateTerminationDefinitions.Where(t => t != null).Select(t => t.Name),
                include_enums = includeEnums,
                state_representation_qualifier = TypeHelper.StateRepresentationQualifier
            });

            SaveToFile(Path.Combine(outputPath, TypeHelper.PlansQualifier, planName, $"{definition.name}Executor.cs"), result);
        }

        void GenerateSystemsProvider(ProblemDefinition definition, string planName, string outputPath)
        {
            var customCumulativeRewardEstimator = definition.CustomCumulativeRewardEstimator;
            var rewardEstimatorTypeName = string.IsNullOrEmpty(customCumulativeRewardEstimator) ? "DefaultCumulativeRewardEstimator" : $"global::{customCumulativeRewardEstimator}";

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateSystemsProvider, new
            {
                @namespace = $"{TypeHelper.PlansQualifier}.{planName}",
                plan_name = definition.Name,
                heuristic = rewardEstimatorTypeName,
                state_representation_qualifier = TypeHelper.StateRepresentationQualifier
            });

            SaveToFile(Path.Combine(outputPath, TypeHelper.PlansQualifier, planName, "PlannerSystemsProvider.cs"), result);
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
                @operator = p.Operator.Contains("contains") ? ".Contains" : p.Operator,
                inverse_condition = p.Operator.Contains("!contains"),
                operand_a = GetPreconditionOperandString(p.OperandA, p.Operator, parameterNames),
                operand_b = GetPreconditionOperandString(p.OperandB, p.Operator, parameterNames),
                is_list_method = p.OperandA.Trait != null && p.OperandA.TraitProperty != null
                                                             && IsListType(p.OperandA.TraitProperty.Type)
                                                             && p.Operator.Contains("contains"),
                loop_index = Mathf.Max(parameterNames.FindIndex(name => name == p.OperandA.Parameter),
                    parameterNames.FindIndex(name => name == p.OperandB.Parameter))
            });

            var preconditionTraits = traitPreconditionList.Where(c => c.OperandA.Trait != null).Select(c => c.OperandA.Trait.name)
                .Concat(traitPreconditionList.Where(c => c.OperandB.Trait != null).Select(c => c.OperandB.Trait.name))
                .Distinct();

            var customPreconditionList = action.Preconditions.Where(p => p.IsSpecialOperator(Operation.SpecialOperators.Custom));
            var customPreconditions = customPreconditionList.Select(p => p.CustomOperatorType);

            var createdObjects = action.CreatedObjects.Select(c => new
            {
                name = c.Name,
                required_traits = c.RequiredTraits.Select(t => t.name),
                prohibited_traits = c.ProhibitedTraits.Select(t => t.name)
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
                @namespace = $"{TypeHelper.PlansQualifier}.{planName}",
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
                state_representation_qualifier = TypeHelper.StateRepresentationQualifier
            });

            SaveToFile(Path.Combine(outputPath, TypeHelper.PlansQualifier, planName, $"{action.Name}.cs"), result);
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
                    var trait = operandB.Trait.name;

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
                    var trait = operandB.Trait.name;

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
                    var traitA = operandA.Trait.name;
                    var fieldA = operandA.TraitProperty.Name;

                    requiredTraitBuffers.Add(traitA);

                    bool originalObject = parameterNames.Contains(paramA);
                    var objectAPrefix = originalObject ? prefixOriginal : prefixNew;

                    if (originalObject)
                    {
                        requiredObjectBuffers.Add(paramA);
                    }

                    modifierLines.Add($"var @{traitA} = new{traitA}Buffer[{objectAPrefix}{paramA}Object.{traitA}Index];");

                    var operandAType = operandA.TraitProperty.Type;
                    var isListOperation = operandAType != null && IsListType(operandAType);

                    if (operandB.Trait == null)
                    {
                        if (operandB.Enum != null)
                        {
                            modifierLines.Add($"@{traitA}.@{fieldA} {@operator} {operandB.Enum.name}.{operandB.Value};");
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
                        else if (isListOperation)
                        {
                            if (@operator == "clear")
                                modifierLines.Add($"@{traitA}.@{fieldA}.Clear();");
                        }
                        else
                        {
                            modifierLines.Add($"@{traitA}.@{fieldA} {@operator} {operandB.Value};");
                        }
                    }
                    else
                    {
                        string traitB = operandB.Trait.name;
                        string fieldB = operandB.TraitProperty.Name;

                        requiredTraitBuffers.Add(traitB);

                        var objectBPrefix = parameterNames.Contains(operandB.Parameter) ? prefixOriginal : prefixNew;
                        if (objectBPrefix == prefixOriginal)
                        {
                            requiredObjectBuffers.Add(operandB.Parameter);
                        }

                        if (isListOperation)
                        {
                            if (@operator == "=")
                            {
                                modifierLines.Add($"@{traitA}.{fieldA}.Clear();");
                                modifierLines.Add($"var list = new{traitB}Buffer[{objectBPrefix}{operandB.Parameter}Object.{traitB}Index].{fieldB};");
                                modifierLines.Add($"for (var i = 0; i < list.Length; i++)");
                                modifierLines.Add($"\t@{traitA}.{fieldA}.Add(list[i]);");
                            }
                            else
                            {
                                @operator = @operator == "+=" ? ".Add" : ".Remove";
                                modifierLines.Add($"@{traitA}.{fieldA}{@operator}(new{traitB}Buffer[{objectBPrefix}{operandB.Parameter}Object.{traitB}Index].{fieldB});");
                            }
                        }
                        else
                        {
                            modifierLines.Add($"@{traitA}.{fieldA} {@operator} new{traitB}Buffer[{objectBPrefix}{operandB.Parameter}Object.{traitB}Index].{fieldB};");
                        }
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

        static string GetPreconditionOperandString(OperandValue operand, string @operator, List<string> parameterNames)
        {
            if (operand.Trait == null)
            {
                if (operand.Enum != null)
                    return $"{operand.Enum.name}.{operand.Value}";

                if (parameterNames.Contains(operand.Parameter))
                    return $"stateData.GetTraitBasedObjectId({operand.Parameter}Index)";

                return operand.Value;
            }

            var precondition =  $"{operand.Trait.name}Buffer[{operand.Parameter}Object.{operand.Trait.name}Index]";
            if (!string.IsNullOrEmpty(operand.TraitProperty.Name))
            {
                precondition += $".{operand.TraitProperty.Name}";

                var operandPropertyType = operand.TraitProperty.Type;
                if (IsListType(operandPropertyType))
                {
                    if (IsComparisonOperator(@operator))
                        precondition += ".Length";
                }
            }

            return precondition;
        }

        static bool IsListType(Type type)
        {
            return type.IsGenericType && typeof(List<>).IsAssignableFrom(type.GetGenericTypeDefinition());
        }

        static bool IsComparisonOperator(string @operator)
        {
            switch (@operator)
            {
                case "==":
                case "!=":
                case "<":
                case ">":
                case "<=":
                case ">=":
                    return true;
            }

            return false;
        }
    }
}
