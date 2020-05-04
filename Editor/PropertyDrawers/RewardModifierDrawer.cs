using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    static class RewardModifierDrawer
    {
        const string k_RewardModifierMethodName = "RewardModifier";
        static readonly string[] k_RewardOperators = { "+=", "-=", "*=" };

        internal static void PropertyField(Rect rect, IList<ParameterDefinition> target, SerializedProperty rewardElement, Type[] rewardTypes)
        {
            const int operatorSize = 60;
            const int spacer = 5;

            var w = rect.width;
            rect.width = operatorSize;
            rect.y += EditorGUIUtility.standardVerticalSpacing;

            var @operator = rewardElement.FindPropertyRelative("m_Operator");
            var opIndex = EditorGUI.Popup(rect, Array.IndexOf(k_RewardOperators, @operator.stringValue),
                k_RewardOperators, EditorStyleHelper.listPopupStyle);

            if (k_RewardOperators.Length > 0)
                @operator.stringValue = k_RewardOperators[Math.Max(0, opIndex)];

            rect.x += operatorSize;
            rect.width = w - operatorSize;
            rect.height = EditorGUIUtility.singleLineHeight;
            var methodName = rewardElement.FindPropertyRelative("m_Typename");
            var customRewardFullNames = rewardTypes.Select(t => t.FullName).ToArray();

            rect.x += spacer;
            EditorStyleHelper.CustomMethodField(rect, methodName.stringValue, rewardTypes);

            var customRewardIndex = Array.IndexOf(customRewardFullNames, methodName.stringValue);
            if (customRewardIndex >= 0)
            {
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var rewardType = rewardTypes[customRewardIndex];

                var rewardParametersInfo = rewardType.GetMethod(k_RewardModifierMethodName)?.GetParameters();
                if (rewardParametersInfo != null)
                {
                    if (rewardParametersInfo[0].ParameterType.GetInterfaces().Contains(typeof(IStateData)))
                    {
                        GUI.Label(rect, "No parameters", EditorStyleHelper.italicGrayLabel);
                    }
                    else
                    {
                        var parameterProperties = rewardElement.FindPropertyRelative("m_Parameters");

                        var customParamCount = rewardParametersInfo.Length;
                        if (parameterProperties.arraySize != customParamCount)
                        {
                            parameterProperties.arraySize = customParamCount;
                        }

                        if (customParamCount >= 0)
                        {
                            float paramWidth = rect.width / customParamCount;
                            rect.width = paramWidth;

                            for (var i = 0; i < customParamCount; i++)
                            {
                                var parameterType = rewardParametersInfo[i].ParameterType;

                                TraitGUIUtility.DrawParameterSelectorField(rect, parameterProperties.GetArrayElementAtIndex(i), target, parameterType);
                                rect.x += paramWidth;
                            }
                        }
                    }
                }
                else
                {
                    GUI.Label(rect, "Invalid reward type", EditorStyleHelper.errorTextField);
                }
            }
        }

        public static void ShowRewardModifierMenu(SerializedObject serializedObject, SerializedProperty propertyList, Type[] rewardTypes)
        {
            var menu = new GenericMenu();

            for (var i = 0; i < rewardTypes.Length; i++)
            {
                var rewardTypeName = rewardTypes[i].FullName;
                var displayName = rewardTypes[i].Name;

                string builtinModule;
                if ((builtinModule = PlannerAssetDatabase.GetBuiltinModuleName(rewardTypes[i].Namespace)) != null)
                {
                    displayName = $"{builtinModule}/{displayName}";
                }

                menu.AddItem(new GUIContent(displayName), false, () =>
                {
                    serializedObject.Update();
                    var newFieldProperty = propertyList.InsertArrayElement();
                    var typeProperty = newFieldProperty.FindPropertyRelative("m_Typename");
                    typeProperty.stringValue = rewardTypeName;
                    var parametersProperty = newFieldProperty.FindPropertyRelative("m_Parameters");
                    parametersProperty.arraySize = 0;

                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }
    }
}
