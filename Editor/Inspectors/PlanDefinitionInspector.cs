using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Planner;
using UnityEditor.AI.Planner.Utility;
using UnityEngine;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;
using Object = UnityEngine.Object;

namespace UnityEditor.AI.Planner.Editors
{
    [CustomEditor(typeof(PlanDefinition), true)]
    class PlanDefinitionInspector : SaveableInspector
    {
        const string k_Default = "Default";

        List<Type> m_CustomHeuristicTypes = new List<Type>();

        NoHeaderReorderableList m_ActionList;
        NoHeaderReorderableList m_TerminationList;

        void OnEnable()
        {
            m_ActionList = new NoHeaderReorderableList(serializedObject,
                serializedObject.FindProperty("m_ActionDefinitions"), DrawActionListElement, 1);
            m_ActionList.onAddDropdownCallback += ShowAddActionMenu;
            m_ActionList.onRemoveCallback += RemoveAction;

            m_TerminationList = new NoHeaderReorderableList(serializedObject,
                serializedObject.FindProperty("m_StateTerminationDefinitions"), DrawTerminationListElement, 1);
            m_TerminationList.onAddDropdownCallback += ShowAddTerminationMenu;
            m_TerminationList.onRemoveCallback += RemoveTermination;

            m_CustomHeuristicTypes.Clear();
            typeof(ICustomHeuristic<>).GetImplementationsOfInterface(m_CustomHeuristicTypes);

            DomainAssetDatabase.Refresh();
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

            GUILayout.Label(EditorStyleHelper.actions, EditorStyles.boldLabel);
            m_ActionList.DoLayoutList();

            GUILayout.Label(EditorStyleHelper.terminations, EditorStyles.boldLabel);
            m_TerminationList.DoLayoutList();

            GUILayout.Label(EditorStyleHelper.planSearchSettings, EditorStyles.boldLabel);

            var customHeuristic = serializedObject.FindProperty("m_CustomHeuristic");
            var heuristicTypeNames = m_CustomHeuristicTypes.Where(t => !t.IsGenericType).Select(t => t.FullName).Prepend(k_Default).ToArray();

            var heuristicIndex = Math.Max(0, Array.IndexOf(heuristicTypeNames, customHeuristic.stringValue));
            EditorGUI.BeginChangeCheck();
            heuristicIndex = EditorGUILayout.Popup(EditorStyleHelper.heuristic, heuristicIndex, heuristicTypeNames);
            if (EditorGUI.EndChangeCheck())
            {
                var customHeuristicName = heuristicTypeNames[heuristicIndex];
                customHeuristic.stringValue = heuristicIndex == 0 ? string.Empty : customHeuristicName;
            }

            if (heuristicIndex == 0)
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PrefixLabel("Bounds");
                }

                var lowerHeuristic = serializedObject.FindProperty("m_DefaultHeuristicLower");
                var averageHeuristic = serializedObject.FindProperty("m_DefaultHeuristicAverage");
                var upperHeuristic = serializedObject.FindProperty("m_DefaultHeuristicUpper");
                lowerHeuristic.intValue = EditorGUILayout.IntField(lowerHeuristic.intValue);
                averageHeuristic.intValue = EditorGUILayout.IntField(averageHeuristic.intValue);
                upperHeuristic.intValue = EditorGUILayout.IntField(upperHeuristic.intValue);
                EditorGUILayout.EndHorizontal();
            }

            var discountFactor = serializedObject.FindProperty("DiscountFactor");
            EditorGUILayout.PropertyField(discountFactor);

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
                if ((builtinModule = DomainAssetDatabase.GetBuiltinModuleName(value.objectReferenceValue)) != null)
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
                if ((builtinModule = DomainAssetDatabase.GetBuiltinModuleName(value.objectReferenceValue)) != null)
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

            foreach (var action in DomainAssetDatabase.ActionDefinitions)
            {
                var displayName = action.Name;

                string builtinModule;
                if ((builtinModule = DomainAssetDatabase.GetBuiltinModuleName(action)) != null)
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
                if ((newAction = DomainAssetDatabase.CreateNewPlannerAsset<ActionDefinition>("Action")) != null)
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

            foreach (var termination in DomainAssetDatabase.StateTerminationDefinitions)
            {
                var displayName = termination.Name;

                string builtinModule;
                if ((builtinModule = DomainAssetDatabase.GetBuiltinModuleName(termination)) != null)
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
                if ((mewTermination = DomainAssetDatabase.CreateNewPlannerAsset<StateTerminationDefinition>("Termination")) != null)
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
