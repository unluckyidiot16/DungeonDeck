// Assets/Editor/DeckAddressablesSetupWindow.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class DeckAddressablesSetupWindow : EditorWindow
{
    [MenuItem("Tools/Addressables/Deck Setup (Groups/Labels/Sync)")]
    public static void Open()
    {
        var w = GetWindow<DeckAddressablesSetupWindow>();
        w.titleContent = new GUIContent("Deck Addr Setup");
        w.minSize = new Vector2(560, 420);
        w.Show();
    }

    private bool createFolders = true;
    private bool createOrRepairGroups = true;
    private bool createLabels = true;

    private bool syncAssetsFromFolders = false;
    private bool forceReassignGroup = true;
    private bool forceRewriteAddress = false;
    private bool forceRewriteLabels = false;

    // 폴더 루트(너가 만든 구조 기준)
    private string localRoot = "Assets/Addressables/Local";
    private string remoteRoot = "Assets/Addressables/Remote";

    // “폴더 → 주소(prefix)” 규칙에 쓰는 id
    private string playerId = "player_01";     // 너 프로젝트에 맞게 바꿔도 됨
    private string oathA = "oath_a";
    private string oathB = "oath_b";
    private string oathC = "oath_c";
    private string chapter1 = "chapter_01";
    private string chapter2 = "chapter_02";

    private Vector2 scroll;

    private void OnGUI()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Addressables Deck Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1) Groups/Labels 생성\n" +
            "2) (옵션) 폴더 스캔 → 엔트리 생성/이동 + 주소/라벨 자동 부여\n\n" +
            "※ Remote/Local Build/Load Path는 Addressables 프로필 설정(RemoteLoadPath 등)을 그대로 사용합니다.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Roots", EditorStyles.boldLabel);
            localRoot = EditorGUILayout.TextField("Local Root", localRoot);
            remoteRoot = EditorGUILayout.TextField("Remote Root", remoteRoot);

            EditorGUILayout.Space(4);
            playerId = EditorGUILayout.TextField("Player Id", playerId);
            oathA = EditorGUILayout.TextField("Oath A Id", oathA);
            oathB = EditorGUILayout.TextField("Oath B Id", oathB);
            oathC = EditorGUILayout.TextField("Oath C Id", oathC);
            chapter1 = EditorGUILayout.TextField("Chapter 1 Label", chapter1);
            chapter2 = EditorGUILayout.TextField("Chapter 2 Label", chapter2);
        }

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            createFolders = EditorGUILayout.ToggleLeft("Create folder skeleton (if missing)", createFolders);
            createOrRepairGroups = EditorGUILayout.ToggleLeft("Create/Repair Addressables groups", createOrRepairGroups);
            createLabels = EditorGUILayout.ToggleLeft("Create label table", createLabels);

            EditorGUILayout.Space(6);
            syncAssetsFromFolders = EditorGUILayout.ToggleLeft("Sync assets from folders (Create entries + set address/labels)", syncAssetsFromFolders);

            using (new EditorGUI.DisabledScope(!syncAssetsFromFolders))
            {
                forceReassignGroup = EditorGUILayout.ToggleLeft("Force move assets into configured group", forceReassignGroup);
                forceRewriteAddress = EditorGUILayout.ToggleLeft("Force rewrite address", forceRewriteAddress);
                forceRewriteLabels = EditorGUILayout.ToggleLeft("Force rewrite labels (clear then set)", forceRewriteLabels);
            }
        }

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledScope(settings == null))
        {
            if (GUILayout.Button("RUN: Setup Groups / Labels / (Optional) Sync", GUILayout.Height(38)))
            {
                Run(settings);
            }
        }

        if (settings == null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "AddressableAssetSettingsDefaultObject.Settings 가 null 입니다.\n" +
                "Window > Asset Management > Addressables > Groups 에서 Addressables 설정 생성 후 다시 실행하세요.",
                MessageType.Error);
        }

        EditorGUILayout.EndScrollView();
    }

    private void Run(AddressableAssetSettings settings)
    {
        try
        {
            if (createFolders) EnsureFolderSkeleton();
            var defs = BuildDefinitions();

            if (createLabels) EnsureLabels(settings, defs);
            if (createOrRepairGroups) EnsureGroups(settings, defs);

            if (syncAssetsFromFolders)
            {
                SyncAssets(settings, defs);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Deck Addr Setup", "완료! Console 로그를 확인하세요.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Deck Addr Setup", "실패: Console을 확인하세요.", "OK");
        }
    }

    // ------------------------------
    // Definitions
    // ------------------------------
    private class GroupDef
    {
        public string name;
        public bool isRemote;
        public string[] folders;          // 스캔할 폴더들
        public string addressPrefix;      // 주소 prefix
        public string[] labels;           // 기본 라벨
        public bool packTogether = true;  // 번들 전략 (기본: PackTogether)
        public bool staticContent;        // ContentUpdate: static 여부
    }

    private List<GroupDef> BuildDefinitions()
    {
        // 로컬/리모트 폴더 설계 (필요하면 너 프로젝트에 맞게 폴더만 조정)
        string L_BOOT = $"{localRoot}/Bootstrap";
        string L_UI = $"{localRoot}/UI_Core";
        string L_DATA = $"{localRoot}/Data";

        string R_PLAYER = $"{remoteRoot}/Player";
        string R_EN_CH1 = $"{remoteRoot}/Enemies/Ch1";
        string R_EN_CH2 = $"{remoteRoot}/Enemies/Ch2";
        string R_BOSS = $"{remoteRoot}/Enemies/Bosses";

        string R_CARD_A = $"{remoteRoot}/CardArt/{oathA}";
        string R_CARD_B = $"{remoteRoot}/CardArt/{oathB}";
        string R_CARD_C = $"{remoteRoot}/CardArt/{oathC}";

        string R_RELIC = $"{remoteRoot}/RelicArt";
        string R_FX_AUDIO = $"{remoteRoot}/FXAudio";

        var list = new List<GroupDef>
        {
            // Local
            new GroupDef {
                name = "L00_Bootstrap_Local",
                isRemote = false,
                folders = new[]{ L_BOOT },
                addressPrefix = "boot/",
                labels = new[]{ "type_ui", "pack_bootstrap" },
                packTogether = true,
                staticContent = true,
            },
            new GroupDef {
                name = "L10_UI_Core_Local",
                isRemote = false,
                folders = new[]{ L_UI },
                addressPrefix = "ui/",
                labels = new[]{ "type_ui", "pack_ui_core" },
                packTogether = true,
                staticContent = true,
            },
            new GroupDef {
                name = "L20_Data_Local",
                isRemote = false,
                folders = new[]{ L_DATA },
                addressPrefix = "data/",
                labels = new[]{ "type_data", "pack_data" },
                packTogether = true,
                staticContent = true,
            },

            // Remote
            new GroupDef {
                name = "R10_Player_Art_Remote",
                isRemote = true,
                folders = new[]{ R_PLAYER },
                addressPrefix = $"unit/player/{playerId}/",
                labels = new[]{ "type_player", $"pack_player_{playerId}" },
                packTogether = true,
                staticContent = false,
            },
            new GroupDef {
                name = "R20_Enemies_Ch1_Remote",
                isRemote = true,
                folders = new[]{ R_EN_CH1 },
                addressPrefix = "unit/enemy/",
                labels = new[]{ "type_enemy", "pack_enemy_ch1", chapter1 },
                packTogether = true,
                staticContent = false,
            },
            new GroupDef {
                name = "R21_Enemies_Ch2_Remote",
                isRemote = true,
                folders = new[]{ R_EN_CH2 },
                addressPrefix = "unit/enemy/",
                labels = new[]{ "type_enemy", "pack_enemy_ch2", chapter2 },
                packTogether = true,
                staticContent = false,
            },
            new GroupDef {
                name = "R22_Bosses_Remote",
                isRemote = true,
                folders = new[]{ R_BOSS },
                addressPrefix = "unit/boss/",
                labels = new[]{ "type_enemy", "pack_bosses" },
                packTogether = true,
                staticContent = false,
            },
            new GroupDef {
                name = $"R30_CardArt_{oathA}_Remote",
                isRemote = true,
                folders = new[]{ R_CARD_A },
                addressPrefix = $"card/{oathA}/",
                labels = new[]{ "type_cardart", oathA, $"pack_card_{oathA}" },
                packTogether = true,
                staticContent = false,
            },
            new GroupDef {
                name = $"R31_CardArt_{oathB}_Remote",
                isRemote = true,
                folders = new[]{ R_CARD_B },
                addressPrefix = $"card/{oathB}/",
                labels = new[]{ "type_cardart", oathB, $"pack_card_{oathB}" },
                packTogether = true,
                staticContent = false,
            },
            new GroupDef {
                name = $"R32_CardArt_{oathC}_Remote",
                isRemote = true,
                folders = new[]{ R_CARD_C },
                addressPrefix = $"card/{oathC}/",
                labels = new[]{ "type_cardart", oathC, $"pack_card_{oathC}" },
                packTogether = true,
                staticContent = false,
            },
            new GroupDef {
                name = "R40_RelicArt_Remote",
                isRemote = true,
                folders = new[]{ R_RELIC },
                addressPrefix = "relic/",
                labels = new[]{ "type_relicart", "pack_relic" },
                packTogether = true,
                staticContent = false,
            },
            new GroupDef {
                name = "R50_FX_Audio_Remote",
                isRemote = true,
                folders = new[]{ R_FX_AUDIO },
                addressPrefix = "fxaudio/",
                labels = new[]{ "type_fx", "type_audio", "pack_fx_audio" },
                packTogether = true,
                staticContent = false,
            },
        };

        return list;
    }

    // ------------------------------
    // Folder Skeleton
    // ------------------------------
    private void EnsureFolderSkeleton()
    {
        EnsureFolder("Assets/Addressables");
        EnsureFolder(localRoot);
        EnsureFolder(remoteRoot);

        // Local
        EnsureFolder($"{localRoot}/Bootstrap");
        EnsureFolder($"{localRoot}/UI_Core");
        EnsureFolder($"{localRoot}/Data");

        // Remote
        EnsureFolder($"{remoteRoot}/Player");
        EnsureFolder($"{remoteRoot}/Enemies");
        EnsureFolder($"{remoteRoot}/Enemies/Ch1");
        EnsureFolder($"{remoteRoot}/Enemies/Ch2");
        EnsureFolder($"{remoteRoot}/Enemies/Bosses");

        EnsureFolder($"{remoteRoot}/CardArt");
        EnsureFolder($"{remoteRoot}/CardArt/{oathA}");
        EnsureFolder($"{remoteRoot}/CardArt/{oathB}");
        EnsureFolder($"{remoteRoot}/CardArt/{oathC}");

        EnsureFolder($"{remoteRoot}/RelicArt");
        EnsureFolder($"{remoteRoot}/FXAudio");

        Debug.Log("[DeckAddrSetup] Folder skeleton ensured.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        var name = Path.GetFileName(path);

        if (string.IsNullOrEmpty(parent) || parent == "Assets")
        {
            if (!AssetDatabase.IsValidFolder("Assets/" + name))
                AssetDatabase.CreateFolder("Assets", name);
            return;
        }

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    // ------------------------------
    // Labels
    // ------------------------------
    private void EnsureLabels(AddressableAssetSettings settings, List<GroupDef> defs)
    {
        var labels = new HashSet<string>();

        // 기본 분류 라벨
        labels.Add("type_ui");
        labels.Add("type_data");
        labels.Add("type_player");
        labels.Add("type_enemy");
        labels.Add("type_cardart");
        labels.Add("type_relicart");
        labels.Add("type_fx");
        labels.Add("type_audio");

        // pack 라벨(번들/분할 의도)
        labels.Add("pack_bootstrap");
        labels.Add("pack_ui_core");
        labels.Add("pack_data");
        labels.Add($"pack_player_{playerId}");
        labels.Add("pack_enemy_ch1");
        labels.Add("pack_enemy_ch2");
        labels.Add("pack_bosses");
        labels.Add($"pack_card_{oathA}");
        labels.Add($"pack_card_{oathB}");
        labels.Add($"pack_card_{oathC}");
        labels.Add("pack_relic");
        labels.Add("pack_fx_audio");

        // oath/chapter 라벨
        labels.Add(oathA);
        labels.Add(oathB);
        labels.Add(oathC);
        labels.Add(chapter1);
        labels.Add(chapter2);

        // defs에 정의된 라벨도 합치기
        foreach (var d in defs)
            foreach (var l in d.labels)
                labels.Add(l);

        var existing = new HashSet<string>(settings.GetLabels());
        int added = 0;

        foreach (var l in labels)
        {
            if (string.IsNullOrWhiteSpace(l)) continue;
            if (existing.Contains(l)) continue;
            settings.AddLabel(l);
            added++;
        }

        Debug.Log($"[DeckAddrSetup] Labels ensured. Added={added}, Total={settings.GetLabels().Count}");
    }

    // ------------------------------
    // Groups
    // ------------------------------
    private void EnsureGroups(AddressableAssetSettings settings, List<GroupDef> defs)
    {
        foreach (var d in defs)
        {
            var group = settings.FindGroup(d.name);
            if (group == null)
            {
                group = settings.CreateGroup(
                    d.name,
                    false, false, false,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema)
                );
                Debug.Log($"[DeckAddrSetup] Group created: {d.name}");
            }

            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled != null)
            {
                // Local vs Remote Build/Load path: Addressables 프로필 변수를 사용
                if (d.isRemote)
                {
                    bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                    bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                }
                else
                {
                    bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                    bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                }

                bundled.IncludeInBuild = true;

                // 번들 분할: 기본은 PackTogether
                bundled.BundleMode = d.packTogether
                    ? BundledAssetGroupSchema.BundlePackingMode.PackTogether
                    : BundledAssetGroupSchema.BundlePackingMode.PackSeparately;

                // 압축은 기본값 유지(원하면 LZ4로 강제해도 됨)
                // bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;

                EditorUtility.SetDirty(group);
            }

            var contentUpdate = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdate != null)
            {
                contentUpdate.StaticContent = d.staticContent;
                EditorUtility.SetDirty(group);
            }
        }

        Debug.Log("[DeckAddrSetup] Groups repaired.");
    }

    // ------------------------------
    // Sync assets
    // ------------------------------
    private void SyncAssets(AddressableAssetSettings settings, List<GroupDef> defs)
    {
        int totalEntries = 0;

        foreach (var def in defs)
        {
            var group = settings.FindGroup(def.name);
            if (group == null)
            {
                Debug.LogWarning($"[DeckAddrSetup] Missing group: {def.name}");
                continue;
            }

            foreach (var folder in def.folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[DeckAddrSetup] Folder missing (skip): {folder}");
                    continue;
                }

                var guids = AssetDatabase.FindAssets("", new[] { folder });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;

                    // 폴더/메타/스크립트류 제외
                    if (AssetDatabase.IsValidFolder(path)) continue;
                    if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                    if (path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)) continue;

                    // 에디터용/Addressables 설정 파일 제외(실수 방지)
                    if (path.Contains("/Editor/")) continue;
                    if (path.Contains("AddressableAssetSettings.asset")) continue;

                    var entry = settings.FindAssetEntry(guid);
                    if (entry == null || forceReassignGroup)
                    {
                        entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    }

                    if (entry == null) continue;

                    // Address 생성: prefix + relativePath(확장자 제거, 소문자, 슬래시 통일)
                    if (forceRewriteAddress || string.IsNullOrEmpty(entry.address))
                    {
                        string addr = BuildAddress(def.addressPrefix, folder, path);
                        entry.address = addr;
                    }

                    // Labels
                    if (forceRewriteLabels)
                    {
                        // labels를 직접 clear하면 버전에 따라 접근이 달라질 수 있어 SetLabel로 off 처리
                        foreach (var l in settings.GetLabels())
                        {
                            TrySetLabel(entry, l, false);
                        }
                    }

                    foreach (var l in def.labels)
                    {
                        TrySetLabel(entry, l, true);
                    }

                    totalEntries++;
                }
            }
        }

        Debug.Log($"[DeckAddrSetup] Sync done. Entries processed={totalEntries}");
    }

    private static string BuildAddress(string prefix, string folderRoot, string assetPath)
    {
        // assetPath에서 folderRoot 아래 상대경로를 뽑고 prefix 붙이기
        // 예: Assets/Addressables/Remote/CardArt/oath_a/Slash01.png
        // -> card/oath_a/slash01
        var rel = assetPath.Replace("\\", "/");

        // folderRoot가 rel의 일부가 아닐 수도 있으니 안전 처리
        var root = folderRoot.Replace("\\", "/").TrimEnd('/');
        int idx = rel.IndexOf(root, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            rel = rel.Substring(idx + root.Length).TrimStart('/');
        }
        else
        {
            // fallback: Assets/ 기준 상대경로
            if (rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                rel = rel.Substring("Assets/".Length);
        }

        // 확장자 제거
        rel = Path.ChangeExtension(rel, null);
        rel = rel.Replace("\\", "/").Trim('/').ToLowerInvariant();

        prefix = (prefix ?? "").Trim().Replace("\\", "/");
        if (!prefix.EndsWith("/")) prefix += "/";

        return (prefix + rel).Replace("//", "/");
    }

    private static void TrySetLabel(UnityEditor.AddressableAssets.Settings.AddressableAssetEntry entry, string label, bool enabled)
    {
        if (entry == null) return;
        if (string.IsNullOrWhiteSpace(label)) return;

        // Addressables 버전에 따라 SetLabel signature가 다를 수 있어 try/catch로 안전 처리
        try
        {
            // 흔한 시그니처: SetLabel(string, bool, bool)
            entry.SetLabel(label, enabled, true);
        }
        catch
        {
            try
            {
                // 다른 시그니처: SetLabel(string, bool)
                entry.SetLabel(label, enabled);
            }
            catch
            {
                // 최후: labels 컬렉션 직접 접근(버전마다 다를 수 있음)
                try
                {
                    var labelsField = entry.GetType().GetField("m_Labels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (labelsField != null)
                    {
                        var set = labelsField.GetValue(entry) as HashSet<string>;
                        if (set != null)
                        {
                            if (enabled) set.Add(label);
                            else set.Remove(label);
                        }
                    }
                }
                catch { /* ignore */ }
            }
        }
    }
}
#endif
