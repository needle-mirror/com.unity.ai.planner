using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(ActionDefinition))]
    class ActionDefinitionInspector : BaseTraitOperationEditor
    {
        static readonly string[] k_DefaultOperators = { "=" };
        static readonly string[] k_NumberOperators = { "=", "+=", "-=", "*=" };

        NoHeaderReorderableList m_PreconditionList;
        NoHeaderReorderableList m_ObjectModifierList;
        NoHeaderReorderableList m_ObjectRemovedList;
        NoHeaderReorderableList m_ObjectCreatedList;
        NoHeaderReorderableList m_ParameterList;
        NoHeaderReorderableList m_RewardModifierList;

        void OnEnable()
        {
            PlannerAssetDatabase.Refresh();
            PlannerCustomTypeCache.Refresh();

            InitializeReorderableLists();
        }

        void InitializeReorderableLists()
        {
            m_PreconditionList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Preconditions"), DrawPreconditionListElement, 1);
            m_PreconditionList.onAddDropdownCallback += ShowPreconditionMenu;

            m_ObjectModifierList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_ObjectModifiers"), DrawObjectModifierListElement, 1);
            m_ObjectModifierList.onAddDropdownCallback += ShowObjectModifierMenu;

            m_ObjectRemovedList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_RemovedObjects"), DrawRemovedObjectListElement, 1);

            m_ObjectCreatedList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_CreatedObjects"), DrawCreatedObjectListElement, 1);
            m_ObjectCreatedList.onAddCallback += AddCreatedObjectElement;

            m_ParameterList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Parameters"), DrawParameterList, 3);
            m_ParameterList.onAddCallback += AddParameterElement;
            m_ParameterList.elementHeightCallback += SetParameterHeight;

            m_RewardModifierList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_CustomRewards"), DrawRewardModifierListElement, 2);
            m_RewardModifierList.onAddDropdownCallback += ShowRewardModifierMenu;
        }

        void OnDisable()
        {
            m_PreconditionList.onAddDropdownCallback -= ShowPreconditionMenu;
            m_ObjectModifierList.onAddDropdownCallback -= ShowObjectModifierMenu;
            m_ObjectCreatedList.onAddCallback -= AddCreatedObjectElement;
            m_ParameterList.onAddCallback -= AddParameterElement;
            m_RewardModifierList.onAddDropdownCallback -= ShowRewardModifierMenu;
        }

        public override void OnInspectorGUI()
        {
            var action = (ActionDefinition)target;
            var assetPath = AssetDatabase.GetAssetPath(action);
            var assetOnDisk = !string.IsNullOrEmpty(assetPath);
            var editable = !assetOnDisk || AssetDatabaseUtility.IsEditable(assetPath);

            if (!editable)
            {
                EditorGUILayout.HelpBox(
                    "This file is currently read-only. You probably need to check it out from version control.",
                    MessageType.Info);
            }

            GUI.enabled = editable;

            serializedObject.Update();

            EditorGUILayout.Space();

            var parametersProperty = serializedObject.FindProperty("m_Parameters");
            parametersProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.parameters, parametersProperty.isExpanded, AIPlannerPreferences.displayActionDefinitionAdvancedSettings, toggle => AIPlannerPreferences.displayActionDefinitionAdvancedSettings = toggle);

            if (parametersProperty.isExpanded)
            {
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                if (m_ParameterList.count >= ActionKey.MaxLength)
                    EditorGUILayout.HelpBox(EditorStyleHelper.maxParametersReached);

                m_ParameterList.AdjustElementHeight(m_ParameterList.count == 0 ? 1 : AIPlannerPreferences.displayActionDefinitionAdvancedSettings?4:3);
                m_ParameterList.DoLayoutList();

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }

            m_PreconditionList.serializedProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.preconditions, m_PreconditionList.serializedProperty.isExpanded);
            if (m_PreconditionList.serializedProperty.isExpanded)
            {
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                m_PreconditionList.DoLayoutList();

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }

            m_ObjectCreatedList.serializedProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.effects, m_ObjectCreatedList.serializedProperty.isExpanded);
            if (m_ObjectCreatedList.serializedProperty.isExpanded)
            {
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                GUILayout.Label(EditorStyleHelper.objectsCreated);
                m_ObjectCreatedList.AdjustElementHeight(m_ObjectCreatedList.count == 0 ? 1 : 2);
                m_ObjectCreatedList.DoLayoutList();

                GUILayout.Label(EditorStyleHelper.objectsChanged);
                m_ObjectModifierList.DoLayoutList();

                GUILayout.Label(EditorStyleHelper.objectsRemoved);
                m_ObjectRemovedList.DoLayoutList();

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }

            var customRewards = serializedObject.FindProperty("m_CustomRewards");
            customRewards.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.rewards,customRewards.isExpanded);
            if (customRewards.isExpanded)
            {
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Reward"), new GUIContent("Base Value"));

                GUILayout.Label(EditorStyleHelper.rewardModifiers);
                m_RewardModifierList.AdjustElementHeight(m_RewardModifierList.count == 0 ? 1 : 2);
                m_RewardModifierList.DoLayoutList();

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        void DrawPreconditionListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_PreconditionList.serializedProperty;
            var precondition = list.GetArrayElementAtIndex(index);

            var actionParameters = (target as ActionDefinition).Parameters.ToList();
            PreconditionDrawer.PropertyField(rect, actionParameters, precondition, PlannerCustomTypeCache.ActionPreconditionTypes);
        }

        void DrawCreatedObjectListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            DrawNamedObjectElement(rect, index, m_ObjectCreatedList.serializedProperty, GetReservedObjectNames(), false);
        }

        void DrawParameterList(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect = DrawNamedObjectElement(rect, index, m_ParameterList.serializedProperty, GetReservedObjectNames());

            var parameter = m_ParameterList.serializedProperty.GetArrayElementAtIndex(index);
            var limitProperty = parameter.FindPropertyRelative("m_LimitCount");
            int selectionLimit = limitProperty.intValue;

            if (AIPlannerPreferences.displayActionDefinitionAdvancedSettings || selectionLimit > 0)
            {
                const int marginSize = 25;
                const int limitLabelWidth = 36;
                const int orderByLabelWidth = 56;

                var backgroundRect = new Rect(rect.x - marginSize, rect.y, rect.width + marginSize + 2, 1);
                EditorGUI.DrawRect(backgroundRect, new Color(0, 0, 0.1f, 0.2f));
                backgroundRect.height = 23;
                EditorGUI.DrawRect(backgroundRect, new Color(0, 0, 0, 0.1f));

                rect.x += 1;
                rect.y += 1;
                rect.height = EditorGUIUtility.singleLineHeight + 2;
                rect.width = limitLabelWidth;

                EditorGUI.LabelField(rect, EditorStyleHelper.comparerLimit, EditorStyles.label);

                rect.x += limitLabelWidth;
                rect.width = 26;
                EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.2f));

                EditorGUI.BeginChangeCheck();
                selectionLimit = EditorGUI.IntField(rect, selectionLimit, EditorStyles.label);

                if (EditorGUI.EndChangeCheck())
                    limitProperty.intValue = selectionLimit;

                var currentComparer = parameter.FindPropertyRelative("m_LimitComparerType");
                if (selectionLimit <= 0)
                {
                    GUI.enabled = false;
                    currentComparer.stringValue = String.Empty;
                }

                rect.x += 32;
                rect.width = orderByLabelWidth;
                EditorGUI.LabelField(rect, EditorStyleHelper.comparerOrder, EditorStyles.label);

                rect.x += orderByLabelWidth;
                rect.width = backgroundRect.width - 154;


                GUIContent comparerContent = new GUIContent("-");
                var comparerReference = parameter.FindPropertyRelative("m_LimitComparerReference");
                var availableParameters = (target as ActionDefinition).Parameters.Take(index).ToList();

                if (!string.IsNullOrEmpty(currentComparer.stringValue))
                {
                    var comparerType = PlannerCustomTypeCache.ActionParameterComparerTypes.FirstOrDefault(t => t.FullName == currentComparer.stringValue);
                    if (comparerType != null)
                    {
                        var parameterComparerWithReferenceType = comparerType.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IParameterComparerWithReference<,>).Name);
                        if (parameterComparerWithReferenceType == null)
                        {
                            comparerContent = new GUIContent(comparerType.Name);
                            comparerReference.stringValue = String.Empty;
                        }
                        else
                        {
                            var traitTypeExpected = parameterComparerWithReferenceType.GenericTypeArguments[1].Name;
                            List<ParameterDefinition> referenceableParameters = availableParameters.Where(p => p.RequiredTraits.FirstOrDefault(t => t.Name == traitTypeExpected) != null).ToList();

                            // Check that the parameter referenced is defined above in the list and contains the expected Trait
                            if (referenceableParameters.FindIndex(p => p.Name == comparerReference.stringValue) == -1)
                            {
                                GUI.backgroundColor = Color.red;
                            }
                            comparerContent = new GUIContent(comparerType.Name + "(" + comparerReference.stringValue + ")");
                        }
                    }
                    else
                    {
                        GUI.contentColor = Color.red;
                        comparerContent = new GUIContent(currentComparer.stringValue);
                    }
                }

                if (GUI.Button(rect, comparerContent, EditorStyles.toolbarPopup))
                {
                    var popup = new ComparerSelectorPopup(currentComparer, comparerReference, PlannerCustomTypeCache.ActionParameterComparerTypes, availableParameters);
                    PopupWindow.Show(rect, popup);
                }

                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
                GUI.contentColor = Color.white;
            }
        }

        void AddParameterElement(ReorderableList list)
        {
            if (list.count < ActionKey.MaxLength)
            {
                InitializeNamedObject(list.serializedProperty.InsertArrayElement());
            }
        }

        float SetParameterHeight(int index)
        {
            if (AIPlannerPreferences.displayActionDefinitionAdvancedSettings)
                return NoHeaderReorderableList.CalcElementHeight(4);

            var list = m_ParameterList.serializedProperty;
            var property = list.GetArrayElementAtIndex(index);
            var parameterLimit = property.FindPropertyRelative("m_LimitCount").intValue;

            return NoHeaderReorderableList.CalcElementHeight(parameterLimit > 0 ? 4 : 3);
        }

        void AddCreatedObjectElement(ReorderableList list)
        {
            InitializeNamedObject(list.serializedProperty.InsertArrayElement());
        }

        void ShowObjectModifierMenu(Rect rect, ReorderableList list)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Modify"), false, () =>
            {
                AddObjectModifier(list, String.Empty);
            });

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("Add Trait"), false, () =>
            {
                AddObjectModifier(list, nameof(Operation.SpecialOperators.Add));
            });

            menu.AddItem(new GUIContent("Remove Trait"), false, () =>
            {
                AddObjectModifier(list, nameof(Operation.SpecialOperators.Remove));
            });

            foreach (var effect in PlannerCustomTypeCache.ActionEffectTypes)
            {
                menu.AddItem(new GUIContent($"Custom/{effect.Name}"), false, () =>
                {
                    AddObjectModifier(list, nameof(Operation.SpecialOperators.Custom), effect.FullName);
                });
            }

            menu.ShowAsContext();
        }

        void ShowRewardModifierMenu(Rect rect, ReorderableList list)
        {
            RewardModifierDrawer.ShowRewardModifierMenu(serializedObject, list.serializedProperty, PlannerCustomTypeCache.ActionRewardTypes);
        }

        void ShowPreconditionMenu(Rect rect, ReorderableList list)
        {
            if (PlannerCustomTypeCache.ActionPreconditionTypes.Length == 0)
            {
                list.serializedProperty.InsertArrayElement();
                return;
            }

            PreconditionDrawer.ShowPreconditionMenu(serializedObject, list.serializedProperty, PlannerCustomTypeCache.ActionPreconditionTypes);
        }

        void AddObjectModifier(ReorderableList list, string @operator, string customOperator = "")
        {
            serializedObject.Update();
            var newFieldProperty = list.serializedProperty.InsertArrayElement();
            var operatorProperty = newFieldProperty.FindPropertyRelative("m_Operator");
            operatorProperty.stringValue = @operator;
            var customOperatorProperty = newFieldProperty.FindPropertyRelative("m_CustomOperatorType");
            customOperatorProperty.stringValue = customOperator;

            serializedObject.ApplyModifiedProperties();
        }

        void DrawRemovedObjectListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var actionParameters = (target as ActionDefinition).Parameters.ToList();

            var list = m_ObjectRemovedList.serializedProperty;
            var removedObject = list.GetArrayElementAtIndex(index);

            TraitGUIUtility.DrawParameterSelectorField(rect, removedObject, actionParameters);
        }

        void DrawObjectModifierListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var actionDefinition = (target as ActionDefinition);
            if (actionDefinition == null) return;

            const int operatorSize = 50;
            const int spacer = 5;

            var parameters = actionDefinition.Parameters.ToList();
            var effectsParameters = actionDefinition.CreatedObjects.ToList();
            parameters.AddRange(effectsParameters);

            var w = rect.width;
            var buttonSize = (w - operatorSize - 3 * spacer) / 2;
            rect.x += spacer;
            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.width = buttonSize;

            var list = m_ObjectModifierList.serializedProperty;
            var effect = list.GetArrayElementAtIndex(index);

            var @operator = effect.FindPropertyRelative("m_Operator");
            var operandA = effect.FindPropertyRelative("m_OperandA");
            var operandB = effect.FindPropertyRelative("m_OperandB");

            switch (@operator.stringValue)
            {
                case nameof(Operation.SpecialOperators.Add):
                case nameof(Operation.SpecialOperators.Remove):
                {
                    TraitGUIUtility.DrawParameterSelectorField(rect, operandA.FindPropertyRelative("m_Parameter"), parameters);

                    rect.x += buttonSize + spacer;
                    rect.width = operatorSize;

                    EditorGUI.LabelField(rect, (@operator.stringValue == nameof(Operation.SpecialOperators.Add))?"Add":"Remove", EditorStyleHelper.listValueStyle);

                    rect.x += operatorSize + spacer;
                    rect.width = buttonSize;

                    var traitDefinitions = PlannerAssetDatabase.TraitDefinitions.ToArray();
                    var traitNames = traitDefinitions.Select(t => t?.Name).ToArray();

                    var traitIndex = Array.IndexOf(traitDefinitions, operandB.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition);
                    EditorGUI.BeginChangeCheck();
                    traitIndex = EditorGUI.Popup(rect, traitIndex, traitNames, EditorStyleHelper.listPopupStyle);

                    if (EditorGUI.EndChangeCheck())
                    {
                        TraitGUIUtility.ClearOperandProperty(operandB);
                        operandB.FindPropertyRelative("m_Trait").objectReferenceValue = traitDefinitions[traitIndex];
                    }
                }
                    break;
                case nameof(Operation.SpecialOperators.Custom):
                    rect.width = w - spacer;
                    var customType = effect.FindPropertyRelative("m_CustomOperatorType").stringValue;
                    EditorStyleHelper.CustomMethodField(rect, customType, PlannerCustomTypeCache.ActionEffectTypes);
                    break;
                default:
                {
                    TraitGUIUtility.DrawOperandSelectorField(rect, operandA, parameters, modified =>
                    {
                        TraitGUIUtility.ClearOperandProperty(operandB);
                    });

                    var validLeftOperand = !string.IsNullOrEmpty(operandA.FindPropertyRelative("m_Parameter").stringValue);

                    rect.x += buttonSize + spacer;
                    rect.width = operatorSize;

                    if (validLeftOperand)
                    {
                        var operators = GetAssignationOperators(operandA);
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
                        TraitGUIUtility.DrawOperandSelectorField(rect, operandB, parameters, TraitGUIUtility.GetOperandValuePropertyType(operandA, ref unknownType), unknownType);
                    }
                    else
                    {
                        // No operand available
                        GUI.enabled = false;
                        GUI.Button(rect, string.Empty, EditorStyleHelper.listPopupStyle);
                        GUI.enabled = true;
                    }
                }
                    break;
            }
        }

        void DrawRewardModifierListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_RewardModifierList.serializedProperty;
            var rewardElement = list.GetArrayElementAtIndex(index);

            var parameters = (target as ActionDefinition).Parameters.ToList();
            RewardModifierDrawer.PropertyField(rect, parameters, rewardElement, PlannerCustomTypeCache.ActionRewardTypes);
        }

        protected override List<string> GetReservedObjectNames()
        {
            var reservedNames = new List<string>();
            m_ParameterList.serializedProperty.ForEachArrayElement(n => reservedNames.Add(n.FindPropertyRelative("m_Name").stringValue));
            m_ObjectCreatedList.serializedProperty.ForEachArrayElement(n => reservedNames.Add(n.FindPropertyRelative("m_Name").stringValue));
            return reservedNames;
        }

        protected override void OnUniqueNameChanged(string oldName, string newName)
        {
            m_PreconditionList.serializedProperty.ForEachArrayElement(property =>
            {
                RenameOperandParameterName(property, oldName, newName, "m_OperandA");
                RenameOperandParameterName(property, oldName, newName, "m_OperandB");
            });

            m_ObjectModifierList.serializedProperty.ForEachArrayElement(property =>
            {
                RenameOperandParameterName(property, oldName, newName, "m_OperandA");
                RenameOperandParameterName(property, oldName, newName, "m_OperandB");
            });

            m_ObjectRemovedList.serializedProperty.ForEachArrayElement(property =>
            {
                if (property.stringValue == oldName)
                {
                    property.stringValue = newName;
                }
            });

            m_RewardModifierList.serializedProperty.ForEachArrayElement(property =>
            {
                var parameterProperties = property.FindPropertyRelative("m_Parameters");

                parameterProperties.ForEachArrayElement(parameter =>
                {
                    if (parameter.stringValue == oldName)
                    {
                        parameter.stringValue = newName;
                    }
                });
            });

            m_ParameterList.serializedProperty.ForEachArrayElement(property =>
            {
                var referencedParameter = property.FindPropertyRelative("m_LimitComparerReference");

                if (referencedParameter.stringValue == oldName)
                {
                    referencedParameter.stringValue = newName;
                }
            });
        }

        protected static string[] GetAssignationOperators(SerializedProperty operand)
        {
            return TraitGUIUtility.IsNumberOperand(operand)? k_NumberOperators : k_DefaultOperators;
        }
    }
}
