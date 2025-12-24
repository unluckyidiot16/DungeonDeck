// Assets/Editor/AddressablesDoctorProWindow.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Collections;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Debug = UnityEngine.Debug;

public class AddressablesDoctorProWindow : EditorWindow
{
    [MenuItem("Tools/Addressables/Doctor Pro")]
    public static void Open()
    {
        var w = GetWindow<AddressablesDoctorProWindow>("Addressables Doctor Pro");
        w.minSize = new Vector2(900, 650);
        w.RefreshSettings();
        w.RebuildProfileCache();
    }

    // ------------------------
    // Settings
    // ------------------------
    private AddressableAssetSettings settings;
    private Vector2 scroll;

    // Profile vars
    [SerializeField] private string remoteBaseUrlVarName = "RemoteBaseUrl";
    [SerializeField] private string remoteBuildPathVarName = "Remote.BuildPath";
    [SerializeField] private string remoteLoadPathVarName = "Remote.LoadPath";

    // Addressables build output templates (expected)
    [SerializeField] private string expectedRemoteBuildPathValue = "ServerData/[BuildTarget]";
    [SerializeField] private string expectedRemoteLoadPathTemplate = "[RemoteBaseUrl]/ServerData/[BuildTarget]";

    // Deploy - GH Pages
    [SerializeField] private string ghPagesBranch = "gh-pages";
    [SerializeField] private string ghWorktreeFolder = ".addr_ghpages_worktree";
    [SerializeField] private bool ghDeployOnlyActiveTarget = true;
    [SerializeField] private bool ghCleanWorktreeBeforeDeploy = true;
    [SerializeField] private bool ghAllowEmptyCommit = false;
    [SerializeField] private bool ghAutoPush = true;
    [SerializeField] private string ghCommitMessageTemplate = "Deploy Addressables {BuildTarget} {Timestamp}";
    
    // ✅ gh-pages hosting-only (ServerData만 유지)
    [SerializeField] private bool ghHostingOnlyServerData = true;
    [SerializeField] private bool ghCreateNoJekyll = true;
    

    // Deploy - R2 Presigned
    public enum R2UploadMode { ZipSinglePutUrl, FileMapPutUrls }
    [SerializeField] private R2UploadMode r2Mode = R2UploadMode.FileMapPutUrls;

    // Zip mode
    [SerializeField] private string r2ZipPutUrl = "";
    [SerializeField] private string r2ZipName = "ServerData.zip";

    // FileMap mode
    [SerializeField] private TextAsset signedUrlsJson; // JSON that contains file path + presigned url
    [SerializeField] private string signedUrlsJsonPath = ""; // optional: local path

    // Common
    [SerializeField] private bool showAdvanced = false;

    // Cache
    private List<ProfileRow> profileRows = new();

    private class ProfileRow
    {
        public string profileId;
        public string profileName;
        public string remoteBaseUrl;
        public string remoteBuildPath;
        public string remoteLoadPath;
    }

    [Serializable]
    public class SignedUrlList
    {
        public SignedUrlEntry[] files;
    }

    [Serializable]
    public class SignedUrlEntry
    {
        public string path; // relative path like "ServerData/Android/catalog.json"
        public string url;  // presigned PUT url
    }

    // ------------------------
    // Unity GUI
    // ------------------------
    private void OnEnable()
    {
        RefreshSettings();
        RebuildProfileCache();
    }

    private void RefreshSettings()
    {
        settings = AddressableAssetSettingsDefaultObject.Settings;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        DrawHeader();

        if (settings == null)
        {
            EditorGUILayout.HelpBox("Addressables Settings를 찾지 못했습니다.\nWindow > Asset Management > Addressables > Groups에서 Initialize Addressables를 먼저 해주세요.", MessageType.Error);
            if (GUILayout.Button("Refresh")) { RefreshSettings(); RebuildProfileCache(); }
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawProfileEditor();
        EditorGUILayout.Space(10);
        DrawBuildSection();
        EditorGUILayout.Space(10);
        DrawGhPagesSection();
        EditorGUILayout.Space(10);
        DrawR2Section();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(settings), EditorStyles.helpBox);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", GUILayout.Width(90)))
                {
                    RefreshSettings();
                    RebuildProfileCache();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                showAdvanced = EditorGUILayout.ToggleLeft("Show Advanced", showAdvanced, GUILayout.Width(130));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Addressables Groups", GUILayout.Width(190)))
                    EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            }
        }
    }

    private void DrawProfileEditor()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("1) Profiles: RemoteBaseUrl quick edit", EditorStyles.boldLabel);

            remoteBaseUrlVarName = EditorGUILayout.TextField("RemoteBaseUrl Var", remoteBaseUrlVarName);

            if (showAdvanced)
            {
                remoteBuildPathVarName = EditorGUILayout.TextField("Remote.BuildPath Var", remoteBuildPathVarName);
                remoteLoadPathVarName = EditorGUILayout.TextField("Remote.LoadPath Var", remoteLoadPathVarName);
                expectedRemoteBuildPathValue = EditorGUILayout.TextField("Expected Remote.BuildPath", expectedRemoteBuildPathValue);
                expectedRemoteLoadPathTemplate = EditorGUILayout.TextField("Expected Remote.LoadPath", expectedRemoteLoadPathTemplate);
            }

            EditorGUILayout.Space(6);

            var ps = settings.profileSettings;
            string activeName = ps.GetProfileName(settings.activeProfileId);
            EditorGUILayout.LabelField($"Active Profile: {activeName}", EditorStyles.miniBoldLabel);

            if (profileRows.Count == 0)
            {
                EditorGUILayout.HelpBox("프로필을 읽지 못했습니다. Refresh를 눌러주세요.", MessageType.Warning);
                return;
            }

            // Table header
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Profile", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                GUILayout.Label("RemoteBaseUrl", EditorStyles.miniBoldLabel);
                GUILayout.Label("Actions", EditorStyles.miniBoldLabel, GUILayout.Width(220));
            }

            EditorGUILayout.Space(2);

            foreach (var row in profileRows)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isActive = row.profileId == settings.activeProfileId;
                    GUILayout.Label(isActive ? $"▶ {row.profileName}" : row.profileName, GUILayout.Width(150));

                    row.remoteBaseUrl = EditorGUILayout.TextField(row.remoteBaseUrl);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(220)))
                    {
                        if (GUILayout.Button("Set Active", GUILayout.Width(90)))
                        {
                            settings.activeProfileId = row.profileId;
                            EditorUtility.SetDirty(settings);
                            AssetDatabase.SaveAssets();
                            RebuildProfileCache();
                        }

                        if (GUILayout.Button("Save", GUILayout.Width(60)))
                        {
                            SaveRemoteBaseUrl(row.profileId, row.remoteBaseUrl);
                            RebuildProfileCache();
                        }

                        if (GUILayout.Button("Copy LoadPath", GUILayout.Width(100)))
                        {
                            var loadPath = settings.profileSettings.GetValueByName(row.profileId, remoteLoadPathVarName);
                            EditorGUIUtility.systemCopyBuffer = loadPath;
                            ShowToast($"Copied: {loadPath}");
                        }
                    }
                }

                if (showAdvanced)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.LabelField($"Remote.BuildPath = {row.remoteBuildPath}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"Remote.LoadPath  = {row.remoteLoadPath}", EditorStyles.miniLabel);

                        // quick warnings
                        if (IsUndefinedOrEmpty(row.remoteLoadPath) || row.remoteLoadPath.Contains("<undefined>"))
                            EditorGUILayout.HelpBox("Remote.LoadPath가 비어 있거나 <undefined> 입니다. 원격 다운로드가 실패합니다.", MessageType.Error);

                        if (!string.IsNullOrEmpty(expectedRemoteBuildPathValue) && row.remoteBuildPath != expectedRemoteBuildPathValue)
                            EditorGUILayout.HelpBox($"Remote.BuildPath가 기대값({expectedRemoteBuildPathValue})과 다릅니다.", MessageType.Warning);
                    }
                }

                EditorGUILayout.Space(4);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save ALL RemoteBaseUrl", GUILayout.Height(26)))
                {
                    foreach (var r in profileRows)
                        SaveRemoteBaseUrl(r.profileId, r.remoteBaseUrl);

                    RebuildProfileCache();
                }

                if (GUILayout.Button("Ensure Variables (RemoteBaseUrl/Paths)", GUILayout.Height(26)))
                {
                    EnsureProfileVariablesAndTemplates();
                    RebuildProfileCache();
                }
            }
        }
    }

    private void DrawBuildSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("2) Build Addressables", EditorStyles.boldLabel);

            var bt = EditorUserBuildSettings.activeBuildTarget;
            EditorGUILayout.LabelField($"Active BuildTarget: {bt}", EditorStyles.miniBoldLabel);

            var serverDataPath = GetServerDataRoot();
            var targetFolder = GetServerDataTargetFolder();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("ServerData Root", GUILayout.Width(120));
                EditorGUILayout.SelectableLabel(serverDataPath, GUILayout.Height(18));
                if (GUILayout.Button("Reveal", GUILayout.Width(80))) EditorUtility.RevealInFinder(serverDataPath);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Target Folder", GUILayout.Width(120));
                EditorGUILayout.SelectableLabel(targetFolder, GUILayout.Height(18));
                if (GUILayout.Button("Reveal", GUILayout.Width(80))) EditorUtility.RevealInFinder(targetFolder);
            }

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Build Addressables (Default Build Script)", GUILayout.Height(30)))
            {
                BuildAddressables();
                RebuildProfileCache();
            }
        }
    }

    private void DrawGhPagesSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("3) Deploy to GitHub Pages (gh-pages branch)", EditorStyles.boldLabel);

            ghPagesBranch = EditorGUILayout.TextField("Branch", ghPagesBranch);
            ghWorktreeFolder = EditorGUILayout.TextField("Worktree Folder", ghWorktreeFolder);

            ghDeployOnlyActiveTarget = EditorGUILayout.ToggleLeft("Deploy ONLY active BuildTarget folder (recommended)", ghDeployOnlyActiveTarget);
            ghCleanWorktreeBeforeDeploy = EditorGUILayout.ToggleLeft("Clean worktree folder before deploy", ghCleanWorktreeBeforeDeploy);
            ghAllowEmptyCommit = EditorGUILayout.ToggleLeft("Allow empty commit", ghAllowEmptyCommit);
            ghAutoPush = EditorGUILayout.ToggleLeft("Auto push after commit", ghAutoPush);
            
            // ✅ gh-pages는 배포물 전용
            ghHostingOnlyServerData = EditorGUILayout.ToggleLeft("Hosting-only: keep ONLY ServerData on gh-pages (recommended)", ghHostingOnlyServerData);
            if (ghHostingOnlyServerData) ghCreateNoJekyll = EditorGUILayout.ToggleLeft("Create .nojekyll (recommended)", ghCreateNoJekyll);
            

            ghCommitMessageTemplate = EditorGUILayout.TextField("Commit Message", ghCommitMessageTemplate);

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Deploy -> gh-pages (worktree, commit, optional push)", GUILayout.Height(32)))
            {
                try
                {
                    DeployToGhPages();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    EditorUtility.DisplayDialog("Deploy failed", ex.Message, "OK");
                }
            }

            EditorGUILayout.HelpBox(
                "GitHub Pages 설정에서 Source를 gh-pages branch (root)로 지정하면\n" +
                "RemoteBaseUrl 예: https://<user>.github.io/<repo>\n" +
                "Remote.LoadPath = {RemoteBaseUrl}/ServerData/[BuildTarget] 로 그대로 작동합니다.",
                MessageType.Info);
        }
    }

    private void DrawR2Section()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("4) Deploy to R2 (Presigned)", EditorStyles.boldLabel);

            r2Mode = (R2UploadMode)EditorGUILayout.EnumPopup("Mode", r2Mode);

            EditorGUILayout.Space(6);

            if (r2Mode == R2UploadMode.ZipSinglePutUrl)
            {
                EditorGUILayout.HelpBox(
                    "ZIP 업로드는 presigned PUT URL 1개로 끝나서 자동화가 쉽지만,\n" +
                    "R2 정적 호스팅으로 바로 쓰려면 보통 '파일별 업로드'가 필요합니다.\n" +
                    "(ZIP을 서버에서 풀어주는 파이프라인이 있다면 ZIP도 OK)",
                    MessageType.Warning);

                r2ZipName = EditorGUILayout.TextField("Zip File Name", r2ZipName);
                r2ZipPutUrl = EditorGUILayout.TextField("Presigned PUT URL", r2ZipPutUrl);

                if (GUILayout.Button("Build ZIP from ServerData/[BuildTarget]", GUILayout.Height(26)))
                {
                    var zipPath = BuildZipFromServerData();
                    EditorUtility.RevealInFinder(zipPath);
                    ShowToast("ZIP created");
                }

                if (GUILayout.Button("Upload ZIP to Presigned URL", GUILayout.Height(30)))
                {
                    _ = UploadZipAsync();
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "파일별 업로드는 '파일 경로 -> presigned PUT url' 목록이 필요합니다.\n" +
                    "아래 JSON 형식(예시):\n" +
                    "{ \"files\": [ {\"path\":\"ServerData/Android/catalog.json\", \"url\":\"https://...\"}, ... ] }",
                    MessageType.Info);

                signedUrlsJson = (TextAsset)EditorGUILayout.ObjectField("Signed URLs JSON (TextAsset)", signedUrlsJson, typeof(TextAsset), false);
                signedUrlsJsonPath = EditorGUILayout.TextField("Or JSON file path", signedUrlsJsonPath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate Manifest JSON (paths only)", GUILayout.Height(26)))
                    {
                        var path = GenerateManifestJson();
                        EditorUtility.RevealInFinder(path);
                        ShowToast("Manifest generated");
                    }

                    if (GUILayout.Button("Upload from Signed URLs JSON", GUILayout.Height(26)))
                    {
                        _ = UploadFromSignedUrlsAsync();
                    }
                }
            }
        }
    }

    // ------------------------
    // Profile ops
    // ------------------------
    private void RebuildProfileCache()
    {
        profileRows.Clear();

        if (settings == null || settings.profileSettings == null) return;
        var ps = settings.profileSettings;

        var names = ps.GetAllProfileNames();
        foreach (var name in names)
        {
            var id = ps.GetProfileId(name);
            if (string.IsNullOrEmpty(id)) continue;

            profileRows.Add(new ProfileRow
            {
                profileId = id,
                profileName = name,
                remoteBaseUrl = ps.GetValueByName(id, remoteBaseUrlVarName),
                remoteBuildPath = ps.GetValueByName(id, remoteBuildPathVarName),
                remoteLoadPath = ps.GetValueByName(id, remoteLoadPathVarName),
            });
        }

        Repaint();
    }

    private void SaveRemoteBaseUrl(string profileId, string newValue)
    {
        var ps = settings.profileSettings;
        Undo.RecordObject(settings, "Set RemoteBaseUrl");
        ps.SetValue(profileId, remoteBaseUrlVarName, newValue ?? "");
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private void EnsureProfileVariablesAndTemplates()
    {
        var ps = settings.profileSettings;

        // Ensure RemoteBaseUrl variable exists
        if (!ps.GetVariableNames().Contains(remoteBaseUrlVarName))
        {
            Undo.RecordObject(settings, "Create RemoteBaseUrl variable");
            ps.CreateValue(remoteBaseUrlVarName, "http://127.0.0.1:8000");
        }

        // Ensure templates on all profiles if undefined
        foreach (var name in ps.GetAllProfileNames())
        {
            var id = ps.GetProfileId(name);
            if (string.IsNullOrEmpty(id)) continue;

            var rb = ps.GetValueByName(id, remoteBuildPathVarName);
            var rl = ps.GetValueByName(id, remoteLoadPathVarName);

            if (IsUndefinedOrEmpty(rb)) ps.SetValue(id, remoteBuildPathVarName, expectedRemoteBuildPathValue);
            if (IsUndefinedOrEmpty(rl)) ps.SetValue(id, remoteLoadPathVarName, expectedRemoteLoadPathTemplate);
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static bool IsUndefinedOrEmpty(string v)
    {
        if (string.IsNullOrEmpty(v)) return true;
        return v.Trim() == AddressableAssetProfileSettings.undefinedEntryValue;
    }

    // ------------------------
    // Addressables Build
    // ------------------------
    private void BuildAddressables()
    {
        EditorUtility.DisplayProgressBar("Addressables", "Building Addressables content...", 0.2f);
        try
        {
            // Addressables standard build entrypoint
            AddressableAssetSettings.BuildPlayerContent();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    private string GetProjectRoot()
    {
        // Application.dataPath = <repo>/Assets
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private string GetServerDataRoot()
    {
        return Path.GetFullPath(Path.Combine(GetProjectRoot(), "ServerData"));
    }

    private string GetBuildTargetName()
    {
        // Matches Addressables [BuildTarget] token in most setups
        return EditorUserBuildSettings.activeBuildTarget.ToString();
    }

    private string GetServerDataTargetFolder()
    {
        return Path.GetFullPath(Path.Combine(GetServerDataRoot(), GetBuildTargetName()));
    }

    // ------------------------
    // GH Pages Deploy
    // ------------------------
    
    private static string SanitizeBranchName(string raw)
    {
        // 사용자가 `git branch` 출력(*, +)을 그대로 복붙하는 경우가 많아서 제거
        var s = (raw ?? "").Trim();
        s = s.TrimStart('*', '+').Trim();
        
        if (string.IsNullOrEmpty(s))
            throw new Exception("gh-pages Branch 값이 비어 있습니다. 예: gh-pages");
        
        // 공백이 있으면 git 인자 파싱이 깨져 usage가 뜨는 케이스가 많음
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsWhiteSpace(s[i]))
                throw new Exception($"gh-pages Branch에 공백이 포함되어 있습니다: '{s}'\nBranch는 공백 없이 'gh-pages' 처럼 입력하세요.");
        }
        
        if (s.Contains("\"") || s.Contains("'"))
            throw new Exception($"gh-pages Branch에 따옴표가 포함되어 있습니다: '{s}'\n따옴표 없이 입력하세요.");
        
        return s;
    }

    private static string SanitizeWorktreeFolder(string raw)
    {
        var s = (raw ?? "").Trim().Trim('\"').Trim();
        if (string.IsNullOrEmpty(s))
            throw new Exception("Worktree Folder 값이 비어 있습니다. 예: .addr_ghpages_worktree");
        return s;
    }
    
    private void DeployToGhPages()
    {
        var repoRoot = GetGitRepoRoot();
        if (string.IsNullOrEmpty(repoRoot))
            throw new Exception("Git repo root를 찾지 못했습니다. (git init 되어 있는지 확인)");
        
        // ✅ 입력값 정리/검증 (usage 에러 예방)
        ghPagesBranch = SanitizeBranchName(ghPagesBranch);
        ghWorktreeFolder = SanitizeWorktreeFolder(ghWorktreeFolder);
        
        // ✅ stale worktree(유령 worktree) 방지
        TryRunGit(repoRoot, "worktree prune --expire now");

        var serverDataRoot = GetServerDataRoot();
        if (!Directory.Exists(serverDataRoot))
            throw new Exception($"ServerData 폴더가 없습니다: {serverDataRoot}\n먼저 Addressables Build를 해주세요.");

        var worktreePath = Path.Combine(repoRoot, ghWorktreeFolder);

        // Optional: remove old worktree folder
        if (ghCleanWorktreeBeforeDeploy && Directory.Exists(worktreePath))
        {
            TryRunGit(repoRoot, $"worktree remove --force \"{worktreePath}\"");
            TryDeleteDirectory(worktreePath);
        }

        // Ensure branch worktree
        EnsureGhWorktree(repoRoot, worktreePath, ghPagesBranch);

        // ✅ gh-pages는 배포물(ServerData)만 남기도록 정리
        if (ghHostingOnlyServerData)
        { 
            CleanGhPagesToHostingOnly(worktreePath, createNoJekyll: ghCreateNoJekyll);
        }
        
        // Copy content
        EditorUtility.DisplayProgressBar("Deploy gh-pages", "Copying ServerData...", 0.25f);

        string destServerDataRoot = Path.Combine(worktreePath, "ServerData");
        Directory.CreateDirectory(destServerDataRoot);

        if (ghDeployOnlyActiveTarget)
        {
            var src = GetServerDataTargetFolder();
            if (!Directory.Exists(src))
                throw new Exception($"Target ServerData 폴더가 없습니다: {src}\nBuildTarget에 맞춰 Addressables를 다시 빌드하세요.");

            var dst = Path.Combine(destServerDataRoot, GetBuildTargetName());
            // ✅ stale 파일 방지: 기존 타겟 폴더 삭제 후 복사
            TryDeleteDirectory(dst);
            CopyDirectory(src, dst, overwrite: true);
        }
        else
        {
            // copy whole ServerData
            // ✅ stale 파일 방지: 전체 ServerData를 갈아끼움
            TryDeleteDirectory(destServerDataRoot);
            Directory.CreateDirectory(destServerDataRoot);
            CopyDirectory(serverDataRoot, destServerDataRoot, overwrite: true);
        }

        // Commit & push
        EditorUtility.DisplayProgressBar("Deploy gh-pages", "Git add/commit...", 0.7f);

        TryRunGit(worktreePath, "add -A");

        string msg = ghCommitMessageTemplate
            .Replace("{BuildTarget}", GetBuildTargetName())
            .Replace("{Timestamp}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        // commit (optionally allow empty)
        var commitArgs = ghAllowEmptyCommit ? $"commit --allow-empty -m \"{EscapeQuotes(msg)}\"" : $"commit -m \"{EscapeQuotes(msg)}\"";
        var commitResult = RunGit(worktreePath, commitArgs, allowFail: true);

        // If nothing to commit, git returns non-zero; treat as OK.
        if (!commitResult.ok)
        {
            if (!commitResult.output.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
                Debug.LogWarning($"[gh-pages] commit non-zero:\n{commitResult.output}");
        }

        if (ghAutoPush)
        {
            EditorUtility.DisplayProgressBar("Deploy gh-pages", "Pushing...", 0.9f);
            var push = RunGit(worktreePath, $"push origin {ghPagesBranch}", allowFail: true);
            if (!push.ok)
                Debug.LogWarning($"[gh-pages] push failed:\n{push.output}\n원격(origin) 설정/권한을 확인하세요.");
        }

        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("Deploy 완료", "gh-pages 배포가 완료되었습니다.\nGitHub Pages 설정에서 gh-pages branch를 source로 지정했는지 확인하세요.", "OK");
    }

    private void EnsureGhWorktree(string repoRoot, string worktreePath, string branch)
    {
        // ✅ IMPORTANT:
        // 기존 코드: `worktree add -B gh-pages <path>` 는 "현재 브랜치 HEAD"를 기준으로 gh-pages를 리셋함.
        // 그래서 gh-pages가 main과 똑같이 되어 버림.
        
        branch = SanitizeBranchName(branch);
        
        // 원격 브랜치 유무 판단 전에 최신 refs 갱신(선택이지만 추천)
        TryRunGit(repoRoot, "fetch origin --prune");
        
        // ✅ 이미 worktree가 존재하고 브랜치도 맞으면 재사용
        try
        {
            var dotGit = Path.Combine(worktreePath, ".git");
            if (Directory.Exists(worktreePath) && File.Exists(dotGit))
            {
                var cur = RunGit(worktreePath, "rev-parse --abbrev-ref HEAD", allowFail: true);
                if (cur.ok && cur.output.Trim() == branch)
                    return;
            }
        }
        catch { /* ignore */ }
        
        
        // 개선: origin/gh-pages가 있으면 그걸 기준으로 worktree 생성/리셋.
                    
        string remoteRef = $"origin/{branch}";
        bool hasRemote = HasRemoteBranch(repoRoot, remoteRef);
        
        string args = hasRemote
            ? $"worktree add -B {branch} \"{worktreePath}\" {remoteRef}"
            : $"worktree add -B {branch} \"{worktreePath}\"";
        
        var primary = RunGit(repoRoot, args, allowFail: true);
        if (primary.ok) return;
        
        // 1차가 실패했다면: (특히 origin/gh-pages 관련) 로컬 브랜치 포인터를 명시적으로 맞춰두고
        // path-first 형태의 worktree add로 재시도
        // ⚠️ gh-pages가 다른 worktree에서 체크아웃 중이면 branch -f는 항상 실패한다.
        // 브랜치 포인터 이동은 worktree 내부에서 reset으로 처리하는 편이 안전.
        // (여기서는 branch -f 시도를 하지 않는다.)
        
        string args2 = $"worktree add -f \"{worktreePath}\" {branch}";
        var secondary = RunGit(repoRoot, args2, allowFail: true);
        if (!secondary.ok)
        {
            string dbg =
                $"git worktree add failed.\n" +
                $"Branch='{branch}'\nWorktreePath='{worktreePath}'\n\n" +
                $"1) git {args}\n{primary.output}\n\n" +
                $"2) git {args2}\n{secondary.output}\n";
            throw new Exception(dbg);
        }
        
        // worktree가 만들어졌으면, 원격 기준으로 내용만 맞춘다 (브랜치 강제 이동 아님)
        if (hasRemote)
        {
            TryRunGit(worktreePath, "fetch origin --prune");
            TryRunGit(worktreePath, $"reset --hard {remoteRef}");
        }
        
    }
    
    private bool HasRemoteBranch(string repoRoot, string remoteRef)
    {
        // remoteRef example: origin/gh-pages
        // `show-ref --verify refs/remotes/origin/gh-pages`
        string rr = remoteRef.Replace("\\", "/");
        if (!rr.StartsWith("origin/")) return false;
        string name = rr.Substring("origin/".Length);
        var r = RunGit(repoRoot, $"show-ref --verify --quiet refs/remotes/origin/{name}", allowFail: true);
        return r.ok;
    }

    private string GetGitRepoRoot()
    {
        var projectRoot = GetProjectRoot();
        var res = RunGit(projectRoot, "rev-parse --show-toplevel", allowFail: true);
        if (!res.ok) return null;
        return res.output.Trim();
    }

    // ------------------------
    // R2 Presigned Upload
    // ------------------------
    private string BuildZipFromServerData()
    {
        var src = GetServerDataTargetFolder();
        if (!Directory.Exists(src))
            throw new Exception($"ServerData 타겟 폴더가 없습니다: {src}\n먼저 Addressables Build를 해주세요.");

        var outPath = Path.Combine(GetProjectRoot(), r2ZipName);
        if (File.Exists(outPath)) File.Delete(outPath);

        EditorUtility.DisplayProgressBar("ZIP", "Creating zip...", 0.2f);

        ZipFile.CreateFromDirectory(src, outPath, CompressionLevel.Optimal, includeBaseDirectory: false);

        EditorUtility.ClearProgressBar();
        return outPath;
    }

    private async Task UploadZipAsync()
    {
        if (string.IsNullOrWhiteSpace(r2ZipPutUrl))
        {
            EditorUtility.DisplayDialog("R2 ZIP 업로드", "Presigned PUT URL을 입력하세요.", "OK");
            return;
        }

        string zipPath = Path.Combine(GetProjectRoot(), r2ZipName);
        if (!File.Exists(zipPath))
        {
            try { zipPath = BuildZipFromServerData(); }
            catch (Exception ex) { EditorUtility.DisplayDialog("ZIP 생성 실패", ex.Message, "OK"); return; }
        }

        try
        {
            await HttpPutFileAsync(r2ZipPutUrl, zipPath, "application/zip", "Uploading ZIP...");
            EditorUtility.DisplayDialog("업로드 완료", "ZIP 업로드가 완료되었습니다.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            EditorUtility.DisplayDialog("업로드 실패", ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private string GenerateManifestJson()
    {
        // Creates a JSON listing paths only (no URLs)
        // Use this to request presigned urls from your backend
        var root = ghDeployOnlyActiveTarget ? GetServerDataTargetFolder() : GetServerDataRoot();
        if (!Directory.Exists(root))
            throw new Exception($"ServerData 폴더가 없습니다: {root}");

        var projectRoot = GetProjectRoot();
        var allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => NormalizePath(RelPathFrom(projectRoot, f)))
            .ToArray();

        // We'll output same structure as SignedUrlList but url empty
        var list = new SignedUrlList
        {
            files = allFiles.Select(p => new SignedUrlEntry { path = p, url = "" }).ToArray()
        };

        var json = JsonUtility.ToJson(list, prettyPrint: true);

        var outPath = Path.Combine(projectRoot, "addressables_manifest_paths.json");
        File.WriteAllText(outPath, json, Encoding.UTF8);
        AssetDatabase.Refresh();
        return outPath;
    }

    private async Task UploadFromSignedUrlsAsync()
    {
        var jsonText = GetSignedUrlsJsonText();
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            EditorUtility.DisplayDialog("R2 업로드", "Signed URLs JSON이 비어 있습니다.", "OK");
            return;
        }

        SignedUrlList list;
        try
        {
            list = JsonUtility.FromJson<SignedUrlList>(jsonText);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("JSON 파싱 실패", $"형식이 맞는지 확인하세요.\n{ex.Message}", "OK");
            return;
        }

        if (list?.files == null || list.files.Length == 0)
        {
            EditorUtility.DisplayDialog("R2 업로드", "files 항목이 비어 있습니다.", "OK");
            return;
        }

        var projectRoot = GetProjectRoot();

        // Validate local files exist
        var missing = list.files.Where(f => !File.Exists(Path.Combine(projectRoot, NormalizePath(f.path)))).Take(5).ToList();
        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "로컬 파일 없음",
                "Signed URL 목록에 포함된 로컬 파일이 없습니다.\n예) " + missing[0].path + "\n경로가 프로젝트 루트 기준인지 확인하세요.",
                "OK");
            return;
        }

        try
        {
            int total = list.files.Length;
            for (int i = 0; i < total; i++)
            {
                var entry = list.files[i];
                if (string.IsNullOrWhiteSpace(entry.url))
                    throw new Exception($"URL이 비어 있습니다: {entry.path}");

                string localPath = Path.Combine(projectRoot, NormalizePath(entry.path));

                float p = (i / (float)total);
                EditorUtility.DisplayProgressBar("R2 Upload", $"{i + 1}/{total} {entry.path}", p);

                await HttpPutFileAsync(entry.url, localPath, GuessContentType(localPath), null);
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("업로드 완료", $"총 {list.files.Length}개 업로드 완료", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            EditorUtility.DisplayDialog("업로드 실패", ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private string GetSignedUrlsJsonText()
    {
        if (signedUrlsJson != null && !string.IsNullOrEmpty(signedUrlsJson.text))
            return signedUrlsJson.text;

        if (!string.IsNullOrWhiteSpace(signedUrlsJsonPath) && File.Exists(signedUrlsJsonPath))
            return File.ReadAllText(signedUrlsJsonPath, Encoding.UTF8);

        return null;
    }

    private static async Task HttpPutFileAsync(string putUrl, string localFilePath, string contentType, string progressTitleOrNull)
    {
        // Uses System.Net.Http. Available in Unity editor.
        using var http = new System.Net.Http.HttpClient();
        byte[] bytes = await File.ReadAllBytesAsync(localFilePath);

        using var content = new System.Net.Http.ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType ?? "application/octet-stream");

        // Optional progress UI (coarse)
        if (!string.IsNullOrEmpty(progressTitleOrNull))
            EditorUtility.DisplayProgressBar(progressTitleOrNull, Path.GetFileName(localFilePath), 0.6f);

        var resp = await http.PutAsync(putUrl, content);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Exception($"PUT failed ({(int)resp.StatusCode}) {resp.ReasonPhrase}\nFile: {localFilePath}\nBody: {body}");
        }
    }

    private static string GuessContentType(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".json" => "application/json",
            ".hash" => "text/plain",
            ".txt"  => "text/plain",
            ".bundle" => "application/octet-stream",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };
    }

    // ------------------------
    // Git helpers
    // ------------------------
    private static void TryRunGit(string workingDir, string args)
    {
        var r = RunGit(workingDir, args, allowFail: true);
        if (!r.ok) Debug.LogWarning($"[git] {args}\n{r.output}");
    }

    private static (bool ok, string output) RunGit(string workingDir, string args, bool allowFail)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var p = Process.Start(psi);
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            string output = (stdout + "\n" + stderr).Trim();
            bool ok = p.ExitCode == 0;
            if (!ok && !allowFail) throw new Exception(output);

            return (ok, output);
        }
        catch (Exception ex)
        {
            if (!allowFail) throw;
            return (false, ex.Message);
        }
    }

    // ------------------------
    // File helpers
    // ------------------------
    private static void CopyDirectory(string sourceDir, string destDir, bool overwrite)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException(sourceDir);

        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dst = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dst, overwrite);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(destDir, name), overwrite);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            Directory.Delete(path, recursive: true);
        }
        catch { /* ignore */ }
    }
    
    // ------------------------
    // gh-pages hosting-only cleanup
    // ------------------------
    private static void CleanGhPagesToHostingOnly(string worktreePath, bool createNoJekyll)
    {
        // worktreePath 루트에서 .git(파일) / ServerData / (.nojekyll 옵션)만 남김
        // ⚠️ .git 은 worktree에서 "파일"로 존재하는 경우가 많음. 삭제하면 worktree가 망가짐.
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { 
            ".git",
            "ServerData",
        };
        
        if (createNoJekyll) keep.Add(".nojekyll");
        
        // Create .nojekyll early so it is preserved
        if (createNoJekyll)
        {
            var nojekyll = Path.Combine(worktreePath, ".nojekyll");
            try
            {
                if (!File.Exists(nojekyll)) File.WriteAllText(nojekyll, "");
            }
            catch { /* ignore */ }
        }
        
        // delete everything else in root
        foreach (var entry in Directory.GetFileSystemEntries(worktreePath))
        {
            var name = Path.GetFileName(entry);
            if (string.IsNullOrEmpty(name)) continue;
            if (keep.Contains(name)) continue;
            
            try
            {
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                else if (File.Exists(entry)) File.Delete(entry);
            }
            catch { /* ignore */ }
        }
    }

    private static string EscapeQuotes(string s) => (s ?? "").Replace("\"", "\\\"");

    private static string RelPathFrom(string root, string fullPath)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        fullPath = Path.GetFullPath(fullPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return fullPath;
        return fullPath.Substring(root.Length);
    }

    private static string NormalizePath(string p)
    {
        return (p ?? "").Replace("\\", "/");
    }

    private static void ShowToast(string msg)
    {
        EditorWindow.focusedWindow?.ShowNotification(new GUIContent(msg));
    }
}
#endif
