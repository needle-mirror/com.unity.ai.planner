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
        List<Type> m_CustomEffectTypes = new List<Type>();
        List<Type> m_CustomRewardTypes = new List<Type>();
        List<Type> m_CustomTraitRewardTypes = new List<Type>();
        List<Type> m_CustomRewardAllTypes = new List<Type>();
        List<Type> m_CustomPreconditionTypes = new List<Type>();

        NoHeaderReorderableList m_PreconditionList;
        NoHeaderReorderableList m_ObjectModifierList;
        NoHeaderReorderableList m_ObjectRemovedList;
        NoHeaderReorderableList m_ObjectCreatedList;
        NoHeaderReorderableList m_ParameterList;
        NoHeaderReorderableList m_RewardModifierList;

        void OnEnable()
        {
            InitializeReorderableLists();

            DomainAssetDatabase.Refresh();

            m_CustomEffectTypes.Clear();
            typeof(ICustomActionEffect<>).GetImplementationsOfInterface(m_CustomEffectTypes);

            m_CustomRewardTypes.Clear();
            typeof(ICustomReward<>).GetImplementationsOfInterface(m_CustomRewardTypes);

            m_CustomTraitRewardTypes.Clear();
            typeof(ICustomTraitReward).GetImplementationsOfInterface(m_CustomTraitRewardTypes);

            m_CustomRewardAllTypes.Clear();
            m_CustomRewardAllTypes.AddRange(m_CustomRewardTypes.Where(t => !t.IsGenericType));
            m_CustomRewardAllTypes.AddRange(m_CustomTraitRewardTypes.Where(t => !t.IsGenericType));

            m_CustomPreconditionTypes.Clear();
            typeof(ICustomPrecondition<>).GetImplementationsOfInterface(m_CustomPreconditionTypes);
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
            parametersProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.parameters, parametersProperty.isExpanded);

            if (parametersProperty.isExpanded)
            {
                GUILayout.Space(3);

                if (m_ParameterList.count >= ActionKey.MaxLength)
                    EditorGUILayout.HelpBox(EditorStyleHelper.maxParametersReached);

                m_ParameterList.AdjustElementHeight(m_ParameterList.count == 0 ? 1 : 3);
                m_ParameterList.DoLayoutList();

                GUILayout.Space(10);
            }

            m_PreconditionList.serializedProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.preconditions, m_PreconditionList.serializedProperty.isExpanded);
            if (m_PreconditionList.serializedProperty.isExpanded)
            {
                GUILayout.Space(3);

                m_PreconditionList.DoLayoutList();

                GUILayout.Space(10);
            }

            m_ObjectCreatedList.serializedProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.effects, m_ObjectCreatedList.serializedProperty.isExpanded);
            if (m_ObjectCreatedList.serializedProperty.isExpanded)
            {
                GUILayout.Space(3);

                GUILayout.Label(EditorStyleHelper.objectsCreated);
                m_ObjectCreatedList.AdjustElementHeight(m_ObjectCreatedList.count == 0 ? 1 : 2);
                m_ObjectCreatedList.DoLayoutList();

                GUILayout.Label(EditorStyleHelper.objectsChanged);
                m_ObjectModifierList.DoLayoutList();

                GUILayout.Label(EditorStyleHelper.objectsRemoved);
                m_ObjectRemovedList.DoLayoutList();

                GUILayout.Space(10);
            }

            var customRewards = serializedObject.FindProperty("m_CustomRewards");
            customRewards.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.rewards,customRewards.isExpanded);
            if (customRewards.isExpanded)
            {
                GUILayout.Space(3);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Reward"), new GUIContent("Base Value"));

                GUILayout.Label(EditorStyleHelper.rewardModifiers);

                m_RewardModifierList.AdjustElementHeight(m_RewardModifierList.count == 0 ? 1 : 2);
                m_RewardModifierList.DoLayoutList();
            }


            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        void DrawPreconditionListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            const int operatorSize = 50;
            const int spacer = 5;

            var parameters = (target as ActionDefinition).Parameters.ToList();

            var w = rect.width;
            var buttonSize = (w - operatorSize - 3 * spacer) / 2;
            rect.x += spacer;
            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.width = buttonSize;

            var list = m_PreconditionList.serializedProperty;
            var precondition = list.GetArrayElementAtIndex(index);

            var @operator = precondition.FindPropertyRelative("m_Operator");

            var operatorType = @operator.stringValue.Split('.')[0];
            switch (operatorType)
            {
                case Operation.CustomOperator:
                    rect.width = w - spacer;
                    EditorGUI.LabelField(rect, @operator.stringValue.Split('.')[1], EditorStyleHelper.listValueStyle);
                    break;
                default:
                {
                    var operandA = precondition.FindPropertyRelative("m_OperandA");
                    var operandB = precondition.FindPropertyRelative("m_OperandB");

                    DrawOperandSelectorField(rect, operandA, parameters, true, false, property =>
                    {
                        ClearOperandProperty(operandB);
                    });

                    var validLeftOperand = !string.IsNullOrEmpty(operandA.FindPropertyRelative("m_Parameter").stringValue);

                    rect.x += buttonSize + spacer;
                    rect.width = operatorSize;

                    if (validLeftOperand)
                    {
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
                        DrawOperandSelectorField(rect, operandB, parameters, TraitUtility.GetOperandValuePropertyType(operandA, ref unknownType), unknownType);
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


        void DrawCreatedObjectListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            DrawNamedObjectElement(rect, index, m_ObjectCreatedList.serializedProperty, GetReservedObjectNames(), false);
        }

        void DrawParameterList(Rect rect, int index, bool isActive, bool isFocused)
        {
            DrawNamedObjectElement(rect, index, m_ParameterList.serializedProperty, GetReservedObjectNames());
        }

        void AddParameterElement(ReorderableList list)
        {
            if (list.count < ActionKey.MaxLength)
            {
                InitializeNamedObject(list.serializedProperty.InsertArrayElement());
            }
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
                AddObjectModifier(list, Operation.AddTraitOperator);
            });

            menu.AddItem(new GUIContent("Remove Trait"), false, () =>
            {
                AddObjectModifier(list, Operation.RemoveTraitOperator);
            });

            var customEffectNames = m_CustomEffectTypes.Where(t => !t.IsGenericType).Select(t => t.Name).ToArray();

            foreach (var effectName in customEffectNames)
            {
                menu.AddItem(new GUIContent($"Custom/{effectName}"), false, () =>
                {
                    AddObjectModifier(list, $"{Operation.CustomOperator}.{effectName}");
                });
            }

            menu.ShowAsContext();
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

        void ShowPreconditionMenu(Rect rect, ReorderableList list)
        {
            var customPreconditionTypes = m_CustomPreconditionTypes.Where(t => !t.IsGenericType).Select(t => t.Name).ToArray();
            if (customPreconditionTypes.Length == 0)
            {
                list.serializedProperty.InsertArrayElement();
                return;
            }

            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Trait condition"), false, () =>
            {
                serializedObject.Update();
                list.serializedProperty.InsertArrayElement();
                serializedObject.ApplyModifiedProperties();
            });

            foreach (var preconditionName in customPreconditionTypes)
            {
                menu.AddItem(new GUIContent($"Custom/{preconditionName}"), false, () =>
                {
                    serializedObject.Update();
                    var newFieldProperty = list.serializedProperty.InsertArrayElement();
                    var operatorProperty = newFieldProperty.FindPropertyRelative("m_Operator");
                    operatorProperty.stringValue = $"{Operation.CustomOperator}.{preconditionName}";

                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        void AddObjectModifier(ReorderableList list, string @operator)
        {
            serializedObject.Update();
            var newFieldProperty = list.serializedProperty.InsertArrayElement();
            var operatorProperty = newFieldProperty.FindPropertyRelative("m_Operator");
            operatorProperty.stringValue = @operator;

            serializedObject.ApplyModifiedProperties();
        }

        void DrawRemovedObjectListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var actionParameters = (target as ActionDefinition).Parameters.ToList();

            var list = m_ObjectRemovedList.serializedProperty;
            var removedObject = list.GetArrayElementAtIndex(index);

            DrawParameterSelectorField(rect, removedObject, actionParameters);
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

            var operatorType = @operator.stringValue.Split('.')[0];
            switch (operatorType)
            {
                case Operation.AddTraitOperator:
                case Operation.RemoveTraitOperator:
                {
                    DrawParameterSelectorField(rect, operandA.FindPropertyRelative("m_Parameter"), parameters);

                    rect.x += buttonSize + spacer;
                    rect.width = operatorSize;

                    EditorGUI.LabelField(rect, (@operator.stringValue == Operation.AddTraitOperator)?"Add":"Remove", EditorStyleHelper.listValueStyle);

                    rect.x += operatorSize + spacer;
                    rect.width = buttonSize;

                    var traitDefinitions = DomainAssetDatabase.TraitDefinitions.ToArray();
                    var traitNames = traitDefinitions.Select(t => t?.Name).ToArray();

                    var traitIndex = Array.IndexOf(traitDefinitions, operandB.FindPropertyRelative("m_Trait").objectReferenceValue as TraitDefinition);
                    EditorGUI.BeginChangeCheck();
                    traitIndex = EditorGUI.Popup(rect, traitIndex, traitNames, EditorStyleHelper.listPopupStyle);

                    if (EditorGUI.EndChangeCheck())
                    {
                        ClearOperandProperty(operandB);
                        operandB.FindPropertyRelative("m_Trait").objectReferenceValue = traitDefinitions[traitIndex];
                    }
                }
                    break;
                case Operation.CustomOperator:
                    rect.width = w - spacer;

                    var customClassName = @operator.stringValue.Split('.')[1];

                    EditorGUI.LabelField(rect,customClassName, m_CustomEffectTypes.Find(c => c.Name == customClassName) == null?EditorStyleHelper.listValueStyleError:EditorStyleHelper.listValueStyle);
                    break;
                default:
                {
                    DrawOperandSelectorField(rect, operandA, parameters, modified =>
                    {
                        ClearOperandProperty(operandB);
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
                        DrawOperandSelectorField(rect, operandB, parameters, TraitUtility.GetOperandValuePropertyType(operandA, ref unknownType), unknownType);
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
            const int operatorSize = 60;
            const int spacer = 5;

            var list = m_RewardModifierList.serializedProperty;
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
                    var actionParameters = (target as ActionDefinition).Parameters.ToList();

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
                            var parameterType = rewardParameters[i].ParameterType;

                            DrawParameterSelectorField(rect, parameterProperties.GetArrayElementAtIndex(i), actionParameters.ToList(), parameterType);
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
        }
    }
}
