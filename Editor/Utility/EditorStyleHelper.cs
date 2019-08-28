using System;
using System.Collections;
using UnityEditor.Experimental;
using UnityEditorInternal;
using UnityEngine;

namespace UnityEditor.AI.Planner.Editors
{
    internal static class EditorStyleHelper
    {
        public static readonly GUIContent parameters = EditorGUIUtility.TrTextContent("Parameters");
        public static readonly GUIContent initialState = EditorGUIUtility.TrTextContent("Initial State Data");
        public static readonly GUIContent maxParametersReached = EditorGUIUtility.TrTextContent("Only a maximum of 16 parameters are supported");
        public static readonly GUIContent preconditions = EditorGUIUtility.TrTextContent("Preconditions");
        public static readonly GUIContent customPrecondition = EditorGUIUtility.TrTextContent("Custom Precondition");
        public static readonly GUIContent rewards = EditorGUIUtility.TrTextContent("Cost (-) / Reward (+)");
        public static readonly GUIContent fields = EditorGUIUtility.TrTextContent("Fields");
        public static readonly GUIContent values = EditorGUIUtility.TrTextContent("Values");
        public static readonly GUIContent usages = EditorGUIUtility.TrTextContent("Usages");
        public static readonly GUIContent criteria = EditorGUIUtility.TrTextContent("Criteria");
        public static readonly GUIContent actions = EditorGUIUtility.TrTextContent("Actions");
        public static readonly GUIContent terminations = EditorGUIUtility.TrTextContent("Terminations");

        public static readonly GUIContent objectsChanged = EditorGUIUtility.TrTextContent("Objects Modified");
        public static readonly GUIContent objectsRemoved = EditorGUIUtility.TrTextContent("Objects Removed");
        public static readonly GUIContent objectsCreated = EditorGUIUtility.TrTextContent("Objects Created");
        public static readonly GUIContent objectsCustom = EditorGUIUtility.TrTextContent("Custom effect");

        public static GUIStyle PopupStyle = new GUIStyle("ShurikenPopUp");
        public static GUIStyle PopupStyleBold = new GUIStyle("ShurikenPopUp");
        public static GUIStyle WhiteLargeLabel = new GUIStyle(EditorStyles.largeLabel);

        public static GUIStyle RequiredTraitLabel = new GUIStyle("AssetLabel");
        public static GUIStyle ProhibitedTraitLabel = new GUIStyle("AssetLabel");
        public static GUIStyle RequiredTraitAdd = new GUIStyle(RequiredTraitLabel);
        public static GUIStyle ProhibitedTraitAdd = new GUIStyle(ProhibitedTraitLabel);
        public static GUIStyle RequiredTraitMore = new GUIStyle(RequiredTraitLabel);
        public static GUIStyle ProhibitedTraitMore = new GUIStyle(ProhibitedTraitLabel);

        public static GUIStyle SmallIndex = new GUIStyle(EditorStyles.miniLabel);
        public static GUIStyle ErrorTextField = new GUIStyle(EditorStyles.textField);
        public static GUIStyle ExampleLabel = new GUIStyle(EditorStyles.label);

        public static GUIStyle DomainObjectBox = new GUIStyle("Box");

        public static GUIStyle IconButtonStyle;
        public static GUIContent GearIconPopup;

        static EditorStyleHelper()
        {
            PopupStyleBold.fontStyle = FontStyle.Bold;
            PopupStyleBold.fontSize = 11;

            PopupStyle.fontSize = 10;

            WhiteLargeLabel.normal.textColor = Color.white;

            var plannerResources = PlannerResources.instance;
            RequiredTraitLabel.normal.background = plannerResources.ImageRequiredTraitLabel;
            ProhibitedTraitLabel.normal.background = plannerResources.ImageProhibitedTraitLabel;
            RequiredTraitAdd.normal.background = plannerResources.ImageRequiredTraitAdd;
            ProhibitedTraitAdd.normal.background = plannerResources.ImageProhibitedTraitAdd;
            RequiredTraitMore.normal.background = plannerResources.ImageRequiredTraitMore;
            ProhibitedTraitMore.normal.background = plannerResources.ImageProhibitedTraitMore;

            SmallIndex.alignment = TextAnchor.MiddleRight;
            SmallIndex.normal.textColor = Color.gray;

            ErrorTextField.normal.textColor = new Color(1,.1f,.1f);
            ErrorTextField.focused.textColor = ErrorTextField.normal.textColor;
            ExampleLabel.normal.textColor = Color.gray;
            ExampleLabel.fontStyle = FontStyle.Italic;

            DomainObjectBox.padding = new RectOffset(0,0,0,4);

            IconButtonStyle = GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
            GearIconPopup = new GUIContent(EditorGUIUtility.Load("icons/d__Popup.png") as Texture2D);
        }

        internal static float IndentPosition => EditorGUI.indentLevel * 15f;
    }

    internal class NoHeaderReorderableList : UnityEditorInternal.ReorderableList
    {
        public NoHeaderReorderableList(SerializedObject serializedObject, SerializedProperty elements, ElementCallbackDelegate drawCallback, int rowByElement) : base(serializedObject, elements, true, false, true, true)
        {
            headerHeight = 2f;
            drawElementCallback = drawCallback;
            AdjustElementHeight(rowByElement);
        }

        internal void AdjustElementHeight(int rowByElement)
        {
            elementHeight = EditorGUIUtility.singleLineHeight * rowByElement + EditorGUIUtility.standardVerticalSpacing * (rowByElement + 2);
        }
    }
}
