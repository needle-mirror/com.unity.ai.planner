using System;
using System.Linq;
using UnityEngine;

namespace UnityEditor.AI.Planner.Editors
{
    static class EditorStyleHelper
    {
        public static readonly GUIContent parameters = EditorGUIUtility.TrTextContent("Parameters");
        public static readonly GUIContent settings = EditorGUIUtility.TrTextContent("Settings");
        public static readonly GUIContent plannerSettings = EditorGUIUtility.TrTextContent("Planner Settings");
        public static readonly GUIContent includeObjects = EditorGUIUtility.TrTextContent("Include objects");
        public static readonly GUIContent worldQuery = EditorGUIUtility.TrTextContent("World query");
        public static readonly GUIContent worldQueryPreview = EditorGUIUtility.TrTextContent("World objects (preview)");
        public static readonly GUIContent localObjects = EditorGUIUtility.TrTextContent("Local objects");
        public static readonly GUIContent actionExecution = EditorGUIUtility.TrTextContent("Available actions from the plan");
        public static readonly GUIContent onActionStart = EditorGUIUtility.TrTextContent("Action Callback", "A callback to your code when the planner selects this action to execute");
        public static readonly GUIContent stateUpdate = EditorGUIUtility.TrTextContent("Next State Update", "After executing this action the planner must update it's internal state using one of these options");
        public static readonly GUIContent maxParametersReached = EditorGUIUtility.TrTextContent("Only a maximum of 16 parameters are supported");
        public static readonly GUIContent preconditions = EditorGUIUtility.TrTextContent("Preconditions");
        public static readonly GUIContent effects = EditorGUIUtility.TrTextContent("Effects");
        public static readonly GUIContent heuristic = EditorGUIUtility.TrTextContent("Heuristic");
        public static readonly GUIContent rewards = EditorGUIUtility.TrTextContent("Cost (-) / Reward (+)");
        public static readonly GUIContent terminalRewards = EditorGUIUtility.TrTextContent("Terminal Cost (-) / Reward (+)");
        public static readonly GUIContent fields = EditorGUIUtility.TrTextContent("Fields");
        public static readonly GUIContent values = EditorGUIUtility.TrTextContent("Values");
        public static readonly GUIContent usages = EditorGUIUtility.TrTextContent("Usages");
        public static readonly GUIContent criteria = EditorGUIUtility.TrTextContent("Criteria");
        public static readonly GUIContent actions = EditorGUIUtility.TrTextContent("Actions");
        public static readonly GUIContent terminations = EditorGUIUtility.TrTextContent("Terminations", tooltip: "For most applications, there is no need to continue planning beyond a point in which one or more termination conditions have been met. These conditions may represent the achievement of a desired goal or possibly the circumstances in which success has become impossible, such as when an agent has been defeated.");
        public static readonly GUIContent planSearchSettings = EditorGUIUtility.TrTextContent("Search Settings", tooltip: "For most applications the default search settings should be okay. However, if you want to ensure that the planner has planned long enough before an action is taken, then you can adjust these settings.");
        public static readonly GUIContent comparerLimit = EditorGUIUtility.TrTextContent("Limit", tooltip: "Maximum number of valid objects considered in the search for this parameter.");
        public static readonly GUIContent comparerOrder = EditorGUIUtility.TrTextContent("Order By", tooltip: "Specify a method used to order object list before the limit is applied.");

        public static readonly GUIContent objectsChanged = EditorGUIUtility.TrTextContent("Objects Modified");
        public static readonly GUIContent objectsRemoved = EditorGUIUtility.TrTextContent("Objects Removed");
        public static readonly GUIContent objectsCreated = EditorGUIUtility.TrTextContent("Objects Created");
        public static readonly GUIContent rewardModifiers = EditorGUIUtility.TrTextContent("Reward modifiers");

        public static GUIStyle listPopupStyle = new GUIStyle("ShurikenPopUp");
        public static GUIStyle listPopupStyleBold = new GUIStyle(listPopupStyle);
        public static GUIStyle listValueStyle = new GUIStyle("ShurikenValue");
        public static GUIStyle listValueStyleError = new GUIStyle(listValueStyle);

        public static GUIStyle namedObjectLabel = new GUIStyle(EditorStyles.largeLabel);
        public static GUIStyle lightGrayLargeLabel = new GUIStyle(EditorStyles.largeLabel);

        public static GUIStyle richTextField = new GUIStyle(EditorStyles.textField);

        public static GUIStyle requiredTraitLabel = new GUIStyle("AssetLabel");
        public static GUIStyle prohibitedTraitLabel = new GUIStyle("AssetLabel");
        public static GUIStyle requiredTraitAdd = new GUIStyle(requiredTraitLabel);
        public static GUIStyle prohibitedTraitAdd = new GUIStyle(prohibitedTraitLabel);
        public static GUIStyle requiredTraitMore = new GUIStyle(requiredTraitLabel);
        public static GUIStyle prohibitedTraitMore = new GUIStyle(prohibitedTraitLabel);

        public static GUIStyle smallIndex = new GUIStyle(EditorStyles.miniLabel);
        public static GUIStyle errorTextField = new GUIStyle(EditorStyles.textField);
        public static GUIStyle italicGrayLabel = new GUIStyle(EditorStyles.label);
        public static GUIStyle grayLabel = new GUIStyle(EditorStyles.label);
        public static GUIStyle lightGrayLabel = new GUIStyle(EditorStyles.label);

        public static GUIStyle traitBasedObjectBox = new GUIStyle("Box");
        public static GUIStyle traitBasedObjectTitleBox = new GUIStyle("Box");

        public static GUIStyle iconButtonStyle;
        public static GUIContent gearIconPopup;

        public static GUIStyle listElementDarkBackground = new GUIStyle();
        public static GUIStyle inspectorStyleLabel;
        public static GUIContent moreOptionsLabel;
        public static GUIStyle moreOptionsLabelStyle;
        public static Color moreOptionsActive = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f, 1f) : Color.gray;

        public static int subHeaderPaddingTop = 3;
        public static int subHeaderPaddingBottom = 10;

        static Texture2D s_DarkBackground;
        static Texture2D s_DarkBackgroundActive;

        static EditorStyleHelper()
        {
            s_DarkBackground = CreateSolidColorTexture(new Color(0.18f, 0.18f, 0.18f, 1f));
            s_DarkBackgroundActive = CreateSolidColorTexture(new Color(0.2f, 0.2f, 0.2f, 1f));

            listPopupStyleBold.fontStyle = FontStyle.Bold;
            listPopupStyleBold.fontSize = 11;
            listPopupStyleBold.fixedHeight = 19;

            listPopupStyle.fontSize = 10;
            listPopupStyle.fixedHeight = 19;

            listValueStyle.fixedHeight = 19;
            listValueStyleError.fixedHeight = 19;
            listValueStyleError.normal.textColor = Color.red;

            namedObjectLabel.normal.textColor = (EditorGUIUtility.isProSkin)?Color.white:Color.black;

            var plannerResources = PlannerResources.instance;
            requiredTraitLabel.normal.background = plannerResources.ImageRequiredTraitLabel;
            requiredTraitLabel.normal.scaledBackgrounds = null;
            prohibitedTraitLabel.normal.background = plannerResources.ImageProhibitedTraitLabel;
            prohibitedTraitLabel.normal.scaledBackgrounds = null;
            requiredTraitAdd.normal.background = plannerResources.ImageRequiredTraitAdd;
            requiredTraitAdd.normal.scaledBackgrounds = null;
            prohibitedTraitAdd.normal.background = plannerResources.ImageProhibitedTraitAdd;
            prohibitedTraitAdd.normal.scaledBackgrounds = null;
            requiredTraitMore.normal.background = plannerResources.ImageRequiredTraitMore;
            requiredTraitMore.normal.scaledBackgrounds = null;
            prohibitedTraitMore.normal.background = plannerResources.ImageProhibitedTraitMore;
            prohibitedTraitMore.normal.scaledBackgrounds = null;

            smallIndex.alignment = TextAnchor.MiddleRight;
            smallIndex.normal.textColor = Color.gray;

            errorTextField.normal.textColor = new Color(1,.1f,.1f);
            errorTextField.focused.textColor = errorTextField.normal.textColor;
            italicGrayLabel.normal.textColor = Color.gray;
            italicGrayLabel.fontStyle = FontStyle.Italic;

            grayLabel.normal.textColor = Color.gray;

            richTextField.richText = true;

            lightGrayLargeLabel.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            lightGrayLabel.normal.textColor = (EditorGUIUtility.isProSkin)?new Color(0.8f, 0.8f, 0.8f):new Color(0.6f, 0.6f, 0.6f);

            traitBasedObjectBox.padding = new RectOffset(0,0,0,4);

            if (EditorGUIUtility.isProSkin)
                traitBasedObjectTitleBox.normal.background = s_DarkBackground;
            traitBasedObjectTitleBox.normal.scaledBackgrounds = null;

            iconButtonStyle = GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
            gearIconPopup = new GUIContent(EditorGUIUtility.Load("icons/d__Popup.png") as Texture2D);

            listElementDarkBackground.normal.background = s_DarkBackground;
            listElementDarkBackground.active.background = s_DarkBackgroundActive;
            listElementDarkBackground.focused.background = s_DarkBackgroundActive;
            listElementDarkBackground.hover.background = s_DarkBackgroundActive;
            listElementDarkBackground.onNormal.background = s_DarkBackground;
            listElementDarkBackground.onActive.background = s_DarkBackgroundActive;
            listElementDarkBackground.onFocused.background = s_DarkBackgroundActive;
            listElementDarkBackground.onHover.background = s_DarkBackgroundActive;

            moreOptionsLabel = EditorGUIUtility.TrIconContent("MoreOptions", "More Options");
            moreOptionsLabelStyle = new GUIStyle(GUI.skin.label);
            moreOptionsLabelStyle.padding = new RectOffset(0, 0, 0, -1);

            inspectorStyleLabel = new GUIStyle
            {
                normal =
                {
                    textColor = Color.white,
                },
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
        }

        static Texture2D CreateSolidColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();

            return tex;
        }

        public static void DrawSplitter()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f);

            rect.xMin = 0f;
            rect.width += 4f;

            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, !EditorGUIUtility.isProSkin
                ? new Color(0.6f, 0.6f, 0.6f, 1.333f)
                : new Color(0.12f, 0.12f, 0.12f, 1.333f));
        }

        public static bool DrawSubHeader(GUIContent title, bool isExpanded, bool toggle = false, Action<bool> toggleMoreOptions = null)
        {
            const float height = 17f;

            DrawSplitter();

            var backgroundRect = GUILayoutUtility.GetRect(1f, height);

            var labelRect = backgroundRect;
            labelRect.xMin += 16f;
            labelRect.xMax -= 20f;

            var foldoutRect = backgroundRect;
            foldoutRect.y += 1f;
            foldoutRect.x += IndentPosition;
            foldoutRect.width = 13f;
            foldoutRect.height = 13f;
            backgroundRect.xMin = 0f;
            backgroundRect.width += 4f;

            // More options
            Rect moreOptionsRect = Rect.zero;
            if (toggleMoreOptions != null)
            {
                moreOptionsRect = backgroundRect;
                moreOptionsRect.x += moreOptionsRect.width - 20 - 1;
                moreOptionsRect.height = 15;
                moreOptionsRect.width = 16;
            }

            // Background full-width
            float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
            EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

            if (toggleMoreOptions != null)
            {
                if (toggle)
                    EditorGUI.DrawRect(moreOptionsRect, moreOptionsActive);

                var value = GUI.Toggle(moreOptionsRect, toggle, moreOptionsLabel, moreOptionsLabelStyle);

                toggleMoreOptions(value);
            }

            // Title
            isExpanded = GUI.Toggle(labelRect, isExpanded, title, EditorStyles.boldLabel);

            // Foldout
            isExpanded = GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, EditorStyles.foldout);

            return isExpanded;
        }

        internal static float IndentPosition => EditorGUI.indentLevel * 15f;

        public static string RichText(string text, string color, bool bold = false)
        {
            return bold ? $"<color={color}><b>{text}</b></color>" : $"<color={color}>{text}</color>";
        }

        public static void CustomMethodField(Rect rect, string typeName, Type[] types)
        {
            var customType = types.FirstOrDefault(c => c.FullName == typeName);

            if (customType == null)
            {
                EditorGUI.LabelField(rect, $"Unknown type {typeName}", listValueStyleError);
            }
            else
            {
                EditorGUI.LabelField(rect, customType.Name, listValueStyle);
            }
        }
    }

    class NoHeaderReorderableList : ReorderableList
    {
        public NoHeaderReorderableList(SerializedObject serializedObject, SerializedProperty elements, ElementCallbackDelegate drawCallback, int rowByElement) : base(serializedObject, elements, true, false, true, true)
        {
            headerHeight = 2f;
            drawElementCallback = drawCallback;
            AdjustElementHeight(rowByElement);
        }

        internal void AdjustElementHeight(int rowByElement)
        {
            elementHeight = CalcElementHeight(rowByElement);
        }

        public static float CalcElementHeight(int rowByElement)
        {
            return EditorGUIUtility.singleLineHeight * rowByElement + EditorGUIUtility.standardVerticalSpacing * (rowByElement + 2);
        }
    }
}
