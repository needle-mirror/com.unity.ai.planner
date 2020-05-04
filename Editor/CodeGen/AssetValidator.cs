using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using Assembly = System.Reflection.Assembly;

namespace UnityEditor.AI.Planner.CodeGen
{
    class AssetValidator
    {
        public event Action<string, ScriptableObject> errorLogged;

        internal bool CheckStateRepresentationAssetsValidity()
        {
            bool assetValid = true;

            foreach (var enumeration in PlannerAssetDatabase.EnumDefinitions)
            {
                assetValid &= IsEnumAssetValid(enumeration);
            }
            foreach (var trait in PlannerAssetDatabase.TraitDefinitions)
            {
                assetValid &= IsTraitAssetValid(trait);
            }

            return assetValid;
        }

        internal bool CheckPlansAssetsValidity(Assembly customAssembly)
        {
            Type[] customTypes = ReflectionUtils.GetTypesFromAssembly(customAssembly);
            bool assetValid = true;

            foreach (var plan in PlannerAssetDatabase.PlanDefinitions)
            {
                if (plan.ActionDefinitions == null)
                    continue;

                assetValid &= IsPlanAssetValid(plan, customTypes);
            }
            foreach (var action in PlannerAssetDatabase.ActionDefinitions)
            {
                assetValid &= IsActionAssetValid(action, customTypes);
            }
            foreach (var termination in PlannerAssetDatabase.StateTerminationDefinitions)
            {
                assetValid &= IsTerminationAssetValid(termination, customTypes);
            }

            return assetValid;
        }

        bool IsPlanAssetValid(PlanDefinition plan, Type[] customTypes = null)
        {
            bool planValid = true;

            // Check for duplicate actions
            List<string> declaredActions = new List<string>();
            foreach (var action in plan.ActionDefinitions)
            {
                if (declaredActions.Contains(action.Name))
                {
                    errorLogged?.Invoke($"{action.Name} is a duplicated action.", plan);
                    planValid = false;
                }
                else
                    declaredActions.Add(action.Name);
            }

            if (!string.IsNullOrEmpty(plan.CustomHeuristic))
            {
                var heuristicType = customTypes?.FirstOrDefault(t => t.FullName == plan.CustomHeuristic);
                if (heuristicType == null)
                {
                    errorLogged?.Invoke($"Couldn't resolve custom heuristic type {plan.CustomHeuristic}.", plan);
                    planValid = false;
                }
            }

            return planValid;
        }

        internal bool IsActionAssetValid(ActionDefinition action, Type[] customTypes = null)
        {
            bool actionValid = true;
            var parameterNames = action.Parameters.Select(p => p.Name).ToList();

            // Check if all declared object names are unique
            List<string> declaredObjectNames = new List<string>();
            foreach (var parameter in action.Parameters)
            {
                if (declaredObjectNames.Contains(parameter.Name))
                {
                    errorLogged?.Invoke($"{parameter.Name} is a duplicated object name.", action);
                    actionValid = false;
                }
                else
                    declaredObjectNames.Add(parameter.Name);
            }
            foreach (var obj in action.CreatedObjects)
            {
                if (declaredObjectNames.Contains(obj.Name))
                {
                    errorLogged?.Invoke($"{obj.Name} is a duplicated object name.", action);
                    actionValid = false;
                }
                else
                    declaredObjectNames.Add(obj.Name);
            }

            // Check if comparer types used in parameters exist in the assembly and are valid
            foreach (var parameter in action.Parameters)
            {
                var comparerTypeName = parameter.LimitComparerType;

                if (string.IsNullOrEmpty(comparerTypeName))
                    continue;

                var comparerType = customTypes?.FirstOrDefault(t => t.FullName == comparerTypeName);
                if (comparerType == null)
                {
                    errorLogged?.Invoke($"Couldn't resolve parameter comparer type {comparerTypeName}.", action);
                    actionValid = false;
                    continue;
                }

                var parameterComparerType = comparerType.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IParameterComparer<>).Name);
                if (parameterComparerType == null)
                {
                    errorLogged?.Invoke($"Parameter comparer type {comparerTypeName} is not implementing IParameterComparer.", action);
                    actionValid = false;
                    continue;
                }

                var parameterComparerWithReferenceType = comparerType.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IParameterComparerWithReference<,>).Name);
                if (parameterComparerWithReferenceType != null)
                {
                    if (string.IsNullOrEmpty(parameter.LimitComparerReference))
                    {
                        errorLogged?.Invoke($"A referenced parameter is missing for the comparer {comparerTypeName}.", action);
                        actionValid = false;
                    }
                    else if (!parameterNames.Contains(parameter.LimitComparerReference))
                    {
                        errorLogged?.Invoke($"Reference {parameter.LimitComparerReference} for the parameter comparer type {comparerTypeName} doesn't exists.", action);
                        actionValid = false;
                    }
                    else
                    {
                        var referencedParameter = action.Parameters.First(p => p.Name == parameter.LimitComparerReference);
                        var genericTraitTypeExpected = parameterComparerWithReferenceType.GenericTypeArguments[1].Name;

                        if (!referencedParameter.RequiredTraits.FirstOrDefault(t => t.Name == genericTraitTypeExpected))
                        {
                            errorLogged?.Invoke($"Reference {parameter.LimitComparerReference} for the parameter comparer type {comparerTypeName} is invalid.", action);
                            actionValid = false;
                        }
                    }
                }
            }

            foreach (var precondition in action.Preconditions)
            {
                if (precondition.IsSpecialOperator(Operation.SpecialOperators.Custom))
                {
                    var preconditionType = customTypes?.FirstOrDefault(t => t.FullName == precondition.CustomOperatorType);
                    if (preconditionType == null)
                    {
                        errorLogged?.Invoke($"Couldn't resolve action precondition type {precondition.CustomOperatorType}.", action);
                        actionValid = false;
                    }
                }
            }

            foreach (var effect in action.ObjectModifiers)
            {
                if (effect.IsSpecialOperator(Operation.SpecialOperators.Custom))
                {
                    var effectType = customTypes?.FirstOrDefault(t => t.FullName == effect.CustomOperatorType);
                    if (effectType == null)
                    {
                        errorLogged?.Invoke($"Couldn't resolve action effect type {effect.CustomOperatorType}.", action);
                        actionValid = false;
                    }
                }
            }

            // Check if custom reward types used exist in the assembly and are valid
            if (action.CustomRewards != null)
            {
                actionValid = CheckActionCustomReward(action, action.CustomRewards, customTypes);
            }

            return actionValid;
        }

        bool IsTerminationAssetValid(StateTerminationDefinition termination, Type[] customTypes)
        {
            bool terminationValid = true;

            // Check if custom reward types used exist in the assembly and are valid
            if (termination.CustomRewards != null)
            {
                terminationValid = CheckTerminationCustomReward(termination, termination.CustomRewards, customTypes);
            }

            foreach (var precondition in termination.Criteria)
            {
                if (precondition.IsSpecialOperator(Operation.SpecialOperators.Custom))
                {
                    var preconditionType = customTypes?.FirstOrDefault(t => t.FullName == precondition.CustomOperatorType);
                    if (preconditionType == null)
                    {
                        errorLogged?.Invoke($"Couldn't resolve termination precondition type {precondition.CustomOperatorType}.", termination);
                        terminationValid = false;
                    }
                }
            }

            return terminationValid;
        }

        bool CheckActionCustomReward(ScriptableObject scriptableObject, IEnumerable<CustomRewardData> customRewards, Type[] customTypes)
        {
            bool isValid = true;

            foreach (var reward in customRewards)
            {
                var rewardType = customTypes?.FirstOrDefault(t => t.FullName == reward.Typename);
                if (rewardType == null)
                {
                    errorLogged?.Invoke($"Couldn't resolve custom reward type {reward.Typename}.", scriptableObject);
                    isValid = false;
                    continue;
                }

                if (!PlannerCustomTypeCache.IsActionRewardType(rewardType))
                {
                    errorLogged?.Invoke($"Custom reward type {reward.Typename} is not implementing a custom action reward type.", scriptableObject);
                    isValid = false;
                }
            }

            return isValid;
        }

        bool CheckTerminationCustomReward(ScriptableObject scriptableObject, IEnumerable<CustomRewardData> customRewards, Type[] customTypes)
        {
            bool isValid = true;

            foreach (var reward in customRewards)
            {
                var rewardType = customTypes?.FirstOrDefault(t => t.FullName == reward.Typename);
                if (rewardType == null)
                {
                    errorLogged?.Invoke($"Couldn't resolve custom reward type {reward.Typename}.", scriptableObject);
                    isValid = false;
                }

                var customRewardType = rewardType.GetInterfaces().FirstOrDefault(i => i.Name == typeof(ICustomTerminationReward<>).Name);
                if (customRewardType == null)
                {
                    errorLogged?.Invoke($"Custom reward type {reward.Typename} is not implementing ICustomTerminationReward.", scriptableObject);
                    isValid = false;
                }
            }

            return isValid;
        }

        internal bool IsEnumAssetValid(EnumDefinition enumeration)
        {
            bool enumValid = true;

            // Check for duplicate enum values
            List<string> declaredValueNames = new List<string>();
            foreach (var value in enumeration.Values)
            {
                if (declaredValueNames.Contains(value))
                {
                    errorLogged?.Invoke($"{value} is a duplicated value name.", enumeration);
                    enumValid = false;
                }
                else
                    declaredValueNames.Add(value);
            }
            return enumValid;
        }

        internal bool IsTraitAssetValid(TraitDefinition trait)
        {
            bool traitValid = true;

            // Check for duplicate field names
            List<string> declaredTraitNames = new List<string>();
            foreach (var field in trait.Fields)
            {
                if (declaredTraitNames.Contains(field.Name))
                {
                    errorLogged?.Invoke($"{field.Name} is a duplicated Field name.", trait);
                    traitValid = false;
                }
                else
                    declaredTraitNames.Add(field.Name);
            }
            return traitValid;
        }
    }
}
