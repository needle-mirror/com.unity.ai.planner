using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEditor.AI.Planner.Utility;
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

        void OnEnable()
        {
            PlannerAssetDatabase.Refresh();
            PlannerCustomTypeCache.Refresh();

            InitializeReorderableLists();
        }
        void InitializeReorderableLists()
        {
            m_ParameterList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Parameters"), DrawParameterList, 3);
            m_ParameterList.onAddCallback += AddParameterElement;

            m_CriteriaList = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_Criteria"), DrawCriteriaListElement, 1);
            m_CriteriaList.onAddDropdownCallback += ShowPreconditionMenu;

            m_RewardModifiers = new NoHeaderReorderableList(serializedObject, serializedObject.FindProperty("m_CustomTerminalRewards"), DrawRewardModifierListElement, 2);
            m_RewardModifiers.onAddDropdownCallback += ShowRewardModifierMenu;
        }

        void OnDisable()
        {
            m_ParameterList.onAddCallback -= AddParameterElement;
            m_CriteriaList.onAddDropdownCallback -= ShowPreconditionMenu;
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
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                m_ParameterList.AdjustElementHeight(m_ParameterList.count == 0 ? 1 : 3);
                m_ParameterList.DoLayoutList();

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }

            var criteriaProperty = serializedObject.FindProperty("m_Criteria");
            criteriaProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.criteria, criteriaProperty.isExpanded);

            if (criteriaProperty.isExpanded)
            {
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                m_CriteriaList.DoLayoutList();

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            var customRewards = serializedObject.FindProperty("m_CustomTerminalRewards");
            if (customRewards != null)
            {
                customRewards.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.terminalRewards,customRewards.isExpanded);
                if (customRewards.isExpanded)
                {
                    GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TerminalReward"), new GUIContent("Base Value"));

                    GUILayout.Label(EditorStyleHelper.rewardModifiers);
                    m_RewardModifiers.AdjustElementHeight(m_RewardModifiers.count == 0 ? 1 : 2);
                    m_RewardModifiers.DoLayoutList();

                    GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
                }
            }


            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        void DrawCriteriaListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_CriteriaList.serializedProperty;
            var precondition = list.GetArrayElementAtIndex(index);

            var parameters = (target as StateTerminationDefinition).Parameters.ToList();
            PreconditionDrawer.PropertyField(rect, parameters, precondition, PlannerCustomTypeCache.TerminationPreconditionTypes);
        }

        void ShowPreconditionMenu(Rect rect, ReorderableList list)
        {
             if (PlannerCustomTypeCache.TerminationPreconditionTypes.Length == 0)
             {
                 list.serializedProperty.InsertArrayElement();
                 return;
             }

             PreconditionDrawer.ShowPreconditionMenu(serializedObject, list.serializedProperty, PlannerCustomTypeCache.TerminationPreconditionTypes);
        }

        void ShowRewardModifierMenu(Rect rect, ReorderableList list)
        {
            RewardModifierDrawer.ShowRewardModifierMenu(serializedObject, list.serializedProperty, PlannerCustomTypeCache.TerminationRewardTypes);
        }

        void DrawRewardModifierListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_RewardModifiers.serializedProperty;
            var rewardElement = list.GetArrayElementAtIndex(index);

            var parameters = (target as StateTerminationDefinition).Parameters.ToList();
            RewardModifierDrawer.PropertyField(rect, parameters, rewardElement, PlannerCustomTypeCache.TerminationRewardTypes);
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
