using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Created by dpl ericdplau@yahoo.com
/// at 2018/09/02 7:31:19
/// </summary>
namespace UniRx.EditorExtras.Editor
{
    public static partial class EditorHelper
    {
        /// <summary>
        /// 创建动态 Texture2D，并设置背景颜色。
        /// </summary>
        /// <param name="color">背景颜色</param>
        /// <returns></returns>
        public static Texture2D MakeColoredTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();

            return texture;
        }

        public static void CleanupSubEditors(UnityEditor.Editor[] subEditors)
        {
            if (subEditors == null) return;
            for (var i = 0; i < subEditors.Length; i++)
            {
                if (subEditors[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(subEditors[i]);
                    subEditors[i] = null;
                }
            }
        }

        public static bool DrawDefaultInspector(SerializedObject obj, List<string> excludePaths)
        {
            obj.Update();
            var changed = DrawPropertiesWithExclusions(obj, excludePaths);
            obj.ApplyModifiedProperties();
            return changed;
        }

        public static bool DrawPropertiesWithExclusions(SerializedObject obj, List<string> excludePaths)
        {
            EditorGUI.BeginChangeCheck();
            SerializedProperty iterator = obj.GetIterator();

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                if (excludePaths == null || !excludePaths.Contains(iterator.propertyPath))
                    EditorGUILayout.PropertyField(iterator, true);

                enterChildren = false;
            }

            return EditorGUI.EndChangeCheck();
        }

        public static bool DrawProperties(SerializedObject obj, List<string> paths)
        {
            EditorGUI.BeginChangeCheck();

            foreach (var path in paths)
            {
                SerializedProperty property = obj.FindProperty(path);

                if (property != null)
                    EditorGUILayout.PropertyField(property, true);
            }

            return EditorGUI.EndChangeCheck();
        }

        static Rect _resizeRect = Rect.zero;
        static bool _isResizing = false;

        /// <summary>
        /// 在水平矩形区域之后画一个水平分割器，并返回 GUI 是否需要重画。
        /// </summary>
        /// <param name="rect">水平矩形区域，如 rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(rectHeight))</param>
        /// <param name="rectHeight">水平矩形区域的动态高度</param>
        /// <returns></returns>
        public static bool DrawHorizontalResizerAfterRect(Rect rect, ref float rectHeight)
        {
            _resizeRect.Set(rect.x, rect.y + rect.height, rect.width, 5f);

            GUI.DrawTexture(_resizeRect, TransparentBackground);
            EditorGUIUtility.AddCursorRect(_resizeRect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.MouseDown && _resizeRect.Contains(Event.current.mousePosition))
                _isResizing = true;

            if (_isResizing)
                rectHeight = Mathf.Max(50f, Event.current.mousePosition.y - rect.y);

            if (Event.current.type == EventType.MouseUp)
                _isResizing = false;

            return _isResizing;
        }

        /// <summary>
        /// 在垂直矩形区域之后画一个垂直分割器，并返回 GUI 是否需要重画。
        /// </summary>
        /// <param name="rect">垂直矩形区域，如 rect = EditorGUILayout.BeginVertical(GUILayout.Width(rectWidth))</param>
        /// <param name="rectWidth">垂直矩形区域的动态高度</param>
        /// <returns></returns>
        public static bool DrawVerticalResizerAfterRect(Rect rect, ref float rectWidth)
        {
            _resizeRect.Set(rect.x + rectWidth, rect.y, 5f, rect.height);

            GUI.DrawTexture(_resizeRect, TransparentBackground);
            EditorGUIUtility.AddCursorRect(_resizeRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && _resizeRect.Contains(Event.current.mousePosition))
                _isResizing = true;

            if (_isResizing)
                rectWidth = Mathf.Max(50f, Event.current.mousePosition.x - rect.x - 5f);

            if (Event.current.type == EventType.MouseUp)
                _isResizing = false;

            return _isResizing;
        }
        public const float RemoveButtonWidth = 30f;

        public static GUIStyle ToolbarSearchFieldCancelEmpty
        {
            get { return _toolbarSearchFieldCancelEmpty ?? (_toolbarSearchFieldCancelEmpty = GUI.skin.GetStyle("ToolbarSeachCancelButtonEmpty")); }
        }
        static GUIStyle _toolbarSearchFieldCancelEmpty;

        public static GUIStyle ToolbarSearchFieldCancel
        {
            get { return _toolbarSearchFieldCancel ?? (_toolbarSearchFieldCancel = GUI.skin.GetStyle("ToolbarSeachCancelButton")); }
        }
        static GUIStyle _toolbarSearchFieldCancel;

        public static GUIStyle ToolbarSearchField
        {
            get { return _toolbarSearchField ?? (_toolbarSearchField = GUI.skin.GetStyle("ToolbarSeachTextField")); }
        }
        static GUIStyle _toolbarSearchField;

        /// <summary>
        /// 透明的 Texture2D
        /// </summary>
        public static Texture2D TransparentBackground
        {
            get
            {
                if (_transparentBackground == null)
                    _transparentBackground = MakeColoredTexture(new Color(255, 255, 255, 0));

                return _transparentBackground;
            }
        }

        static Texture2D _transparentBackground;

        /// <summary>
        /// 无边框按钮样式
        /// </summary>
        public static GUIStyle BorderlessButtonStyle
        {
            get
            {
                if (_borderlessButtonStyle == null)
                {
                    _borderlessButtonStyle = new GUIStyle(GUI.skin.button)
                    {
                        normal = {
                            background = EditorGUIUtility.isProSkin
                                ? MakeColoredTexture(new Color(1f, 1f, 1f, 0.2f))
                                : MakeColoredTexture(new Color(.8f, .8f, .8f, 0.4f))
                        },
                        padding = new RectOffset(0, 0, 0, 0),
                        fontSize = 10
                    };
                }

                return _borderlessButtonStyle;
            }
        }

        static GUIStyle _borderlessButtonStyle;

        public static bool ButtonTrimmed(string text, GUIStyle style)
        {
            return GUILayout.Button(text, style, GUILayout.MaxWidth(style.CalcSize(new GUIContent(text)).x));
        }

        public static bool ToggleTrimmed(bool value, string text, GUIStyle style)
        {
            return GUILayout.Toggle(value, text, style, GUILayout.MaxWidth(style.CalcSize(new GUIContent(text)).x));
        }

        public static void LabelTrimmed(string text, GUIStyle style)
        {
            GUILayout.Label(text, style, GUILayout.MaxWidth(style.CalcSize(new GUIContent(text)).x));
        }

        /// <summary>
        /// 可拖放区域 Box 样式
        /// </summary>
        public static GUIStyle DropAreaStyle
        {
            get
            {
                if (_dropAreaStyle == null)
                {
                    _dropAreaStyle = new GUIStyle(GUI.skin.box)
                    {
                        normal = {
                            background = EditorGUIUtility.isProSkin
                                ? MakeColoredTexture(new Color(1f, 1f, 1f, 0.2f))
                                : MakeColoredTexture(new Color(1f, 1f, 1f, 0.6f))
                        },
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 14
                    };
                }

                return _dropAreaStyle;
            }
        }

        static GUIStyle _dropAreaStyle;

        /// <summary>
        /// 浅色 Box 区域样式
        /// </summary>
        public static GUIStyle BoxLightStyle
        {
            get
            {
                if (_boxLightStyle == null)
                {
                    _boxLightStyle = new GUIStyle(GUI.skin.box)
                    {
                        normal = {
                            background = EditorGUIUtility.isProSkin
                                ? MakeColoredTexture(new Color(0.5f, 0.5f, 0.5f, 0.2f))
                                : MakeColoredTexture(new Color(1f, 1f, 1f, 0.4f))
                            }
                    };
                }

                return _boxLightStyle;
            }
        }

        static GUIStyle _boxLightStyle;

        /// <summary>
        /// 深色 Box 区域样式
        /// </summary>
        public static GUIStyle BoxDarkStyle
        {
            get
            {
                if (_boxDarkStyle == null)
                {
                    _boxDarkStyle = new GUIStyle(GUI.skin.box)
                    {
                        normal = {
                            background = EditorGUIUtility.isProSkin
                                ? MakeColoredTexture(new Color(0f, 0f, 0.7f, 1f))
                                : MakeColoredTexture(new Color(0.8f, 1f, 0.6f, 0.75f))
                            }
                    };
                }

                return _boxDarkStyle;
            }
        }

        static GUIStyle _boxDarkStyle;

        /// <summary>
        /// 自动折行输入框样式
        /// </summary>
        public static GUIStyle WordWrapTextField
        {
            get
            {
                if (_wordWrapTextField == null)
                {
                    _wordWrapTextField = new GUIStyle(GUI.skin.textField)
                    {
                        wordWrap = true
                    };
                }

                return _wordWrapTextField;
            }
        }

        static GUIStyle _wordWrapTextField;

        /// <summary>
        /// 文字居中标签样式
        /// </summary>
        public static GUIStyle CenteredLabelStyle
        {
            get
            {
                if (_centeredLabelStyle == null)
                {
                    _centeredLabelStyle = new GUIStyle
                    {
                        alignment = TextAnchor.UpperCenter
                    };
                }

                return _centeredLabelStyle;
            }
        }

        static GUIStyle _centeredLabelStyle;

        /// <summary>
        /// 文字右对齐标签样式
        /// </summary>
        public static GUIStyle RightLabelStyle
        {
            get
            {
                if (_rightLabelStyle == null)
                {
                    _rightLabelStyle = new GUIStyle
                    {
                        alignment = TextAnchor.UpperRight
                    };
                }

                return _rightLabelStyle;
            }
        }

        static GUIStyle _rightLabelStyle;

        public static GUIStyle BlueLabelStyle
        {
            get
            {
                if (_blueLabelStyle == null)
                {
                    _blueLabelStyle = new GUIStyle
                    {
                        normal = { textColor = Color.blue }
                    };
                }

                return _blueLabelStyle;
            }
        }

        static GUIStyle _blueLabelStyle;

        public static GUIStyle RedLabelStyle
        {
            get
            {
                if (_redLabelStyle == null)
                {
                    _redLabelStyle = new GUIStyle
                    {
                        normal = { textColor = Color.red }
                    };
                }

                return _redLabelStyle;
            }
        }

        static GUIStyle _redLabelStyle;

        public static GUIStyle HelpAreaYellowStyle
        {
            get
            {
                if (_helpAreaYellowStyle == null)
                {
                    _helpAreaYellowStyle = new GUIStyle(GUI.skin.box)
                    {
                        normal = {
                            background = EditorGUIUtility.isProSkin
                                ? MakeColoredTexture(new Color(1f, 1f, 0f, 0.2f))
                                : MakeColoredTexture(new Color(1f, 1f, 0f, 0.6f))
                        },
                        alignment = TextAnchor.UpperLeft,
                        wordWrap = true,
                        fontSize = 12
                    };
                }

                return _helpAreaYellowStyle;
            }
        }

        static GUIStyle _helpAreaYellowStyle;

        public static GUIStyle HelpAreaStyle
        {
            get
            {
                if (_helpAreaStyle == null)
                {
                    _helpAreaStyle = new GUIStyle(GUI.skin.box)
                    {
                        normal = {
                            background = EditorGUIUtility.isProSkin
                                ? MakeColoredTexture(new Color(0.5f, 0.5f, 0.5f, 0.2f))
                                : MakeColoredTexture(new Color(1f, 1f, 1f, 0.4f))
                        },
                        alignment = TextAnchor.UpperLeft,
                        wordWrap = true,
                        fontSize = 12
                    };
                }

                return _helpAreaStyle;
            }
        }

        static GUIStyle _helpAreaStyle;

        public static GUIStyle BoldLabelStyle
        {
            get
            {
                if (_boldLabelStyle == null)
                {
                    _boldLabelStyle = new GUIStyle
                    {
                        fontStyle = FontStyle.Bold
                    };
                }

                return _boldLabelStyle;
            }
        }

        static GUIStyle _boldLabelStyle;

        public static GUIStyle HeaderLabelStyle
        {
            get { return BoldLabelStyle; }
        }
    }
}
