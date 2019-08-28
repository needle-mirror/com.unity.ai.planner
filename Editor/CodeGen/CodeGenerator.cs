using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Planner.Utility;
using UnityEditor;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.CodeGen
{
    class CodeGenerator
    {
        CodeRenderer m_CodeRenderer = new CodeRenderer();
        List<string> m_GeneratedFilePaths = new List<string>();

        internal List<string> GenerateDomain(string outputPath, string rootFolder = null)
        {
            m_GeneratedFilePaths.Clear();
            DomainAssetDatabase.Refresh();

            bool anyEnums = false;
            foreach (var e in DomainAssetDatabase.EnumDefinitions)
            {
                if (rootFolder == null || !AssetDatabase.GetAssetPath(e).StartsWith(rootFolder))
                    continue;

                GenerateEnum(e, TypeResolver.DomainsNamespace, outputPath);
                anyEnums = true;
            }

            foreach (var trait in DomainAssetDatabase.TraitDefinitions)
            {
                if (rootFolder == null || !AssetDatabase.GetAssetPath(trait).StartsWith(rootFolder))
                    continue;

                GenerateTrait(trait, TypeResolver.DomainsNamespace, outputPath, anyEnums);
            }

            GeneratePlanningDomain(TypeResolver.DomainsNamespace, outputPath);

            var domainsNamespace = TypeResolver.DomainsNamespace;
            GenerateConditionalAssembly(domainsNamespace, "PLANNER_DOMAIN_GENERATED", outputPath);
            SaveToFile($"{outputPath}/{domainsNamespace}/AssemblyInfo.cs", "using System.Runtime.CompilerServices; [assembly: InternalsVisibleTo(\"AI.Planner.Actions\")]");

            return m_GeneratedFilePaths;
        }

        internal List<string> GenerateActions(string outputPath, string rootFolder = null)
        {
            m_GeneratedFilePaths.Clear();
            DomainAssetDatabase.Refresh();

            var actionsNamespace = TypeResolver.ActionsNamespace;

            var anyEnums = DomainAssetDatabase.EnumDefinitions.Any();
            foreach (var agent in DomainAssetDatabase.AgentDefinitions)
            {
                if (rootFolder == null || !AssetDatabase.GetAssetPath(agent).StartsWith(rootFolder))
                    continue;

                foreach (var action in agent.ActionDefinitions)
                {
                    if (action != null)
                        GenerateAction(action, agent.Name, TypeResolver.DomainsNamespace, actionsNamespace, outputPath, anyEnums);
                }

                GenerateActionScheduler(agent, agent.Name, TypeResolver.ActionsNamespace, outputPath);
            }
            GenerateConditionalAssembly(actionsNamespace, "PLANNER_ACTIONS_GENERATED", outputPath);

            return m_GeneratedFilePaths;
        }

        private void GenerateTrait(TraitDefinition trait, string domainNamespace, string outputPath, bool includeEnums = false)
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

        private void GenerateEnum(EnumDefinition @enum, string domainNamespace, string outputPath)
        {
            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateEnum, new
            {
                Namespace = domainNamespace,
                Name = @enum.Name,
                Values = @enum.Values
            });

            SaveToFile($"{outputPath}/{domainNamespace}/Traits/{@enum.Name}.cs", result);
        }

        private void GeneratePlanningDomain(string domainNamespace, string outputPath)
        {
            var traits = DomainAssetDatabase.TraitDefinitions.Select(p => new
            {
                name = p.Name
            });

            var terminations = new List<string>();
            foreach (var termination in DomainAssetDatabase.StateTerminationDefinitions)
            {
                GenerateTermination(termination, TypeResolver.DomainsNamespace, outputPath);
                terminations.Add(termination.Name);
            }

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateDomain, new
            {
                domain_namespace = domainNamespace,
                trait_list = traits,
                num_traits = traits.Count(),
                terminations = terminations
            });

            SaveToFile($"{outputPath}/{domainNamespace}/PlanningDomainData.cs", result);
        }

        void GenerateTermination(StateTerminationDefinition termination, string domainNamespace, string outputPath)
        {
            var terminationName = termination.Name;

            var objectParameters = termination.ObjectParameters;
            var parameters = new
            {
                name = objectParameters.Name,
                required_traits = objectParameters.RequiredTraits,
                prohibited_traits = objectParameters.ProhibitedTraits,
            };

            var terminationCriteria = termination.Criteria;
            var criteriaTraits = terminationCriteria.Select(p => GetOperandTrait(p.OperandA))
                .Where(a => a != null)
                .Concat(terminationCriteria.Select(p => GetOperandTrait(p.OperandB)).Where(a => a != null))
                .Distinct();

            var parameterNames = new List<string>();
            parameterNames.Add(objectParameters.Name);
            var criteria = termination.Criteria.Select(p => new
            {
                @operator = p.Operator,
                operand_a = GetPreconditionOperandString(p.OperandA, parameterNames),
                operand_b = GetPreconditionOperandString(p.OperandB, parameterNames),
            });

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateTermination, new
            {
                @namespace = domainNamespace,
                name = terminationName,
                parameter_list = new[] { parameters },
                criteria_traits = criteriaTraits.ToArray(),
                criteria_list = criteria.ToArray(),
            });
            SaveToFile($"{outputPath}/{domainNamespace}/{terminationName}.cs", result);
        }

        private void GenerateActionScheduler(PlanningDomainDefinition definition, string planningNamespace, string domainNamespace, string outputPath)
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

        private void GenerateAction(ActionDefinition action, string agentName, string domainNamespace, string actionNamespace, string outputPath, bool includeEnums = false)
        {
            var parameters = action.Parameters.Select(p => new
            {
                Name = p.Name,
                required_traits = p.RequiredTraits,
                prohibited_traits = p.ProhibitedTraits,
            });

            var parameterNames = parameters.Select(p => p.Name).ToList();
            var preconditions = action.Preconditions.Select(p => new
            {
                Operator = p.Operator,
                operand_a = GetPreconditionOperandString(p.OperandA, parameterNames),
                operand_b = GetPreconditionOperandString(p.OperandB, parameterNames),
                loop_index = Mathf.Max(parameterNames.FindIndex(name => name == GetOperandParam(p.OperandA, parameterNames))
                    , parameterNames.FindIndex(name => name == GetOperandParam(p.OperandB, parameterNames)))
            });

            var preconditionTraits = action.Preconditions.Select(p => GetOperandTrait(p.OperandA))
                .Where(a => a != null)
                .Concat(action.Preconditions.Select(p => GetOperandTrait(p.OperandB)).Where(a => a != null))
                .Distinct();

            var objectEffects = action.Effects.Select(p => new
            {
                Operator = p.Operator,
                operand_a = GetEffectOperandString(action, p.OperandA, parameterNames),
                operand_b = GetEffectOperandString(action, p.OperandB, parameterNames),
            });

            var createdObjects = action.CreatedObjects.Select(c => new
            {
                name = c.Name,
                required_traits = c.RequiredTraits.Select(t => t.Name),
                prohibited_traits = c.ProhibitedTraits.Select(t => t.Name)
            });

            var effectTraits = action.Effects.Select(p => GetOperandTrait(p.OperandA))
                .Where(a => a != null)
                .Concat(action.Effects.Select(p => GetOperandTrait(p.OperandB)).Where(a => a != null))
                .Distinct();

            var effectParams = action.Effects.Select(p => GetOperandParam(p.OperandA, parameterNames))
                .Where(a => a != null && !action.CreatedObjects.Any(c => c.Name == a))
                .Concat(action.Effects.Select(p => GetOperandParam(p.OperandB, parameterNames))
                    .Where(a => a != null && !action.CreatedObjects.Any(c => c.Name == a)))
                .Distinct();

            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateAction, new
            {
                agent_name = agentName,
                action_name = action.Name,
                action_namespace = actionNamespace,
                domain_namespace = domainNamespace,
                parameter_list = parameters.ToList(),
                precondition_list = preconditions.ToList(),
                precondition_traits = preconditionTraits.ToList(),
                created_objects = createdObjects.ToArray(),
                created_object_names = createdObjects.Select(c => c.name),
                object_effect_list = objectEffects.ToList(),
                object_effect_traits = effectTraits.ToList(),
                object_effect_params = effectParams.ToList(),
                reward_value = action.Reward,
                custom_effect = action.CustomEffect,
                custom_reward = action.CustomReward,
                custom_precondition = action.CustomPrecondition,
                include_enums = includeEnums,
                removed_objects = action.RemovedObjects,
            });

            SaveToFile($"{outputPath}/{actionNamespace}/{agentName}/{action.Name}.cs", result);
        }

        void GenerateConditionalAssembly(string domainNamespace, string define, string outputPath)
        {
            var result = m_CodeRenderer.RenderTemplate(PlannerResources.instance.TemplateConditionalAssembly, new
            {
                @namespace = domainNamespace,
                define = define
            });

            SaveToFile($"{outputPath}/{domainNamespace}/ConditionalAssembly.cs", result);
        }

        private void SaveToFile(string filePath, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            File.WriteAllText(filePath, text);
            AssetDatabase.ImportAsset(filePath);

            m_GeneratedFilePaths.Add(filePath);
        }

        string GetPreconditionOperandString(IEnumerable<string> operand, List<string> parameterNames)
        {
            var operandString = string.Empty;
            var i = 0;
            foreach (var e in operand)
            {
                if (i == 0)
                {
                    operandString = e;
                }
                else
                {
                    var split = e.Split('.');
                    var traitType = split[0];

                    if (i == 1)
                    {
                        var parameterName = operandString;
                        operandString = $"{traitType}Buffer[{parameterName}Object.{traitType}Index].{split[1]}";
                    }
                    else
                    {
                        var additionalProperties = $".{split[1]}";
                        operandString += additionalProperties;
                    }
                }

                i++;
            }

            if (parameterNames.Contains(operandString))
                operandString = $"stateData.GetDomainObjectID({operandString}Index)";

            return operandString;
        }

        object GetEffectOperandString(ActionDefinition action, IEnumerable<string> operand, List<string> parameterNames)
        {
            var operandList = operand.ToList();
            var parameterName = operandList[0];

            if (operandList.Count <= 1)
            {
                if (parameterNames.Contains(parameterName))
                    parameterName = $"originalState.GetDomainObjectID(original{parameterName}Object)";
                else if (action.CreatedObjects.Any(c => c.Name == parameterName))
                    parameterName = $"new{parameterName}ObjectID";

                return new
                {
                    trait = string.Empty,
                    parameter = parameterName,
                    field = string.Empty
                };
            }

            var traitDataSplit = operandList[1].Split('.');

            return new
            {
                trait = traitDataSplit[0],
                parameter = parameterName,
                field = traitDataSplit[1]
            };
        }

        string GetOperandTrait(IEnumerable<string> operand)
        {
            var i = 0;
            foreach (var e in operand)
            {
                if (i != 0)
                {
                    var split = e.Split('.');
                    return split[0];
                }

                i++;
            }
            return null;
        }

        string GetOperandParam(IEnumerable<string> operand, List<string> parameterNames)
        {
            var operandList = operand.ToList();

            if (operandList.Count <= 1)
            {
                return parameterNames.Contains(operandList[0]) ? operandList[0] : string.Empty;
            }

            return operandList[0];
        }
    }
}
