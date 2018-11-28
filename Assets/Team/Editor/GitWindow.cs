using System.Collections.Generic;
using UniRx.EditorExtras.Editor;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Created by dpl ericdplau@yahoo.com
/// at 2018/08/24 0:01:37
/// </summary>
namespace UniRx.Team.Editor
{
    public class GitWindow : EditorWindow
    {
        [MenuItem("Window/Git", priority = 202)]
        public static void ShowWindow()
        {
            GetWindow<GitWindow>("Git", true);
        }

        readonly string[] _mainTabs = { "Local", "Logs", "Remote" };
        int _lastTabIndex = 0;
        int _mainTabIndex = 0;

        Vector2 _unstagedAreaScrollPos;
        float _unstagedAreaHeight = 250f;

        Vector2 _stagedAreaScrollPos;
        Vector2 _commentsScrollPos;
        string _comments;

        Vector2 _updatesAreaScrollPos;
        Vector2 _readyToPushScrollPos;
        float _updatesAreaHeight = 320f;

        bool _upstausRequired;
        bool _repaintRequired;
        bool _isResizing;

        Vector2 _logsAreaScrollPos;
        float _logsAreaHeight = 250f;
        Vector2 _logFilesAreaScrollPos;

        List<Log> _logs = new List<Log>();
        List<File> _logFiles = new List<File>();
        string _currentLogFile;
        Log _currentLog;

        void OnEnable()
        {
            maxSize = new Vector2(960, 720);
            minSize = new Vector2(400, 400);

            Upstatus();
        }

        void Update()
        {
            if (_upstausRequired && Git.IsReady)
            {
                _upstausRequired = false;

                Upstatus();
                Repaint();
            }
        }

        void Upstatus()
        {
            if (_lastTabIndex != 1)
                Git.RefreshStatus(_lastTabIndex == 2);
            else
                _logs = Git.GetLogs(".", 10);
        }

        void OnGUI()
        {
            _mainTabIndex = GUILayout.Toolbar(_mainTabIndex, _mainTabs);

            switch (_mainTabIndex)
            {
                case 0:
                    DrawLocalManagerPanel();
                    break;
                case 1:
                    DrawLogsPanel();
                    break;
                case 2:
                    DrawRemoteManagerPanel();
                    break;
            }

            if (_isResizing || _repaintRequired)
            {
                _repaintRequired = false;
                Repaint();
            }

            if (_lastTabIndex != _mainTabIndex)
            {
                _lastTabIndex = _mainTabIndex;
                Upstatus();
            }
        }

        void DrawLocalManagerPanel()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Unstaged Files", EditorStyles.boldLabel);
            if (GUILayout.Button(" ↓ Add All", GUILayout.Width(80)))
            {
                Git.Add();
                _upstausRequired = true;
            }
            if (GUILayout.Button(new GUIContent(" ↓ Stage Selected"), GUILayout.Width(110)))
            {
                if (Git.AddSelected())
                    _upstausRequired = true;
            }
            if (GUILayout.Button("Select None", GUILayout.Width(80)))
            {
                foreach (var file in Git.LocalChangedFiles)
                {
                    if (file.HasStatus(EStatus.HasUnstagedChanges))
                        file.Toggled = false;
                }
            }
            EditorGUILayout.EndHorizontal();
            // --------------------------------------------------------------------------------------------

            var rect = EditorGUILayout.BeginHorizontal("TextArea", GUILayout.Height(_unstagedAreaHeight));
            _unstagedAreaScrollPos = EditorGUILayout.BeginScrollView(_unstagedAreaScrollPos);
            EditorGUILayout.BeginVertical();

            foreach (var file in Git.LocalChangedFiles)
            {
                if (!file.HasStatus(EStatus.HasUnstagedChanges))
                    continue;

                var style = EditorStyles.label;

                if (file.HasStatus(EStatus.HasStagedChanges))
                    style = EditorHelper.BlueLabelStyle;
                else if (file.IsDeleted)
                    style = EditorHelper.RedLabelStyle;

                EditorGUILayout.BeginHorizontal();

                file.Toggled = EditorGUILayout.Toggle(file.Toggled, GUILayout.Width(25));
                EditorGUILayout.LabelField(file.StatusTag, style, GUILayout.Height(18), GUILayout.Width(25));
                EditorGUILayout.SelectableLabel(file.Path, style, GUILayout.Height(18));

                if (Git.RemoteUpdateFiles.Find(x => x.Path == file.Path) != null)
                    EditorGUILayout.LabelField("[C]", EditorHelper.RedLabelStyle, GUILayout.Height(18), GUILayout.Width(25));

                if (file.IsModified && GUILayout.Button("dicard", GUILayout.Width(50)) &&
                    EditorUtility.DisplayDialog("Discard Entry?", "Are you sure you want to discard this entry?", "Yes", "No"))
                {
                    Git.Discard(file);
                    EditorGUILayout.EndHorizontal();
                    _upstausRequired = true;
                    break;
                }
                if (file.IsModified && GUILayout.Button(new GUIContent("diff"), GUILayout.Width(50)))
                {
                    Git.InvokeDiffTool(file.Path);
                }
                if (GUILayout.Button(new GUIContent("+", "Adds to staged"), GUILayout.Width(20)))
                {
                    Git.Add(file);
                    EditorGUILayout.EndHorizontal();
                    _upstausRequired = true;
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();

            // --------------------------------------------------------------------------------------------
            _isResizing = EditorHelper.DrawHorizontalResizerAfterRect(rect, ref _unstagedAreaHeight);
            _unstagedAreaHeight = Mathf.Min(_unstagedAreaHeight, position.height - 200f);
            // --------------------------------------------------------------------------------------------

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Staged Files", EditorStyles.boldLabel);
            if (GUILayout.Button(" ↑ Reset All", GUILayout.Width(80)))
            {
                Git.Reset();
                _upstausRequired = true;
            }
            if (GUILayout.Button(new GUIContent(" ↑ Reset Selected"), GUILayout.Width(110)))
            {
                if (Git.ResetSelectd())
                    _upstausRequired = true;
            }
            if (GUILayout.Button("Select None", GUILayout.Width(80)))
            {
                foreach (var file in Git.LocalChangedFiles)
                {
                    if (file.HasStatus(EStatus.HasStagedChanges))
                        file.Toggled = false;
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);

            // --------------------------------------------------------------------------------------------

            EditorGUILayout.BeginHorizontal("TextArea");
            _stagedAreaScrollPos = EditorGUILayout.BeginScrollView(_stagedAreaScrollPos);
            EditorGUILayout.BeginVertical();

            foreach (var file in Git.LocalChangedFiles)
            {
                if (!file.HasStatus(EStatus.HasStagedChanges))
                    continue;

                var style = EditorStyles.label;

                if (file.HasStatus(EStatus.HasUnstagedChanges))
                    style = EditorHelper.BlueLabelStyle;
                else if (file.IsDeleted)
                    style = EditorHelper.RedLabelStyle;

                EditorGUILayout.BeginHorizontal();

                file.Toggled = EditorGUILayout.Toggle(file.Toggled, GUILayout.Width(25));
                EditorGUILayout.LabelField(file.StatusTag, style, GUILayout.Height(18), GUILayout.Width(25));
                EditorGUILayout.SelectableLabel(file.Path, style, GUILayout.Height(18));

                if (file.IsModified && GUILayout.Button(new GUIContent("diff"), GUILayout.Width(50)))
                {
                    Git.InvokeDiffTool(file.Path);
                }
                if (GUILayout.Button(new GUIContent("-", "Reset to unstated"), GUILayout.Width(20)))
                {
                    Git.Reset(file);
                    EditorGUILayout.EndHorizontal();
                    _upstausRequired = true;
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (file.HasStatus(EStatus.Renamed) && !string.IsNullOrEmpty(file.SourcePath))
                {
                    EditorGUILayout.BeginHorizontal();
                    file.Toggled = EditorGUILayout.Toggle(file.Toggled, GUILayout.Width(25));
                    EditorGUILayout.LabelField("D ", EditorHelper.RedLabelStyle, GUILayout.Height(18), GUILayout.Width(25));
                    EditorGUILayout.SelectableLabel(file.SourcePath, EditorHelper.RedLabelStyle, GUILayout.Height(18));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();

            // --------------------------------------------------------------------------------------------

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            _commentsScrollPos = GUILayout.BeginScrollView(_commentsScrollPos, GUILayout.Height(50));
            _comments = EditorGUILayout.TextArea(_comments, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            if (GUILayout.Button(new GUIContent("Commit"), GUILayout.Height(50), GUILayout.ExpandWidth(true)) && !string.IsNullOrEmpty(_comments))
            {
                Git.Commit(_comments); _comments = "";
                _upstausRequired = true;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        void DrawRemoteManagerPanel()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Branch " + Git.BranchInfomation);
            if (!string.IsNullOrEmpty(Git.Remote) && GUILayout.Button(new GUIContent("Refresh"), GUILayout.Width(70)))
            {
                _upstausRequired = true;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            // --------------------------------------------------------------------------------------------

            var rect = EditorGUILayout.BeginHorizontal("TextArea", GUILayout.Height(_updatesAreaHeight));
            _updatesAreaScrollPos = EditorGUILayout.BeginScrollView(_updatesAreaScrollPos);
            EditorGUILayout.BeginVertical();

            bool updatable = Git.RemoteUpdateFiles.Count > 0;

            foreach (var file in Git.RemoteUpdateFiles)
            {
                bool isConflict = Git.LocalChangedFiles.Find(x => x.Path == file.Path) != null;

                if (updatable && isConflict)
                    updatable = false;

                if (!isConflict)
                    isConflict = Git.LocalPushingFiles.Find(x => x.Path == file.Path) != null;

                EditorGUILayout.BeginHorizontal();

                if (isConflict)
                    EditorGUILayout.SelectableLabel(file.Path, EditorHelper.RedLabelStyle, GUILayout.Height(18));
                else
                    EditorGUILayout.SelectableLabel(file.Path, GUILayout.Height(18));

                if (isConflict)
                    file.DiscardRemote = EditorGUILayout.ToggleLeft("Discard Remote", file.DiscardRemote, EditorHelper.RedLabelStyle, GUILayout.Width(110));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();

            // --------------------------------------------------------------------------------------------
            _isResizing = EditorHelper.DrawHorizontalResizerAfterRect(rect, ref _updatesAreaHeight);
            _updatesAreaHeight = Mathf.Min(_updatesAreaHeight, position.height - 150f);
            // --------------------------------------------------------------------------------------------

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Pushing Files", EditorStyles.boldLabel);
            if (updatable && GUILayout.Button(new GUIContent("Update"), GUILayout.Width(70)))
            {
                Git.Pull();
                _upstausRequired = true;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);

            // --------------------------------------------------------------------------------------------

            EditorGUILayout.BeginHorizontal("TextArea");
            _readyToPushScrollPos = EditorGUILayout.BeginScrollView(_readyToPushScrollPos);
            EditorGUILayout.BeginVertical();

            bool pushable = Git.LocalPushingFiles.Count > 0;

            foreach (var file in Git.LocalPushingFiles)
            {
                EditorGUILayout.SelectableLabel(file.Path, GUILayout.Height(18));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();

            // --------------------------------------------------------------------------------------------

            EditorGUILayout.BeginHorizontal();
            if (Git.BranchBehind == 0)
            {
                GUILayout.FlexibleSpace();
                if (!updatable && pushable && GUILayout.Button(new GUIContent("Push"), GUILayout.Width(70)))
                {
                    Git.Push();
                    _upstausRequired = true;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawLogsPanel()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Recent Logs", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            // --------------------------------------------------------------------------------------------

            var rect = EditorGUILayout.BeginHorizontal("TextArea", GUILayout.Height(_logsAreaHeight));
            _logsAreaScrollPos = EditorGUILayout.BeginScrollView(_logsAreaScrollPos);
            EditorGUILayout.BeginVertical();

            foreach (var log in _logs)
            {
                GUIStyle style = (_currentLog != null && log.Sha1 == _currentLog.Sha1) ? EditorHelper.BoxDarkStyle : EditorHelper.BoxLightStyle;

                var area = EditorGUILayout.BeginHorizontal(style);
                GUILayout.Label(log.Sha1, GUILayout.Width(70));
                GUILayout.Label(log.Message);
                GUILayout.Label(string.Format("{0} by {1}", log.Date.ToString("MM-dd HH:mm"), log.User), GUILayout.Width(140));
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);

                if (Event.current.type == EventType.MouseDown && area.Contains(Event.current.mousePosition))
                {
                    if (_currentLog == null || _currentLog.Sha1 != log.Sha1)
                    {
                        _repaintRequired = true;
                        _logFiles = Git.GetHeadFiles(log.Sha1);
                        _currentLogFile = null;
                        _currentLog = log;
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();

            // --------------------------------------------------------------------------------------------
            _isResizing = EditorHelper.DrawHorizontalResizerAfterRect(rect, ref _logsAreaHeight);
            _logsAreaHeight = Mathf.Min(_logsAreaHeight, position.height - 200f);
            // --------------------------------------------------------------------------------------------

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Log Files", GUILayout.Width(80));

            if (!_repaintRequired && _currentLog != null)
            {
                GUILayout.Label(string.Format("{0} by {1} : {2}",
                    _currentLog.Date.ToString("MM-dd HH:mm"), _currentLog.User, _currentLog.Message));
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);

            // --------------------------------------------------------------------------------------------

            EditorGUILayout.BeginHorizontal("TextArea");
            _logFilesAreaScrollPos = EditorGUILayout.BeginScrollView(_logFilesAreaScrollPos);
            EditorGUILayout.BeginVertical();

            if (!_repaintRequired && _currentLog != null)
            {
                int safeId = 0;

                foreach (var file in _logFiles)
                {
                    if (++safeId > 50)
                        break;

                    GUIStyle style = (_currentLogFile == file.Path) ? EditorHelper.BoxDarkStyle : EditorHelper.BoxLightStyle;
                    var area = EditorGUILayout.BeginHorizontal(style);

                    if (GUILayout.Button("checkout", GUILayout.Width(80)) &&
                        EditorUtility.DisplayDialog("Checkout Entry?", "Are you sure you want to checkout this entry?", "Yes", "No"))
                    {
                        Git.Checkout(file, _currentLog.Sha1);
                    }

                    GUILayout.Label(file.Path, GUILayout.MaxWidth(position.width - 220), GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("diff", GUILayout.Width(60)))
                    {
                        if (_currentLogFile != file.Path)
                        {
                            _repaintRequired = true;
                            _currentLogFile = file.Path;
                        }

                        Git.InvokeDiffTool(file.Path, _currentLog.Sha1);
                    }
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);

                    if (Event.current.type == EventType.MouseDown && area.Contains(Event.current.mousePosition))
                    {
                        if (_currentLogFile != file.Path)
                        {
                            _repaintRequired = true;
                            _currentLogFile = file.Path;
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
        }
    }
}
