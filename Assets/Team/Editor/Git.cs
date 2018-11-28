using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

/// <summary>
/// Created by dpl ericdplau@yahoo.com
/// at 2018/08/23 21:48:13
/// </summary>
namespace UniRx.Team.Editor
{
    public static class Git
    {
        const string kGitExecutablePath = "kGitExecutablePath";

        const bool isDebugOutputInfos = false;
        const bool isDebugOutputError = true;

        /// <summary>
        /// Git 执行文件路径
        /// </summary>
        public static string ExecutablePath { get; set; }

        /// <summary>
        /// Git 版本名称
        /// </summary>
        public static string Version { get; private set; }

        /// <summary>
        /// Git 是否准备就绪（是否已发现 Git 版本号）
        /// </summary>
        public static bool IsReady { get; private set; }

        /// <summary>
        /// Git 本地仓库的当前分支名称
        /// </summary>
        public static string Branch { get; private set; }

        /// <summary>
        /// Git 本地分支的状态信息（通过 git branch -vv 获取，包含 ahead, belind 信息）
        /// </summary>
        public static string BranchInfomation { get; private set; }

        /// <summary>
        /// Git 本地分支落后于远程仓库的版本数
        /// </summary>
        public static int BranchBehind { get; private set; }

        /// <summary>
        /// Git 本地分支提前于远程仓库的版本数
        /// </summary>
        public static int BranchAhead { get; private set; }

        /// <summary>
        /// Git 本地最后一次提交的 Head sha1 值
        /// </summary>
        public static string LastCommitHead { get; private set; }

        /// <summary>
        /// Git 本地分支对应的远程分支路径
        /// </summary>
        public static string UpstreamBranch { get; private set; }

        /// <summary>
        /// Git 远程仓库名称
        /// </summary>
        public static string Remote { get; private set; }

        /// <summary>
        /// Git 本地更改的文件（仅未提交的）
        /// </summary>
        public static readonly List<File> LocalChangedFiles = new List<File>();

        /// <summary>
        /// Git 本地可以提交到远程仓库的文件（仅已提交的）
        /// </summary>
        public static readonly List<File> LocalPushingFiles = new List<File>();

        /// <summary>
        /// Git 远程仓库相对于本地仓库的更新文件（仅已提交的）
        /// </summary>
        public static readonly List<File> RemoteUpdateFiles = new List<File>();

        /// <summary>
        /// Git 本地仓库与远程仓库的差异（包括未提交的）
        /// </summary>
        public static readonly List<File> L2RDifferentFiles = new List<File>();

        /// <summary>
        /// 启动 Unity3d IDE 时初始化
        /// </summary>
        static Git()
        {
            ExecutablePath = EditorPrefs.GetString(kGitExecutablePath, "git");
            CheckEnvironment();
        }

        /// <summary>
        /// 设置 Git 执行文件路径并存储为本地偏好设置
        /// </summary>
        /// <param name="path"></param>
        public static void SetExecutablePath(string path)
        {
            ExecutablePath = path;
            CheckEnvironment();

            if (!string.IsNullOrEmpty(Version))
                EditorPrefs.SetString(kGitExecutablePath, path);
        }

        /// <summary>
        /// 更新 Git 仓库状态
        /// </summary>
        public static void RefreshStatus(bool checkRemote = false)
        {
            UpdateBranchInfo();
            CheckLocalChanges();

            if (checkRemote)
                CheckRemoteDiffs();
        }

        public static void Add(File file = null)
        {
            var args = (file != null) ? string.Format("add {0}", file.QuotedPaths) : "add .";
            StartGitProcess(args, true);
        }

        public static bool AddSelected()
        {
            bool added = false;

            foreach (var item in LocalChangedFiles)
            {
                if (item.Toggled && item.HasStatus(EStatus.HasUnstagedChanges))
                {
                    added = true;
                    Add(item);
                }
            }

            return added;
        }

        public static void Reset(File file = null)
        {
            var args = (file != null) ? string.Format("reset -- {0}", file.QuotedPaths) : "reset .";
            StartGitProcess(args, true);

            if (file != null && file.HasStatus(EStatus.Renamed))
            {
                var deleted = File.Create(file.SourcePath, true);

                if (deleted != null)
                    StartGitProcess(string.Format("reset -- {0}", deleted.QuotedPaths), true);
            }
        }

        public static bool ResetSelectd()
        {
            bool reseted = false;

            foreach (var item in LocalChangedFiles)
            {
                if (item.Toggled && item.HasStatus(EStatus.HasStagedChanges))
                {
                    reseted = true;
                    Reset(item);
                }
            }

            return reseted;
        }

        public static void Checkout(File file, string version)
        {
            StartGitProcess(string.Format("checkout {0} -- {1}", version, file.QuotedPaths), true);
            Reset(file);

            AssetDatabase.Refresh();
        }

        public static void Checkout(string file, string version)
        {
            Checkout(File.Create(file, true), version);
        }

        public static void Discard(File file)
        {
            Checkout(file, "HEAD^");
        }

        public static void Commit(string comments)
        {
            var args = string.Format("commit --quiet -m \"{0}\"", comments.Replace("\"", "\\\""));
            StartGitProcess(args, true);
        }

        public static void Push()
        {
            StartGitProcess("push --quiet", true);
        }

        public static void Pull()
        {
            EditorUtility.DisplayProgressBar("Hold on", "Updating...", 1f);

            List<File> reverts = new List<File>();

            foreach (var file in RemoteUpdateFiles)
            {
                if (LocalChangedFiles.Find(x => x.Path == file.Path) != null)
                {
                    UnityEngine.Debug.LogError("Please commit local changes first.");
                    EditorUtility.ClearProgressBar();
                    return;
                }

                if (file.DiscardRemote)
                    reverts.Add(file);
            }

            var args = string.Format("merge -s recursive -X theirs {0} -m \"Overwrite Merge\"", UpstreamBranch);
            StartGitProcess(args, true);

            foreach (var file in reverts)
            {
                UnityEngine.Debug.Log(string.Format("Discard Remote: {0}", file.QuotedPaths));
                StartGitProcess(string.Format("checkout HEAD~1 {0}", file.QuotedPaths), true);
            }

            //if (reverts.Count > 0)
            //{
            //    Commit("Discard Remote Changes");
            //    Push();
            //}

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            if (reverts.Count > 0)
            {
                string message = string.Format("Git pulled, but there was {0} files discard from remote and not commit.", reverts.Count);
                EditorUtility.DisplayDialog("Warning", message, "OK");
            }
        }

        public static void InvokeDiffTool(string path, string version = "HEAD")
        {
            var exts = path.Substring(path.IndexOf("."));
            var temp = string.Format("{0}{1}{2}", System.IO.Path.GetTempPath(), Guid.NewGuid().ToString(), exts);

            var stream = System.IO.File.OpenWrite(temp);
            var proc = StartGitProcess(string.Format("show {0}:\"{1}\"", version, path));

            stream.SetLength(0);

            while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
            {
                var line = proc.StandardOutput.ReadLine();
                byte[] data = new System.Text.UTF8Encoding(true).GetBytes(line + "\n");
                stream.Write(data, 0, data.Length);
            }

            stream.Flush();
            stream.Close();

            EditorUtility.InvokeDiffTool(version, temp, path, path, "", "");
        }

        public static List<File> GetHeadFiles(string head)
        {
            List<File> diffs = new List<File>();
            var proc = StartGitProcess(string.Format("diff --name-only {0}~ {0}", string.IsNullOrEmpty(head) ? "HEAD" : head));

            while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
            {
                var line = proc.StandardOutput.ReadLine();
                var file = File.Create(line, true);

                if (file != null)
                    diffs.Add(file);
            }

            return diffs;
        }

        public static List<Log> GetLogs(string path, int number)
        {
            var proc = StartGitProcess(string.Format("log --pretty=format:\"%at|%s|%h|%an\" --abbrev-commit -n {0} -- \"{1}\"", number, path));

            List<Log> logs = new List<Log>();

            while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
            {
                var line = proc.StandardOutput.ReadLine();
                var logString = line.Split('|');

                if (logString.Length == 4)
                {
                    var localDateTime = GetLocalDateTime(Convert.ToInt32(logString[0]));

                    logs.Add(new Log
                    {
                        Date = localDateTime,
                        Message = logString[1],
                        Sha1 = logString[2],
                        User = logString[3],
                    });
                }
            }

            return logs;
        }

        static DateTime GetLocalDateTime(int timestamp)
        {
            DateTime converted = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return converted.AddSeconds(timestamp).ToLocalTime();
        }

        /// <summary>
        /// 获取 Git 环境信息，验证 Git 版本
        /// </summary>
        static void CheckEnvironment()
        {
            var proc = StartProcess(ExecutablePath, "--version");

            Version = string.Empty;
            IsReady = false;

            while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
            {
                var line = proc.StandardOutput.ReadLine();

                if (Regex.IsMatch(line, @"version \d+\.\d+"))
                {
                    Version = line;
                    IsReady = true;
                }
            }
        }

        /// <summary>
        /// 更新分支信息，获取版本差异
        /// </summary>
        static void UpdateBranchInfo()
        {
            BranchBehind = BranchAhead = 0;
            UpstreamBranch = Remote = "";

            var proc = StartGitProcess("branch -vv");

            while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
            {
                var line = proc.StandardOutput.ReadLine();

                if (line.StartsWith("*"))
                {
                    BranchInfomation = line;
                    ParseBranchInfo();
                }
            }
        }

        /// <summary>
        /// 分析版本差异
        /// </summary>
        static void ParseBranchInfo()
        {
            var match0 = Regex.Match(BranchInfomation, @"^\*\s*(\w+)\s+(\w+)\s*(.*)$");

            if (!match0.Success)
                return;

            Branch = match0.Groups[1].Value;
            LastCommitHead = match0.Groups[2].Value;

            var relations = match0.Groups[3].Value;
            var upstreams = Regex.Match(relations, @"^\[([^:]+)");

            if (upstreams.Success)
            {
                UpstreamBranch = upstreams.Groups[1].Value;
                Remote = UpstreamBranch.Substring(0, UpstreamBranch.IndexOf("/"));

                var behind = Regex.Match(relations, @"behind\s+(\d+)");

                if (behind.Success)
                    BranchBehind = Convert.ToInt32(behind.Groups[1].Value);

                var ahead = Regex.Match(relations, @"ahead\s+(\d+)");

                if (ahead.Success)
                    BranchAhead = Convert.ToInt32(ahead.Groups[1].Value);
            }
        }

        /// <summary>
        /// 获取本地文件更改信息
        /// </summary>
        static void CheckLocalChanges()
        {
            LocalChangedFiles.Clear();

            var proc = StartGitProcess("status --porcelain --untracked-files");

            while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
            {
                var file = File.Create(proc.StandardOutput.ReadLine(), false);

                if (file != null)
                    LocalChangedFiles.Add(file);
            }
        }

        /// <summary>
        /// 检查本地仓库文件变化以及与远程仓库的文件差异
        /// </summary>
        static void CheckRemoteDiffs()
        {
            if (string.IsNullOrEmpty(Remote) || string.IsNullOrEmpty(UpstreamBranch))
                return;

            EditorUtility.DisplayProgressBar("Hold on", "Refreshing Status", 1f);

            LocalPushingFiles.Clear();
            RemoteUpdateFiles.Clear();
            L2RDifferentFiles.Clear();

            StartGitProcess(string.Format("fetch {0}", Remote), true);
            AssetDatabase.Refresh();

            var proc = StartGitProcess(string.Format("diff --name-only {0} {1}", Branch, UpstreamBranch));

            while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
            {
                var line = proc.StandardOutput.ReadLine();
                var file = File.Create(line, true);

                if (file != null)
                    L2RDifferentFiles.Add(file);
            }

            if (BranchAhead > 0)
            {
                string headstr = string.Format("HEAD~{0}", BranchAhead);
                proc = StartGitProcess(string.Format("diff --name-only {0} {1}", headstr, Branch));

                while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
                {
                    var line = proc.StandardOutput.ReadLine();
                    var file = File.Create(line, true);

                    if (file != null)
                        LocalPushingFiles.Add(file);
                }
            }

            if (BranchBehind > 0)
            {
                string headstr = BranchAhead > 0 ? string.Format("HEAD~{0}", BranchAhead) : "HEAD";
                proc = StartGitProcess(string.Format("diff --name-only {0} {1}", headstr, UpstreamBranch));

                while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
                {
                    var line = proc.StandardOutput.ReadLine();
                    var file = File.Create(line, true);

                    if (file != null)
                        RemoteUpdateFiles.Add(file);
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        /*static void ExecuteCommandSync(object args)
        {
            try
            {
                ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c " + args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process proc = new Process
                {
                    StartInfo = procStartInfo
                };

                if (proc.Start())
                {
                    string result = proc.StandardOutput.ReadToEnd();
                    UnityEngine.Debug.Log(result);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        static void ExecuteCommandAsync(string args)
        {
            try
            {
                Thread objThread = new Thread(new ParameterizedThreadStart(ExecuteCommandSync))
                {
                    Priority = ThreadPriority.AboveNormal,
                    IsBackground = true
                };

                objThread.Start(args);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }*/

        /// <summary>
        /// 创建并启动一个处理线程
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="args"></param>
        /// <param name="waitForExit"></param>
        /// <returns></returns>
        static Process StartProcess(string filename, string args, bool waitForExit = false)
        {
            var proc = new Process();

            proc.StartInfo.FileName = filename;
            proc.StartInfo.Arguments = args;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.UseShellExecute = false;

            try
            {
                if (proc.Start())
                {
                    if (waitForExit)
                    {
                        //DebugLogProcessResult(proc, isDebugOutputInfos, isDebugOutputError);
                        proc.WaitForExit();
                    }

                    return proc;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            return null;
        }

        /// <summary>
        /// 创建并启动一个 Git 处理线程
        /// </summary>
        /// <param name="args"></param>
        /// <param name="waitForExit"></param>
        /// <returns></returns>
        static Process StartGitProcess(string args, bool waitForExit = false)
        {
            return IsReady ? StartProcess(ExecutablePath, args, waitForExit) : null;
        }

        /// <summary>
        /// 读取线程输出并打印错误信息
        /// </summary>
        /// <param name="proc"></param>
        /// <returns></returns>
        static void DebugLogProcessResult(Process proc, bool debugLogInfo = false, bool debugLogError = true)
        {
            if (proc != null && debugLogInfo)
                UnityEngine.Debug.Log(string.Format("{0} {1}", proc.StartInfo.FileName, proc.StartInfo.Arguments));

            while (!(proc == null || proc.HasExited || proc.StandardOutput.EndOfStream))
            {
                var line = proc.StandardOutput.ReadLine();

                if (debugLogInfo)
                    UnityEngine.Debug.Log(line);
            }

            while (!(proc == null || proc.HasExited || proc.StandardError.EndOfStream))
            {
                var line = proc.StandardOutput.ReadLine();

                if (debugLogError)
                    UnityEngine.Debug.LogError(line);
            }
        }
    }

    public class File
    {
        public string Path { get; private set; }
        public string SourcePath { get; private set; }
        public string Name { get; private set; }
        public string StatusTag { get; private set; }
        public uint StatusCode { get; private set; }
        public bool IsUnityFile { get; private set; }
        public bool IsMetaFile { get; private set; }
        public bool IsFolder { get; private set; }
        public bool DiscardRemote { get; set; }
        public bool Toggled { get; set; }

        public string QuotedPaths
        {
            get
            {
                if (IsMetaFile)
                    return string.Format("\"{0}\" \"{1}\"", Path, Path.Replace(".meta", ""));
                else if (IsUnityFile)
                    return string.Format("\"{0}\" \"{0}.meta\"", Path);
                else
                    return Path;
            }
        }

        public bool IsModified
        {
            get { return StatusTag.Contains('M'); }
        }

        public bool IsDeleted
        {
            get { return StatusTag.Contains('D'); }
        }

        public bool HasStatus(EStatus status)
        {
            return (StatusCode & (uint)status) == (uint)status;
        }

        public static File Create(string line, bool nameOnly = false)
        {
            if (line.Length < 4 || line.StartsWith("#"))
                return null;

            string name, statusCode, path;
            string sourcePath = "";

            if (nameOnly)
            {
                statusCode = "  ";
                path = line;
            }
            else
            {
                statusCode = line.Substring(0, 2);
                path = line.Substring(3);
            }

            if (path.IndexOf("->") > 0)
            {
                var leftright = path.Split(new string[] { "->" }, StringSplitOptions.None);
                sourcePath = leftright.First().Trim();
                path = leftright.Last().Trim();
            }

            path = path.Replace("\"", "").Trim();

            string[] substrings = path.Split('/');
            bool isFolder = path.EndsWith("/");

            if (isFolder)
            {
                path = path.Remove(path.Length - 1);
                name = substrings[substrings.Length - 2];
            }
            else
            {
                name = substrings[substrings.Length - 1];
            }

            bool isUnityFile = path.StartsWith("Assets");
            bool isMetaFile = path.EndsWith(".meta");

            File file = new File
            {
                StatusTag = statusCode,
                IsUnityFile = isUnityFile,
                IsMetaFile = isMetaFile,
                IsFolder = isFolder,
                SourcePath = sourcePath,
                Path = path,
                Name = name
            };

            if (!nameOnly)
                file.ParseStatus();

            return file;
        }

        void ParseStatus()
        {
            if (StatusTag.Contains("U") || StatusTag == "AA" || StatusTag == "DD")
            {
                StatusCode |= (uint)EStatus.Unresolved;
            }
            else if (StatusTag[0] == '!')
            {
                StatusCode |= (uint)EStatus.Ignored;
            }
            else if (StatusTag[0] == '?')
            {
                StatusCode |= (uint)EStatus.Untracked;
            }
            else if (StatusTag[0] == 'R')
            {
                StatusCode |= (uint)EStatus.HasStagedChanges;
                StatusCode |= (uint)EStatus.Renamed;
            }
            else if (StatusTag[0] == 'D')
            {
                StatusCode |= (uint)EStatus.HasStagedChanges;
                StatusCode |= (uint)EStatus.Deleted;
            }
            else if (StatusTag[0] != ' ')
            {
                StatusCode |= (uint)EStatus.HasStagedChanges;
            }

            if (StatusTag[1] != ' ' && StatusTag[1] != '!')
                StatusCode |= (uint)EStatus.HasUnstagedChanges;
        }
    }

    [Flags]
    public enum EStatus : uint
    {
        Untracked = 1 << 0,
        HasStagedChanges = 1 << 1,
        HasUnstagedChanges = 1 << 2,
        Deleted = 1 << 3,
        Renamed = 1 << 4,
        Unresolved = 1 << 5,
        Ignored = 1 << 6
    }

    public class Log
    {
        public DateTime Date;
        public string Message;
        public string Sha1;
        public string User;
    }
}
