using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        internal List<string> GenerateDomain(string outputPath)
        {
            m_GeneratedFilePaths.Clear();
            DomainAssetDatabase.Refresh();

            bool anyEnums = false;
            foreach (var e in DomainAssetDatabase.EnumDefinitions)
            {
                GenerateEnum(e, TypeResolver.DomainsNamespace, outputPath);
                anyEnums = true;
            }

            foreach (var trait in DomainAssetDatabase.TraitDefinitions)
            {
                var traitType = TypeResolver.GetType(trait.Name);
                if (traitType != null && typeof(ICustomTrait).IsAssignableFrom(traitType)) // No codegen needed
                    continue;

                GenerateTrait(trait, TypeResolver.DomainsNamespace, outputPath, anyEnums);
            }

            GeneratePlanningDomain(TypeResolver.DomainsNamespace, outputPath);

            var domainsNamespace = TypeResolver.DomainsNamespace;
            SaveToFile($"{outputPath}/{domainsNamespace}/AssemblyInfo.cs", "using System.Runtime.CompilerServices; [assembly: InternalsVisibleTo(\"AI.Planner.Actions\")]");

            // Make a copy, so this is re-entrant
            return m_GeneratedFilePaths.ToList();
        }

        internal List<string> GeneratePlans(string outputPath)
        {
            m_GeneratedFilePaths.Clear();
            DomainAssetDatabase.Refresh();

            var actionsNamespace = TypeResolver.ActionsNamespace;

            var anyEnums = DomainAssetDatabase.EnumDefinitions.Any();
            foreach (var plan in DomainAssetDatabase.PlanDefinitions)
            {
                foreach (var action in plan.ActionDefinitions)
                {
                    if (action != null)
                        GenerateAction(action, plan.Name, TypeResolver.DomainsNamespace, actionsNamespace, outputPath, anyEnums);
                }

                GenerateActionScheduler(plan, plan.Name, TypeResolver.ActionsNamespace, outputPath);

                GeneratePlanner(plan, plan.Name, TypeResolver.ActionsNamespace, outputPath, anyEnums);
            }

            // Make a copy, so this is re-entrant
            return m_GeneratedFilePaths.ToList();
        }

        void GenerateTrait(TraitDefinition trait, string domainNamespace, string outputPath, bool includeEnums = false)
        {
            var fields = trait.Fields.Select(p => new
            {
                field_type = p.Type,
                field_name = p.Name
            });

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateTrait, new
            {
                @namespace = domainNamespace,
                name = trait.Name,
                fields = fields,
                include_enums = includeEnums,
            });

            SaveToFile($"{outputPath}/{domainNamespace}/Traits/{trait.Name}.cs", result);
        }

        void GenerateEnum(EnumDefinition @enum, string domainNamespace, string outputPath)
        {
            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateEnum, new
            {
                Namespace = domainNamespace,
                Name = @enum.Name,
                Values = @enum.Values
            });

            SaveToFile($"{outputPath}/{domainNamespace}/Traits/{@enum.Name}.cs", result);
        }

        void GeneratePlanningDomain(string domainNamespace, string outputPath)
        {
            var traits = DomainAssetDatabase.TraitDefinitions.Select(p => new
            {
                name = p.Name,
                relations = p.Fields.Where(f => f.Type.EndsWith("ObjectId")).Select(f => new { name = f.Name }),
                attributes = p.Fields.Where(f => !f.Type.EndsWith("ObjectId")
                    && (f.FieldType == null // It's possible the type isn't available for reflection, so just assume it's blittable for now
                    || UnsafeUtility.IsBlittable(f.FieldType))).Select(t => new
                {
                    field_type = t.Type,
                    field_name = t.Name
                })
            });

            foreach (var termination in DomainAssetDatabase.StateTerminationDefinitions)
            {
                GenerateTermination(termination, TypeResolver.DomainsNamespace, outputPath);
            }

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateDomain, new
            {
                domain_namespace = domainNamespace,
                trait_list = traits,
                num_traits = traits.Count()
            });

            SaveToFile($"{outputPath}/{domainNamespace}/PlanningDomainData.cs", result);
        }

        void GenerateTermination(StateTerminationDefinition termination, string domainNamespace, string outputPath)
        {
            var terminationName = termination.Name;

            var parameters = termination.Parameters.Select(p => new
            {
                Name = p.Name,
                required_traits = p.RequiredTraits,
                prohibited_traits = p.ProhibitedTraits,
            });

            var terminationCriteria = termination.Criteria;
            var criteriaTraits = terminationCriteria.Where(c => c.OperandA.Trait != null).Select(c => c.OperandA.Trait.Name)
                .Concat(terminationCriteria.Where(c => c.OperandB.Trait != null).Select(c => c.OperandB.Trait.Name))
                .Distinct();

            var parameterNames = parameters.Select(p => p.Name).ToList();
            var criteria = termination.Criteria.Select(p => new
            {
                @operator = p.Operator,
                operand_a = GetPreconditionOperandString(p.OperandA, parameterNames),
                operand_b = GetPreconditionOperandString(p.OperandB, parameterNames),
                loop_index = Mathf.Max(parameterNames.FindIndex(name => name == p.OperandA.Parameter)
                    , parameterNames.FindIndex(name => name == p.OperandB.Parameter))
            });

            var customRewards = termination.CustomRewards.Select(c =>
            {
                var customRewardType = TypeResolver.GetType(c.Typename);
                if (customRewardType != null)
                {
                    return new
                    {
                        @operator = c.Operator,
                        typename = customRewardType.FullName,
                        parameters = c.Parameters.Select((p, index) => new
                        {
                            index = parameterNames.IndexOf(p),
                            type = TypeResolver.GetType(c.Typename).GetMethod("RewardModifier")?.GetParameters()[index].ParameterType
                        })
                    };
                }
                else
                {
                    Debug.LogWarning($"Couldn't resolve custom type {c.Typename} for termination {termination.Name}. Skipping for now, but try to re-generate.");
                }

                return null;
            }).Where(c => c != null);

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateTermination, new
            {
                @namespace = domainNamespace,
                name = terminationName,
                parameter_list = parameters,
                criteria_traits = criteriaTraits.ToArray(),
                criteria_list = criteria.ToArray(),
                reward_value = termination.TerminalReward,
                custom_rewards =  customRewards,
            });
            SaveToFile($"{outputPath}/{domainNamespace}/{terminationName}.cs", result);
        }

        void GenerateActionScheduler(PlanDefinition definition, string planningNamespace, string domainNamespace, string outputPath)
        {
            int maxArgs = 0;
            foreach (var action in definition.ActionDefinitions)
            {
                if (action != null)
                    maxArgs = Math.Max(maxArgs, action.Parameters.Count());
            }

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateActionScheduler, new
            {
                planning_name = definition.Name,
                domain_namespace = $"{domainNamespace}.{planningNamespace}",
                actions = definition.ActionDefinitions,
                num_actions = definition.ActionDefinitions.Count(),
                num_args = maxArgs,
            });

            SaveToFile($"{outputPath}/{domainNamespace}/{planningNamespace}/ActionScheduler.cs", result);
        }

        void GeneratePlanner(PlanDefinition definition, string planningNamespace, string domainNamespace, string outputPath, bool includeEnums = false)
        {
            var customHeuristic = definition.CustomHeuristic;
            var heuristicTypeName = string.IsNullOrEmpty(customHeuristic) ? "DefaultHeuristic" : customHeuristic;

            var defaultHeuristic = new
            {
                lower = definition.DefaultHeuristicLower,
                avg = definition.DefaultHeuristicAverage,
                upper = definition.DefaultHeuristicUpper,
            };

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplatePlanExecutor, new
            {
                plan_name = definition.Name,
                class_namespace = $"{domainNamespace}.{planningNamespace}",
                actions = definition.ActionDefinitions,
                traits = definition.GetTraitsUsed(),
                heuristic = heuristicTypeName,
                default_heuristic = defaultHeuristic,
                terminations = definition.StateTerminationDefinitions.Where(t => t != null).Select(t => t.name),
                include_enums = includeEnums,
            });

            SaveToFile($"{outputPath}/{domainNamespace}/{planningNamespace}/{definition.name}Executor.cs", result);
        }

        void GenerateAction(ActionDefinition action, string planName, string domainNamespace, string actionNamespace, string outputPath, bool includeEnums = false)
        {
            var parameters = action.Parameters.Select(p => new
            {
                Name = p.Name,
                required_traits = p.RequiredTraits,
                prohibited_traits = p.ProhibitedTraits,
            });

            var parameterNames = parameters.Select(p => p.Name).ToList();
            var traitPreconditionList = action.Preconditions.Where(p => !p.Operator.Contains('.'));
            var preconditions = traitPreconditionList.Select(p => new
            {
                Operator = p.Operator,
                operand_a = GetPreconditionOperandString(p.OperandA, parameterNames),
                operand_b = GetPreconditionOperandString(p.OperandB, parameterNames),
                loop_index = Mathf.Max(parameterNames.FindIndex(name => name == p.OperandA.Parameter)
                    , parameterNames.FindIndex(name => name == p.OperandB.Parameter))
            });

            var preconditionTraits = traitPreconditionList.Where(c => c.OperandA.Trait != null).Select(c => c.OperandA.Trait.Name)
                .Concat(traitPreconditionList.Where(c => c.OperandB.Trait != null).Select(c => c.OperandB.Trait.Name))
                .Distinct();

            var customPreconditionList = action.Preconditions.Where(p => p.Operator.Contains('.'));
            var customPreconditions = customPreconditionList.Select(p => p.Operator.Substring(p.Operator.IndexOf('.') + 1));

            var createdObjects = action.CreatedObjects.Select(c => new
            {
                name = c.Name,
                required_traits = c.RequiredTraits.Select(t => t.Name),
                prohibited_traits = c.ProhibitedTraits.Select(t => t.Name)
            });

            var requiredObjectBuffers = new HashSet<string>();
            var requiredTraitBuffers = new HashSet<string>();

            var objectModifiers = action.ObjectModifiers.Select(p => BuildModifierLines(action, p.Operator, p.OperandA, p.OperandB, ref requiredObjectBuffers, ref requiredTraitBuffers));

            var customRewards = action.CustomRewards.Select(c =>
            {
                var customRewardType = TypeResolver.GetType(c.Typename);
                if (customRewardType != null)
                {
                    return new
                    {
                        @operator = c.Operator,
                        typename = c.Typename.Split(',')[0],
                        parameters = c.Parameters.Select((p, index) => new
                        {
                            index = parameterNames.IndexOf(p),
                            type = customRewardType.GetMethod("RewardModifier")?.GetParameters()[index].ParameterType
                        })
                    };
                }
                else
                {
                    Debug.LogWarning($"Couldn't resolve custom type {c.Typename} for termination {action.Name}. Skipping for now, but try to re-generate.");
                }

                return null;
            }).Where(c => c != null);

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateAction, new
            {
                plan_name = planName,
                action_name = action.Name,
                action_namespace = actionNamespace,
                domain_namespace = domainNamespace,
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
            });

            SaveToFile($"{outputPath}/{actionNamespace}/{planName}/{action.Name}.cs", result);
        }

        public string[] BuildModifierLines(ActionDefinition action, string @operator, OperandValue operandA, OperandValue operandB, ref HashSet<string> requiredObjectBuffers, ref HashSet<string> requiredTraitBuffers)
        {
            const string prefixNew = "new";
            const string prefixOriginal = "original";

            List<string> modifierLines = new List<string>();

            string operatorType = @operator.Split('.')[0];
            switch (operatorType)
            {
                case Operation.CustomOperator:
                {
                    var customClass = @operator.Split('.')[1];

                    modifierLines.Add($"new {customClass}().ApplyCustomActionEffectsToState(originalState, action, newState);");
                }
                    break;
                case Operation.AddTraitOperator:
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
                case Operation.RemoveTraitOperator:
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

        void SaveToFile(string filePath, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, text);

            m_GeneratedFilePaths.Add(filePath);
        }

        string GetPreconditionOperandString(OperandValue operand, List<string> parameterNames)
        {
            if (operand.Trait == null)
            {
                if (operand.Enum != null)
                {
                    return $"{operand.Enum.Name}.{operand.Value}";
                }
                else if (parameterNames.Contains(operand.Parameter))
                {
                    return $"stateData.GetTraitBasedObjectId({operand.Parameter}Index)";
                }

                return operand.Value;
            }

            var precondition =  $"{operand.Trait.Name}Buffer[{operand.Parameter}Object.{operand.Trait.Name}Index]";
            if (!string.IsNullOrEmpty(operand.TraitFieldName))
                precondition +=  $".{operand.TraitFieldName}";

            return precondition;
        }
    }
}
