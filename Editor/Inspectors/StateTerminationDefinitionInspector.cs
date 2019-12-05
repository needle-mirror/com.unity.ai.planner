using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEditor.AI.Planner.Utility;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(StateTerminationDefinition))]
    class StateTerminationDefinitionInspector : BaseTraitOperationEditor
    {
        NoHeaderReorderableList m_CriteriaList;
        NoHeaderReorderableList m_ParameterList;
        NoHeaderReorderableList m_RewardModifiers;
        List<Type> m_CustomRewardTypes = new List<Type>();
        List<Type> m_CustomTraitRewardTypes = new List<Type>();
        List<Type> m_CustomRewardAllTypes = new List<Type>();

        void OnEnable()
        {
            m_CriteriaList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Criteria"), DrawCriteriaListElement, 1);
            DomainAssetDatabase.Refresh();

            m_ParameterList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Parameters"), DrawParameterList, 3);
            m_ParameterList.onAddCallback += AddParameterElement;

            m_RewardModifiers = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_CustomTerminalRewards"), DrawRewardModifierListElement, 2);
            m_RewardModifiers.onAddDropdownCallback += ShowRewardModifierMenu;

            m_CustomRewardTypes.Clear();
            typeof(ICustomReward<>).GetImplementationsOfInterface(m_CustomRewardTypes);

            m_CustomTraitRewardTypes.Clear();
            typeof(ICustomTraitReward).GetImplementationsOfInterface(m_CustomTraitRewardTypes);

            m_CustomRewardAllTypes.Clear();
            m_CustomRewardAllTypes.AddRange(m_CustomRewardTypes.Where(t => !t.IsGenericType));
            m_CustomRewardAllTypes.AddRange(m_CustomTraitRewardTypes.Where(t => !t.IsGenericType));
        }

        void OnDisable()
        {
            m_ParameterList.onAddCallback -= AddParameterElement;
            m_ParameterList.onAddCallback -= AddParameterElement;
            m_RewardModifiers.onAddDropdownCallback -= ShowRewardModifierMenu;
        }

        public override void OnInspectorGUI()
        {
            var terminalStateDefinition = (StateTerminationDefinition) target;
            var assetPath = AssetDatabase.GetAssetPath(terminalStateDefinition);
            var assetOnDisk = !string.IsNullOrEmpty(assetPath);
            var editable = !assetOnDisk || AssetDatabaseUtility.IsEditable(assetPath);

            if (!editable)
            {
                EditorGUILayout.HelpBox(
                    "This file is currently read-only. You probably need to check it out from version control.",
                    MessageType.Info);
            }

            EditorGUILayout.Separator();

            GUI.enabled = editable;
            serializedObject.Update();

            var parametersProperty = serializedObject.FindProperty("m_Parameters");
            parametersProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.parameters, parametersProperty.isExpanded);
            if (parametersProperty.isExpanded)
            {
                GUILayout.Space(3);

                m_ParameterList.AdjustElementHeight(m_ParameterList.count == 0 ? 1 : 3);
                m_ParameterList.DoLayoutList();

                GUILayout.Space(10);
            }

            var criteriaProperty = serializedObject.FindProperty("m_Criteria");
            criteriaProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.criteria, criteriaProperty.isExpanded);

            if (criteriaProperty.isExpanded)
            {
                GUILayout.Space(3);
                m_CriteriaList.DoLayoutList();

                GUILayout.Space(10);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            var customRewards = serializedObject.FindProperty("m_CustomTerminalRewards");
            if (customRewards != null)
            {
                customRewards.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.terminalRewards,customRewards.isExpanded);
                if (customRewards.isExpanded)
                {
                    GUILayout.Space(3);

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TerminalReward"), new GUIContent("Base Value"));

                    GUILayout.Label(EditorStyleHelper.rewardModifiers);
                    m_RewardModifiers.DoLayoutList();
                }
            }


            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        void DrawCriteriaListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            const int operatorSize = 40;
            const int spacer = 5;

            var actionParameters = (target as StateTerminationDefinition)?.Parameters.ToList();

            var w = rect.width;
            var buttonSize = (w - operatorSize - 3 * spacer ) / 2;
            rect.x += spacer;
            rect.width = buttonSize;
            rect.y += EditorGUIUtility.standardVerticalSpacing;

            var list = m_CriteriaList.serializedProperty;
            var precondition = list.GetArrayElementAtIndex(index);

            var operandA = precondition.FindPropertyRelative("m_OperandA");
            var operandB = precondition.FindPropertyRelative("m_OperandB");

            DrawOperandSelectorField(rect, operandA, actionParameters, true, false, property =>
            {
                ClearOperandProperty(operandB);
            });

            var validLeftOperand = !string.IsNullOrEmpty(operandA.FindPropertyRelative("m_Parameter").stringValue);

            rect.x += buttonSize + spacer;
            rect.width = operatorSize;

            if (validLeftOperand)
            {
                var @operator = precondition.FindPropertyRelative("m_Operator");
                var operators = GetComparisonOperators(operandA);
                var opIndex = EditorGUI.Popup(rect, Array.IndexOf(operators, @operator.stringValue),
                    operators, EditorStyleHelper.listPopupStyleBold);

                @operator.stringValue = operators[Math.Max(0, opIndex)];
            }
            else
            {
                // No operand available
                GUI.enabled = false;
                GUI.Button(rect, string.Empty, EditorStyleHelper.listPopupStyle);
                GUI.enabled = true;
            }

            rect.x += operatorSize + spacer;
            rect.width = buttonSize;

            if (validLeftOperand)
            {
                string unknownType = default;
                DrawOperandSelectorField(rect, operandB, actionParameters, TraitUtility.GetOperandValuePropertyType(operandA, ref unknownType), unknownType);
            }
            else
            {
                // No operand available
                GUI.enabled = false;
                GUI.Button(rect, string.Empty, EditorStyleHelper.listPopupStyle);
                GUI.enabled = true;
            }
        }

        void ShowRewardModifierMenu(Rect rect, ReorderableList list)
        {
            var menu = new GenericMenu();

            var customRewards = m_CustomRewardAllTypes.ToArray();
            var customRewardFullNames = m_CustomRewardAllTypes.Select(t => $"{t.FullName},{t.Assembly.GetName().Name}").ToArray();

            for (var i = 0; i < customRewards.Length; i++)
            {
                var rewardTypeName = customRewardFullNames[i];
                var displayName = customRewards[i].Name;

                string builtinModule;
                if ((builtinModule = DomainAssetDatabase.GetBuiltinModuleName(customRewards[i].Namespace)) != null)
                {
                    displayName = $"{builtinModule}/{displayName}";
                }

                menu.AddItem(new GUIContent(displayName), false, () =>
                {
                    serializedObject.Update();
                    var newFieldProperty = list.serializedProperty.InsertArrayElement();
                    var typeProperty = newFieldProperty.FindPropertyRelative("m_Typename");
                    typeProperty.stringValue = rewardTypeName;

                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }


        void DrawRewardModifierListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            const int operatorSize = 60;
            const int spacer = 5;

            var list = m_RewardModifiers.serializedProperty;
            var rewardElement = list.GetArrayElementAtIndex(index);

            var w = rect.width;
            rect.width = operatorSize;
            rect.y += EditorGUIUtility.standardVerticalSpacing;

            var @operator  = rewardElement.FindPropertyRelative("m_Operator");
            var opIndex = EditorGUI.Popup(rect, Array.IndexOf(s_RewardOperators, @operator.stringValue),
                s_RewardOperators, EditorStyleHelper.listPopupStyle);
            if (opIndex >= 0)
                @operator.stringValue = s_RewardOperators[opIndex];

            rect.x += operatorSize;
            rect.width = w - operatorSize;
            rect.height = EditorGUIUtility.singleLineHeight;
            var methodName = rewardElement.FindPropertyRelative("m_Typename");

            var customRewardNames = m_CustomRewardAllTypes.Select(t => t.Name).ToArray();
            var customRewardFullNames = m_CustomRewardAllTypes.Select(t => $"{t.FullName},{t.Assembly.GetName().Name}").ToArray();

            var customRewardIndex = Array.IndexOf(customRewardFullNames, methodName.stringValue);

            if (customRewardIndex >= 0)
            {
                var rewardType = m_CustomRewardAllTypes[customRewardIndex];

                rect.x += spacer;
                EditorGUI.LabelField(rect, customRewardNames[customRewardIndex], EditorStyleHelper.listValueStyle);

                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                if (m_CustomRewardTypes.Contains(rewardType))
                {
                    GUI.Label(rect, "No parameters", EditorStyleHelper.italicGrayLabel);
                }
                else
                {
                    var actionParameters = (target as StateTerminationDefinition).Parameters.ToList();

                    var parameterProperties = rewardElement.FindPropertyRelative("m_Parameters");

                    var rewardParameters = rewardType.GetMethod("RewardModifier").GetParameters();
                    var customParamCount = rewardParameters.Length;
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
                            DrawParameterSelectorField(rect, parameterProperties.GetArrayElementAtIndex(i), actionParameters.ToList());
                            rect.x += paramWidth;
                        }
                    }
                }
            }
            else
            {
                EditorGUI.LabelField(rect, $"Unknown type {methodName.stringValue}", EditorStyleHelper.listValueStyleError);
            }
        }


        void AddParameterElement(ReorderableList list)
        {
            InitializeNamedObject(list.serializedProperty.InsertArrayElement());
        }

        protected override List<string> GetReservedObjectNames()
        {
            var reservedNames = new List<string>();
            m_ParameterList.serializedProperty.ForEachArrayElement(n => reservedNames.Add(n.FindPropertyRelative("m_Name").stringValue));
            return reservedNames;
        }

        void DrawParameterList(Rect rect, int index, bool isActive, bool isFocused)
        {
            DrawNamedObjectElement(rect, index, m_ParameterList.serializedProperty, GetReservedObjectNames());
        }

        protected override void OnUniqueNameChanged(string oldName, string newName)
        {
            m_CriteriaList.serializedProperty.ForEachArrayElement(property =>
            {
                RenameOperandParameterName(property, oldName, newName, "m_OperandA");
                RenameOperandParameterName(property, oldName, newName, "m_OperandB");
            });
        }
    }
}
