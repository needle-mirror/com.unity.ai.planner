using System;
using System.Linq;
using Unity.AI.Planner.Traits;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(ProblemDefinition), true)]
    class ProblemDefinitionInspector : SaveableInspector
    {
        const string k_Default = "Default";

        NoHeaderReorderableList m_ActionList;
        NoHeaderReorderableList m_TerminationList;

        void OnEnable()
        {
            PlannerAssetDatabase.Refresh();
            PlannerCustomTypeCache.Refresh();

            InitializeReorderableLists();
        }

        void InitializeReorderableLists()
        {
            m_ActionList = new NoHeaderReorderableList(serializedObject,
                serializedObject.FindProperty("m_ActionDefinitions"), DrawActionListElement, 1);
            m_ActionList.onAddDropdownCallback += ShowAddActionMenu;
            m_ActionList.onRemoveCallback += RemoveAction;

            m_TerminationList = new NoHeaderReorderableList(serializedObject,
                serializedObject.FindProperty("m_StateTerminationDefinitions"), DrawTerminationListElement, 1);
            m_TerminationList.onAddDropdownCallback += ShowAddTerminationMenu;
            m_TerminationList.onRemoveCallback += RemoveTermination;
        }

        void OnDisable()
        {
            m_ActionList.onAddDropdownCallback -= ShowAddActionMenu;
            m_ActionList.onRemoveCallback -= RemoveAction;
            m_TerminationList.onAddDropdownCallback -= ShowAddTerminationMenu;
            m_TerminationList.onRemoveCallback -= RemoveTermination;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            m_ActionList.serializedProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.actions, m_ActionList.serializedProperty.isExpanded);
            if (m_ActionList.serializedProperty.isExpanded)
            {
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                m_ActionList.DoLayoutList();

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }

            m_TerminationList.serializedProperty.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.terminations, m_TerminationList.serializedProperty.isExpanded);
            if (m_TerminationList.serializedProperty.isExpanded)
            {
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                m_TerminationList.DoLayoutList();

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }

            var customCumulativeRewardEstimator = serializedObject.FindProperty("m_CustomCumulativeRewardEstimator");
            customCumulativeRewardEstimator.isExpanded = EditorStyleHelper.DrawSubHeader(EditorStyleHelper.plannerSettings, customCumulativeRewardEstimator.isExpanded, AIPlannerPreferences.displayProblemDefinitionAdvancedSettings, value => AIPlannerPreferences.displayProblemDefinitionAdvancedSettings = value);
            if (customCumulativeRewardEstimator.isExpanded)
            {
                GUILayout.Space(EditorStyleHelper.subHeaderPaddingTop);

                var rewardEstimatorTypeNames = PlannerCustomTypeCache.CumulativeRewardEstimatorTypes.Select(t => t.FullName).Prepend(string.Empty).ToArray();
                var rewardEstimatorTypeShortNames = PlannerCustomTypeCache.CumulativeRewardEstimatorTypes.Select(t => t.Name).Prepend(k_Default).ToArray();

                var rewardEstimatorIndex = Array.IndexOf(rewardEstimatorTypeNames, customCumulativeRewardEstimator.stringValue);
                EditorGUI.BeginChangeCheck();

                if (rewardEstimatorIndex == -1 && !string.IsNullOrEmpty(customCumulativeRewardEstimator.stringValue))
                {
                    GUI.backgroundColor = Color.red;
                    rewardEstimatorTypeShortNames = rewardEstimatorTypeShortNames.Append($"Unknown type {customCumulativeRewardEstimator.stringValue}").ToArray();
                    rewardEstimatorIndex = rewardEstimatorTypeShortNames.Length - 1;
                }

                rewardEstimatorIndex = EditorGUILayout.Popup(EditorStyleHelper.cumulativeRewardEstimator, rewardEstimatorIndex, rewardEstimatorTypeShortNames);

                if (EditorGUI.EndChangeCheck())
                {
                    if (rewardEstimatorIndex < rewardEstimatorTypeNames.Length)
                        customCumulativeRewardEstimator.stringValue = rewardEstimatorTypeNames[rewardEstimatorIndex];
                }
                GUI.backgroundColor = Color.white;

                if (rewardEstimatorIndex == 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PrefixLabel("Bounds");
                    }

                    var lowerEstimate = serializedObject.FindProperty("m_DefaultEstimateLower");
                    var averageEstimate = serializedObject.FindProperty("m_DefaultEstimateAverage");
                    var upperEstimate = serializedObject.FindProperty("m_DefaultEstimateUpper");
                    lowerEstimate.intValue = EditorGUILayout.IntField(lowerEstimate.intValue);
                    averageEstimate.intValue = EditorGUILayout.IntField(averageEstimate.intValue);
                    upperEstimate.intValue = EditorGUILayout.IntField(upperEstimate.intValue);
                    EditorGUILayout.EndHorizontal();
                }

                if (AIPlannerPreferences.displayProblemDefinitionAdvancedSettings)
                {
                    var discountFactor = serializedObject.FindProperty("DiscountFactor");
                    EditorGUILayout.PropertyField(discountFactor);
                }

                GUILayout.Space(EditorStyleHelper.subHeaderPaddingBottom);
            }

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        void DrawActionListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_ActionList.serializedProperty;
            var value = list.GetArrayElementAtIndex(index);

            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.height = EditorGUIUtility.singleLineHeight;

            if (value.objectReferenceValue != null)
            {
                var displayName = value.objectReferenceValue.name;

                string builtinModule;
                if ((builtinModule = PlannerAssetDatabase.GetBuiltinModuleName(value.objectReferenceValue)) != null)
                {
                    displayName = $"{displayName} <color=grey>({builtinModule})</color>";
                }

                if (GUI.Button(rect, displayName, EditorStyleHelper.richTextField))
                {
                    EditorGUIUtility.PingObject(value.objectReferenceValue);
                }
            }
            else
            {
                EditorGUI.LabelField(rect, "Action not found", EditorStyleHelper.grayLabel);
            }
        }

        void DrawTerminationListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var list = m_TerminationList.serializedProperty;
            var value = list.GetArrayElementAtIndex(index);

            rect.y += EditorGUIUtility.standardVerticalSpacing;
            rect.height = EditorGUIUtility.singleLineHeight;

            if (value.objectReferenceValue != null)
            {
                var displayName = value.objectReferenceValue.name;

                string builtinModule;
                if ((builtinModule = PlannerAssetDatabase.GetBuiltinModuleName(value.objectReferenceValue)) != null)
                {
                    displayName = $"{displayName} <color=grey>({builtinModule})</color>";
                }

                if (GUI.Button(rect, displayName, EditorStyleHelper.richTextField))
                {
                    EditorGUIUtility.PingObject(value.objectReferenceValue);
                }
            }
            else
            {
                EditorGUI.LabelField(rect, "Termination not found", EditorStyleHelper.grayLabel);
            }
        }

        void ShowAddActionMenu(Rect rect, ReorderableList list)
        {
            var menu = new GenericMenu();

            foreach (var action in PlannerAssetDatabase.ActionDefinitions)
            {
                var displayName = action.Name;

                string builtinModule;
                if ((builtinModule = PlannerAssetDatabase.GetBuiltinModuleName(action)) != null)
                {
                    displayName = $"{builtinModule}/{displayName}";
                }

                var alreadyInList = false;
                m_ActionList.serializedProperty.ForEachArrayElement(a => alreadyInList |= (a.objectReferenceValue == action));

                if (!alreadyInList)
                {
                    menu.AddItem(new GUIContent(displayName), false, () =>
                    {
                        serializedObject.Update();
                        var newActionProperty = list.serializedProperty.InsertArrayElement();
                        newActionProperty.objectReferenceValue = action;
                        serializedObject.ApplyModifiedProperties();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(displayName));
                }
            }

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("New Action..."), false, () =>
            {
                Object newAction;
                if ((newAction = PlannerAssetDatabase.CreateNewPlannerAsset<ActionDefinition>("Action")) != null)
                {
                    serializedObject.Update();
                    var newActionProperty = list.serializedProperty.InsertArrayElement();
                    newActionProperty.objectReferenceValue = newAction;
                    serializedObject.ApplyModifiedProperties();
                }
            });

            menu.ShowAsContext();
        }

        void ShowAddTerminationMenu(Rect rect, ReorderableList list)
        {
            var menu = new GenericMenu();

            foreach (var termination in PlannerAssetDatabase.StateTerminationDefinitions)
            {
                var displayName = termination.Name;

                string builtinModule;
                if ((builtinModule = PlannerAssetDatabase.GetBuiltinModuleName(termination)) != null)
                {
                    displayName = $"{builtinModule}/{displayName}";
                }

                var alreadyInList = false;
                m_TerminationList.serializedProperty.ForEachArrayElement(a => alreadyInList |= (a.objectReferenceValue == termination));

                if (!alreadyInList)
                {
                    menu.AddItem(new GUIContent(displayName), false, () =>
                    {
                        serializedObject.Update();
                        var newTerminationProperty = list.serializedProperty.InsertArrayElement();
                        newTerminationProperty.objectReferenceValue = termination;
                        serializedObject.ApplyModifiedProperties();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(displayName));
                }
            }

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("New Termination..."), false, () =>
            {
                Object mewTermination;
                if ((mewTermination = PlannerAssetDatabase.CreateNewPlannerAsset<StateTerminationDefinition>("Termination")) != null)
                {
                    serializedObject.Update();
                    var newTerminationProperty = list.serializedProperty.InsertArrayElement();
                    newTerminationProperty.objectReferenceValue = mewTermination;
                    serializedObject.ApplyModifiedProperties();
                }
            });

            menu.ShowAsContext();
        }

        void RemoveAction(ReorderableList list)
        {
            m_ActionList.serializedProperty.ForceDeleteArrayElementAtIndex(list.index);
        }

        void RemoveTermination(ReorderableList list)
        {
            m_TerminationList.serializedProperty.ForceDeleteArrayElementAtIndex(list.index);
        }


    }
}
