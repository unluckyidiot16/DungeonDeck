// Assets/Editor/AddressablesDoctorWindow.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

public class AddressablesDoctorWindow : EditorWindow
{
    [MenuItem("Tools/Addressables/Doctor")]
    public static void Open()
    {
        var w = GetWindow<AddressablesDoctorWindow>("Addressables Doctor");
        w.minSize = new Vector2(760, 520);
        w.RefreshSettings();
        w.Scan();
    }

    private AddressableAssetSettings _settings;
    private Vector2 _scroll;

    // ---- User-tunable rules ----
    [Header("Rules")]
    [SerializeField] private string requiredLabelsCsv = "artpack";
    [SerializeField] private string localGroupNamesCsv = "Base_Local,Default Local Group";
    [SerializeField] private string remoteGroupNamesCsv = "ArtPack_Remote";

    [Header("Profiles")]
    [SerializeField] private bool applyProfileFixToAllProfiles = true;

    // If you want remote paths driven by a single base URL per profile:
    [SerializeField] private string remoteBaseUrlVarName = "RemoteBaseUrl";
    [SerializeField] private string remoteLoadPathVarName = "Remote.LoadPath";
    [SerializeField] private string remoteBuildPathVarName = "Remote.BuildPath";
    [SerializeField] private string localLoadPathVarName  = "Local.LoadPath";
    [SerializeField] private string localBuildPathVarName = "Local.BuildPath";

    // Default templates (tokens in [] are evaluated by Addressables Profiles)
    [SerializeField] private string remoteBuildPathValue = "ServerData/[BuildTarget]";
    [SerializeField] private string remoteLoadPathTemplate = "[RemoteBaseUrl]/ServerData/[BuildTarget]";

    [Header("Catalog")]
    [SerializeField] private bool enforceBuildRemoteCatalog = true;
    [SerializeField] private int catalogRequestTimeoutSeconds = 20; // 0 can mean "no timeout" (hang risk)

    private readonly List<CheckItem> _checks = new();

    private enum CheckStatus { Ok, Warn, Error }

    private class CheckItem
    {
        public string title;
        public CheckStatus status;
        public string details;
        public Action fix;
    }

    private void OnEnable()
    {
        RefreshSettings();
    }

    private void RefreshSettings()
    {
        _settings = AddressableAssetSettingsDefaultObject.Settings;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField(_settings ? AssetDatabase.GetAssetPath(_settings) : "(null)", EditorStyles.helpBox);
        }

        if (_settings == null)
        {
            EditorGUILayout.HelpBox("AddressableAssetSettingsDefaultObject.Settings 를 찾지 못했습니다.\nWindow > Asset Management > Addressables > Groups 에서 Initialize Addressables 를 먼저 해주세요.", MessageType.Error);
            if (GUILayout.Button("Refresh")) RefreshSettings();
            return;
        }

        EditorGUILayout.Space(6);

        DrawRulesUI();

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan", GUILayout.Height(28))) Scan();
            if (GUILayout.Button("Fix All (Safe)", GUILayout.Height(28))) FixAll();
            if (GUILayout.Button("Fix Labels", GUILayout.Height(28))) FixLabels();
            if (GUILayout.Button("Fix Group Paths", GUILayout.Height(28))) FixGroupPaths();
            if (GUILayout.Button("Fix Profiles", GUILayout.Height(28))) FixProfiles();
        }

        EditorGUILayout.Space(8);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (_checks.Count == 0)
        {
            EditorGUILayout.HelpBox("Scan을 눌러 진단 결과를 확인하세요.", MessageType.Info);
        }
        else
        {
            foreach (var c in _checks)
                DrawCheckRow(c);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "팁) 원격 배포를 진짜로 할 거면 RemoteBaseUrl을 프로필별로 다르게 두는 게 편합니다.\n" +
            "예) LocalDev=http://127.0.0.1:8000, RemoteStaging=https://staging.example.com, RemoteProd=https://cdn.example.com",
            MessageType.None);
    }

    private void DrawRulesUI()
    {
        EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);

        requiredLabelsCsv = EditorGUILayout.TextField("Required Labels (CSV)", requiredLabelsCsv);
        localGroupNamesCsv = EditorGUILayout.TextField("Local Group Names (CSV)", localGroupNamesCsv);
        remoteGroupNamesCsv = EditorGUILayout.TextField("Remote Group Names (CSV)", remoteGroupNamesCsv);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Profiles", EditorStyles.boldLabel);
        applyProfileFixToAllProfiles = EditorGUILayout.ToggleLeft("Apply profile fixes to ALL profiles", applyProfileFixToAllProfiles);

        remoteBaseUrlVarName = EditorGUILayout.TextField("Remote Base URL Var", remoteBaseUrlVarName);
        remoteBuildPathValue = EditorGUILayout.TextField("Remote.BuildPath Value", remoteBuildPathValue);
        remoteLoadPathTemplate = EditorGUILayout.TextField("Remote.LoadPath Template", remoteLoadPathTemplate);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
        enforceBuildRemoteCatalog = EditorGUILayout.ToggleLeft("Enforce Build Remote Catalog", enforceBuildRemoteCatalog);
        catalogRequestTimeoutSeconds = EditorGUILayout.IntField("Catalog Request Timeout (sec)", catalogRequestTimeoutSeconds);
    }

    private void DrawCheckRow(CheckItem c)
    {
        var (icon, color) = c.status switch
        {
            CheckStatus.Ok => ("✅", new Color(0.25f, 0.65f, 0.25f)),
            CheckStatus.Warn => ("⚠️", new Color(0.85f, 0.65f, 0.15f)),
            _ => ("❌", new Color(0.85f, 0.25f, 0.25f)),
        };

        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.color;
                GUI.color = color;
                GUILayout.Label(icon, GUILayout.Width(28));
                GUI.color = old;

                EditorGUILayout.LabelField(c.title, EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(c.fix == null))
                {
                    if (GUILayout.Button("Fix", GUILayout.Width(90), GUILayout.Height(20)))
                    {
                        c.fix?.Invoke();
                        Scan(); // refresh after fix
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(c.details))
                EditorGUILayout.LabelField(c.details, EditorStyles.wordWrappedLabel);
        }
    }

    // -------------------------
    // Scan
    // -------------------------
    private void Scan()
    {
        _checks.Clear();

        ScanLabels();
        ScanCatalog();
        ScanProfiles();
        ScanGroups();

        Repaint();
    }

    private void ScanLabels()
    {
        var required = SplitCsv(requiredLabelsCsv);
        var existing = _settings.GetLabels(); // returns all defined labels

        foreach (var label in required)
        {
            if (existing.Contains(label))
            {
                _checks.Add(new CheckItem
                {
                    title = $"Label: {label}",
                    status = CheckStatus.Ok,
                    details = "OK",
                    fix = null
                });
            }
            else
            {
                _checks.Add(new CheckItem
                {
                    title = $"Label missing: {label}",
                    status = CheckStatus.Error,
                    details = "Settings에 라벨이 정의되어 있지 않습니다. (Entry에 라벨을 달아도 테이블에 없으면 드롭다운/검증에서 꼬일 수 있음)",
                    fix = () => EnsureLabel(label)
                });
            }
        }
    }

    private void ScanCatalog()
    {
        if (enforceBuildRemoteCatalog && !_settings.BuildRemoteCatalog)
        {
            _checks.Add(new CheckItem
            {
                title = "Build Remote Catalog = OFF",
                status = CheckStatus.Warn,
                details = "원격 그룹을 운영하면서 카탈로그까지 원격으로 갱신하려면 ON 권장.",
                fix = () =>
                {
                    Undo.RecordObject(_settings, "Enable Build Remote Catalog");
                    _settings.BuildRemoteCatalog = true;
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                }
            });
        }
        else
        {
            _checks.Add(new CheckItem
            {
                title = $"Build Remote Catalog = {(_settings.BuildRemoteCatalog ? "ON" : "OFF")}",
                status = CheckStatus.Ok,
                details = "OK",
                fix = null
            });
        }

        if (catalogRequestTimeoutSeconds > 0 && _settings.CatalogRequestsTimeout != catalogRequestTimeoutSeconds)
        {
            _checks.Add(new CheckItem
            {
                title = $"CatalogRequestsTimeout = {_settings.CatalogRequestsTimeout}",
                status = (_settings.CatalogRequestsTimeout <= 0) ? CheckStatus.Warn : CheckStatus.Ok,
                details = "0이면 네트워크 이슈에서 무한 대기처럼 보일 수 있어요. (프로젝트 성격에 맞게 조정)",
                fix = () =>
                {
                    Undo.RecordObject(_settings, "Set CatalogRequestsTimeout");
                    _settings.CatalogRequestsTimeout = Mathf.Max(1, catalogRequestTimeoutSeconds);
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                }
            });
        }
        else
        {
            _checks.Add(new CheckItem
            {
                title = $"CatalogRequestsTimeout = {_settings.CatalogRequestsTimeout}",
                status = (_settings.CatalogRequestsTimeout <= 0) ? CheckStatus.Warn : CheckStatus.Ok,
                details = (_settings.CatalogRequestsTimeout <= 0) ? "0은 상황에 따라 무한대기처럼 보일 수 있어요." : "OK",
                fix = null
            });
        }

        // Remote catalog path references (sanity)
        bool remoteCatalogBuildOk = _settings.RemoteCatalogBuildPath != null && !string.IsNullOrEmpty(_settings.RemoteCatalogBuildPath.Id);
        bool remoteCatalogLoadOk  = _settings.RemoteCatalogLoadPath  != null && !string.IsNullOrEmpty(_settings.RemoteCatalogLoadPath.Id);

        if (!remoteCatalogBuildOk || !remoteCatalogLoadOk)
        {
            _checks.Add(new CheckItem
            {
                title = "RemoteCatalogBuild/LoadPath reference",
                status = CheckStatus.Warn,
                details = "RemoteCatalogBuildPath/LoadPath가 프로필 변수에 연결되어 있지 않을 수 있습니다.",
                fix = () =>
                {
                    Undo.RecordObject(_settings, "Fix RemoteCatalog Path references");
                    _settings.RemoteCatalogBuildPath.SetVariableByName(_settings, remoteBuildPathVarName);
                    _settings.RemoteCatalogLoadPath.SetVariableByName(_settings, remoteLoadPathVarName);
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                }
            });
        }
        else
        {
            _checks.Add(new CheckItem
            {
                title = "RemoteCatalogBuild/LoadPath reference",
                status = CheckStatus.Ok,
                details = "OK",
                fix = null
            });
        }
    }

    private void ScanProfiles()
    {
        var ps = _settings.profileSettings;
        if (ps == null)
        {
            _checks.Add(new CheckItem
            {
                title = "ProfileSettings missing",
                status = CheckStatus.Error,
                details = "Addressables ProfileSettings가 null 입니다.",
                fix = null
            });
            return;
        }

        var profileNames = ps.GetAllProfileNames();
        if (profileNames == null || profileNames.Count == 0)
        {
            _checks.Add(new CheckItem
            {
                title = "No profiles found",
                status = CheckStatus.Error,
                details = "프로필이 없습니다. Addressables 초기화가 덜 되었을 수 있어요.",
                fix = null
            });
            return;
        }

        var activeId = _settings.activeProfileId;
        var activeName = ps.GetProfileName(activeId);
        _checks.Add(new CheckItem
        {
            title = $"Active Profile: {activeName}",
            status = CheckStatus.Ok,
            details = $"activeProfileId = {activeId}",
            fix = null
        });

        bool hasRemoteBase = ps.GetVariableNames().Contains(remoteBaseUrlVarName);

        // Check remote vars for each profile
        foreach (var name in profileNames)
        {
            var id = ps.GetProfileId(name);
            if (string.IsNullOrEmpty(id)) continue;

            var remoteLoad = ps.GetValueByName(id, remoteLoadPathVarName);
            var remoteBuild = ps.GetValueByName(id, remoteBuildPathVarName);

            bool loadBad = IsUndefinedOrEmpty(remoteLoad);
            bool buildBad = IsUndefinedOrEmpty(remoteBuild);

            if (!hasRemoteBase)
            {
                _checks.Add(new CheckItem
                {
                    title = $"Profile '{name}': variable missing -> {remoteBaseUrlVarName}",
                    status = CheckStatus.Warn,
                    details = "Remote.LoadPath 템플릿에서 [RemoteBaseUrl]을 쓰려면 변수부터 있어야 합니다.",
                    fix = () => EnsureProfileVariable(remoteBaseUrlVarName, "http://127.0.0.1:8000")
                });
                hasRemoteBase = true; // so we don't spam
            }

            if (loadBad || buildBad)
            {
                _checks.Add(new CheckItem
                {
                    title = $"Profile '{name}': Remote paths undefined",
                    status = CheckStatus.Error,
                    details = $"Remote.BuildPath='{remoteBuild}' / Remote.LoadPath='{remoteLoad}'",
                    fix = () => FixProfileRemotePaths(name)
                });
            }
            else
            {
                _checks.Add(new CheckItem
                {
                    title = $"Profile '{name}': Remote paths",
                    status = CheckStatus.Ok,
                    details = $"Remote.BuildPath='{remoteBuild}'\nRemote.LoadPath='{remoteLoad}'",
                    fix = null
                });
            }
        }
    }

    private void ScanGroups()
    {
        var locals = SplitCsv(localGroupNamesCsv);
        var remotes = SplitCsv(remoteGroupNamesCsv);

        foreach (var gName in locals)
        {
            var g = _settings.FindGroup(gName);
            if (g == null)
            {
                _checks.Add(new CheckItem
                {
                    title = $"Local group missing: {gName}",
                    status = CheckStatus.Warn,
                    details = "그룹 이름이 다를 수 있어요. CSV를 현재 그룹명에 맞춰 수정하세요.",
                    fix = null
                });
                continue;
            }

            var bundled = g.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                _checks.Add(new CheckItem
                {
                    title = $"Group '{gName}': BundledAssetGroupSchema missing",
                    status = CheckStatus.Error,
                    details = "Packed Assets 템플릿이 아니라 Blank로 만들면 스키마가 없을 수 있어요.",
                    fix = () => EnsureBundledSchema(g)
                });
                continue;
            }

            bool buildOk = !string.IsNullOrEmpty(bundled.BuildPath.Id);
            bool loadOk = !string.IsNullOrEmpty(bundled.LoadPath.Id);

            if (!buildOk || !loadOk)
            {
                _checks.Add(new CheckItem
                {
                    title = $"Group '{gName}': Local Build/LoadPath not linked",
                    status = CheckStatus.Error,
                    details = $"BuildPath.Id='{bundled.BuildPath.Id}' / LoadPath.Id='{bundled.LoadPath.Id}'",
                    fix = () => LinkGroupPaths(g, isRemote:false)
                });
            }
            else
            {
                _checks.Add(new CheckItem
                {
                    title = $"Group '{gName}': Local Build/LoadPath",
                    status = CheckStatus.Ok,
                    details = "OK",
                    fix = null
                });
            }
        }

        foreach (var gName in remotes)
        {
            var g = _settings.FindGroup(gName);
            if (g == null)
            {
                _checks.Add(new CheckItem
                {
                    title = $"Remote group missing: {gName}",
                    status = CheckStatus.Warn,
                    details = "그룹 이름이 다를 수 있어요. CSV를 현재 그룹명에 맞춰 수정하세요.",
                    fix = null
                });
                continue;
            }

            var bundled = g.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                _checks.Add(new CheckItem
                {
                    title = $"Group '{gName}': BundledAssetGroupSchema missing",
                    status = CheckStatus.Error,
                    details = "Remote 그룹도 번들 스키마가 있어야 합니다.",
                    fix = () => EnsureBundledSchema(g)
                });
                continue;
            }

            bool buildOk = !string.IsNullOrEmpty(bundled.BuildPath.Id);
            bool loadOk = !string.IsNullOrEmpty(bundled.LoadPath.Id);

            if (!buildOk || !loadOk)
            {
                _checks.Add(new CheckItem
                {
                    title = $"Group '{gName}': Remote Build/LoadPath not linked",
                    status = CheckStatus.Error,
                    details = $"BuildPath.Id='{bundled.BuildPath.Id}' / LoadPath.Id='{bundled.LoadPath.Id}'",
                    fix = () => LinkGroupPaths(g, isRemote:true)
                });
            }
            else
            {
                _checks.Add(new CheckItem
                {
                    title = $"Group '{gName}': Remote Build/LoadPath",
                    status = CheckStatus.Ok,
                    details = "OK",
                    fix = null
                });
            }
        }
    }

    // -------------------------
    // Fix Actions
    // -------------------------
    private void FixAll()
    {
        FixLabels();
        FixProfiles();
        FixGroupPaths();

        if (enforceBuildRemoteCatalog && !_settings.BuildRemoteCatalog)
        {
            Undo.RecordObject(_settings, "Enable Build Remote Catalog");
            _settings.BuildRemoteCatalog = true;
        }

        if (catalogRequestTimeoutSeconds > 0 && (_settings.CatalogRequestsTimeout <= 0 || _settings.CatalogRequestsTimeout != catalogRequestTimeoutSeconds))
        {
            Undo.RecordObject(_settings, "Set CatalogRequestsTimeout");
            _settings.CatalogRequestsTimeout = Mathf.Max(1, catalogRequestTimeoutSeconds);
        }

        // Ensure catalog path references
        Undo.RecordObject(_settings, "Fix RemoteCatalog references");
        _settings.RemoteCatalogBuildPath.SetVariableByName(_settings, remoteBuildPathVarName);
        _settings.RemoteCatalogLoadPath.SetVariableByName(_settings, remoteLoadPathVarName);

        EditorUtility.SetDirty(_settings);
        AssetDatabase.SaveAssets();
        Scan();
    }

    private void FixLabels()
    {
        foreach (var label in SplitCsv(requiredLabelsCsv))
            EnsureLabel(label);

        AssetDatabase.SaveAssets();
        Scan();
    }

    private void FixGroupPaths()
    {
        foreach (var gName in SplitCsv(localGroupNamesCsv))
        {
            var g = _settings.FindGroup(gName);
            if (g) LinkGroupPaths(g, isRemote:false);
        }
        foreach (var gName in SplitCsv(remoteGroupNamesCsv))
        {
            var g = _settings.FindGroup(gName);
            if (g) LinkGroupPaths(g, isRemote:true);
        }

        AssetDatabase.SaveAssets();
        Scan();
    }

    private void FixProfiles()
    {
        // Ensure RemoteBaseUrl var exists if template uses it
        if (remoteLoadPathTemplate.Contains($"[{remoteBaseUrlVarName}]"))
            EnsureProfileVariable(remoteBaseUrlVarName, "http://127.0.0.1:8000");

        var ps = _settings.profileSettings;
        var names = ps.GetAllProfileNames();

        foreach (var name in names)
        {
            if (!applyProfileFixToAllProfiles)
            {
                // only active
                if (ps.GetProfileId(name) != _settings.activeProfileId) continue;
            }

            FixProfileRemotePaths(name);
        }

        EditorUtility.SetDirty(_settings);
        AssetDatabase.SaveAssets();
        Scan();
    }

    private void FixProfileRemotePaths(string profileName)
    {
        var ps = _settings.profileSettings;
        var id = ps.GetProfileId(profileName);
        if (string.IsNullOrEmpty(id)) return;

        Undo.RecordObject(_settings, "Fix Profile Remote Paths");

        // Only overwrite if undefined/empty (safe)
        var curBuild = ps.GetValueByName(id, remoteBuildPathVarName);
        if (IsUndefinedOrEmpty(curBuild))
            ps.SetValue(id, remoteBuildPathVarName, remoteBuildPathValue);

        var curLoad = ps.GetValueByName(id, remoteLoadPathVarName);
        if (IsUndefinedOrEmpty(curLoad))
            ps.SetValue(id, remoteLoadPathVarName, remoteLoadPathTemplate);

        // Also ensure RemoteCatalog path references are linked to profile variables
        _settings.RemoteCatalogBuildPath.SetVariableByName(_settings, remoteBuildPathVarName);
        _settings.RemoteCatalogLoadPath.SetVariableByName(_settings, remoteLoadPathVarName);

        EditorUtility.SetDirty(_settings);
    }

    private void EnsureLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return;

        var labels = _settings.GetLabels();
        if (labels.Contains(label)) return;

        Undo.RecordObject(_settings, "Add Addressables Label");
        _settings.AddLabel(label);
        EditorUtility.SetDirty(_settings);
    }

    private void EnsureProfileVariable(string variableName, string defaultValue)
    {
        var ps = _settings.profileSettings;
        var vars = ps.GetVariableNames();
        if (vars.Contains(variableName)) return;

        Undo.RecordObject(_settings, "Create Addressables Profile Variable");
        ps.CreateValue(variableName, defaultValue);
        EditorUtility.SetDirty(_settings);
        AssetDatabase.SaveAssets();
    }

    private void EnsureBundledSchema(AddressableAssetGroup g)
    {
        Undo.RecordObject(g, "Add BundledAssetGroupSchema");
        var schema = g.AddSchema(typeof(BundledAssetGroupSchema), false) as BundledAssetGroupSchema;
        if (schema != null)
        {
            schema.IncludeInBuild = true;
            EditorUtility.SetDirty(schema);
        }
        EditorUtility.SetDirty(g);
        AssetDatabase.SaveAssets();
    }

    private void LinkGroupPaths(AddressableAssetGroup g, bool isRemote)
    {
        if (g == null) return;

        var bundled = g.GetSchema<BundledAssetGroupSchema>();
        if (bundled == null)
        {
            EnsureBundledSchema(g);
            bundled = g.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null) return;
        }

        Undo.RecordObject(bundled, "Link Group Paths");

        var buildVar = isRemote ? remoteBuildPathVarName : localBuildPathVarName;
        var loadVar  = isRemote ? remoteLoadPathVarName  : localLoadPathVarName;

        bool okBuild = bundled.BuildPath.SetVariableByName(_settings, buildVar);
        bool okLoad  = bundled.LoadPath.SetVariableByName(_settings, loadVar);

        bundled.IncludeInBuild = true;

        EditorUtility.SetDirty(bundled);
        EditorUtility.SetDirty(g);

        if (!okBuild || !okLoad)
            Debug.LogWarning($"[AddressablesDoctor] Failed to link paths on group '{g.Name}' (build:{okBuild}, load:{okLoad}). Check profile variable names.");
    }

    // -------------------------
    // Helpers
    // -------------------------
    private static List<string> SplitCsv(string csv)
    {
        return (csv ?? "")
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private static bool IsUndefinedOrEmpty(string v)
    {
        if (string.IsNullOrEmpty(v)) return true;
        return v.Trim() == AddressableAssetProfileSettings.undefinedEntryValue;
    }
}


// Optional: run a quick scan on editor start (no auto-fix).
[InitializeOnLoad]
public static class AddressablesDoctorAutoCheck
{
    static AddressablesDoctorAutoCheck()
    {
        // Lightweight sanity check
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        var ps = settings.profileSettings;
        if (ps == null) return;

        var active = settings.activeProfileId;
        var load = ps.GetValueByName(active, "Remote.LoadPath");

        if (string.IsNullOrEmpty(load) || load.Trim() == AddressableAssetProfileSettings.undefinedEntryValue)
        {
            Debug.LogWarning("[AddressablesDoctor] Active profile Remote.LoadPath is <undefined>. Remote content will fail to load. Open Tools > Addressables > Doctor.");
        }
    }
}
#endif
