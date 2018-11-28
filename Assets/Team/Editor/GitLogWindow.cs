using System.Collections.Generic;
using UniRx.EditorExtras.Editor;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Created by dpl ericdplau@yahoo.com
/// at 2018/08/23 23:37:43
/// </summary>
namespace UniRx.Team.Editor
{
    public class GitLogWindow : EditorWindow
    {
        static GitLogWindow window;

        [MenuItem("Assets/Git Logs", false, 161)]
        static void ShowGitLogWindow()
        {
            if (Selection.assetGUIDs.Length != 1)
                return;

            var target = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);

            if (System.IO.File.Exists(target))
                Show(target);
        }

        public static void Show(string filename)
        {
            if (window != null)
                window.Close();

            window = GetWindow<GitLogWindow>("Git Logs", true);
            window.Init(filename);
            window.Show();
        }

        List<Log> logs;
        string file;

        Vector2 _logsPanelScrollPos;
        bool _requiredRepaint;
        string _selectedSha1;

        void Init(string filename)
        {
            minSize = new Vector2(720, 420);

            logs = Git.GetLogs(filename, 20);
            file = filename;
        }

        void OnGUI()
        {
            if (logs == null || logs.Count == 0)
            {
                EditorGUILayout.HelpBox("Git Info not found.", MessageType.Error);
                return;
            }

            GUILayout.Label(file, EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            _logsPanelScrollPos = EditorGUILayout.BeginScrollView(_logsPanelScrollPos);
            DrawGitFileLogsPanel();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (_requiredRepaint)
            {
                _requiredRepaint = false;
                Repaint();
            }
        }

        void DrawGitFileLogsPanel()
        {
            foreach (var log in logs)
            {
                GUIStyle style = (log.Sha1 == _selectedSha1) ? EditorHelper.BoxDarkStyle : EditorHelper.BoxLightStyle;
                var rect = EditorGUILayout.BeginHorizontal(style);
                if (GUILayout.Button("checkout", GUILayout.Width(80)) &&
                    EditorUtility.DisplayDialog("Checkout Entry?", "Are you sure you want to checkout this entry?", "Yes", "No"))
                {
                    Git.Checkout(file, log.Sha1);
                    window.Close();
                }
                GUILayout.Label(log.Sha1, GUILayout.Width(70));
                GUILayout.Label(log.Message);
                GUILayout.Label(string.Format("{0} by {1}", log.Date.ToString("MM-dd HH:mm"), log.User), EditorHelper.RightLabelStyle, GUILayout.Width(140));
                if (GUILayout.Button("diff", GUILayout.Width(60)))
                {
                    if (_selectedSha1 != log.Sha1)
                    {
                        _requiredRepaint = true;
                        _selectedSha1 = log.Sha1;
                    }

                    Git.InvokeDiffTool(file, log.Sha1);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    if (_selectedSha1 != log.Sha1)
                    {
                        _requiredRepaint = true;
                        _selectedSha1 = log.Sha1;
                    }
                }
            }
        }
    }
}
