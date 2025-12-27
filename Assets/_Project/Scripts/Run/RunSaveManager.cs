using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;
using DungeonDeck.Config.Oaths;
using DungeonDeck.Config.Balance;
using DungeonDeck.Config.Map;

namespace DungeonDeck.Run
{
    /// <summary>
    /// 런 세이브/로드/중단 재개 (단일 세이브)
    /// - PlayerPrefs + JsonUtility
    /// - RunState.deck(SO 참조)을 cardId 리스트로 변환해 저장
    /// - Plan은 nodes(int) 스냅샷 저장 (재개 안정성↑)
    /// </summary>
    public class RunSaveManager : MonoBehaviour
    {
        public static RunSaveManager I { get; private set; }

        private const string SaveKey = "DungeonDeck.RunSave.v1.single";

        [Header("Auto Save")]
        public bool autoSaveOnSceneLoaded = false;
        public bool autoSaveOnAppPause = true;
        public bool autoSaveOnQuit = true;

        [Header("Skip Save Scenes")]
        public bool skipSaveOnBoot = true;
        public bool skipSaveOnTitle = true;
        public bool skipSaveOnEnd = true;

        [Header("Continue Behavior")]
        [Tooltip("저장된 sceneName이 Battle/Shop인데 현재 노드 타입과 안 맞으면 Map으로 강제합니다.")]
        public bool validateSceneWithNodeType = true;

        [Header("Optional Registries (recommended)")]
        public List<OathDefinition> knownOaths = new();
        public List<RunBalanceDefinition> knownBalances = new();
        public List<MapPlanDefinition> knownMapPlans = new();
        
        [Header("Ultra Safe Mode")]
        public bool forceSavedSceneNameToMap = true;

        
        public enum ContinueDestination
        {
            MapAlways,
            ShopOnlyElseMap,
            SavedScene
        }

        [Header("Continue Destination")]
        [SerializeField] private ContinueDestination continueDestination = ContinueDestination.ShopOnlyElseMap;


        private void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }

            I = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (I == this) I = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnApplicationPause(bool pause)
        {
            if (!autoSaveOnAppPause) return;
            if (pause) SaveCurrentRun();
        }

        private void OnApplicationQuit()
        {
            if (!autoSaveOnQuit) return;
            SaveCurrentRun();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!autoSaveOnSceneLoaded) return;
            SaveCurrentRun();
        }

        // -------------------------
        // Public API
        // -------------------------

        public static bool HasSave() => PlayerPrefs.HasKey(SaveKey);

        public static void ClearSave()
        {
            if (PlayerPrefs.HasKey(SaveKey))
                PlayerPrefs.DeleteKey(SaveKey);
        }

        public void SaveCurrentRun()
        {
            // ✅ 0) 런이 끝난 상태면 (어떤 씬이든) 세이브 삭제를 먼저 보장
            var run = RunSession.I;
            if (run != null && run.State != null && run.State.lastOutcome != RunEndOutcome.None)
            {
                ClearSave();
                PlayerPrefs.Save();
                return;
            }

            var currentScene = SceneManager.GetActiveScene().name;
            var sceneToSave = forceSavedSceneNameToMap ? SceneRoutes.Map : currentScene;

            // (기존 skipSaveOnBoot/Title/End 체크는 currentScene 기준으로 유지)
            if (skipSaveOnBoot && currentScene == SceneRoutes.Boot) return;
            if (skipSaveOnTitle && currentScene == SceneRoutes.Title) return;
            if (skipSaveOnEnd && currentScene == SceneRoutes.End) return;

            var data = Capture(run, sceneToSave);

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }


        /// <summary>
        /// Title에서 호출: 저장이 있으면 RunSession에 로드 적용 후 저장된 씬(또는 Map)으로 이동.
        /// </summary>
        public bool TryContinueFromSave(OathDefinition fallbackOath, RunBalanceDefinition fallbackBalance, MapPlanDefinition fallbackPlan)
        {
            if (!HasSave()) return false;

            var data = LoadRaw();
            if (data == null) return false;

            EnsureRunSessionExists();

            var oath = ResolveOath(data.oathId) ?? fallbackOath;
            var balance = ResolveBalance(data.balanceName) ?? fallbackBalance;
            var planTemplate = ResolvePlan(data.planTemplateName) ?? fallbackPlan;

            if (oath == null || balance == null)
            {
                Debug.LogWarning("[RunSaveManager] Continue failed: missing oath/balance. Clearing save.");
                ClearSave();
                PlayerPrefs.Save();
                return false;
            }

            RunSession.I.LoadFromSaveData(data, oath, balance, planTemplate);

            // Route
            string target;

            switch (continueDestination)
            {
                case ContinueDestination.SavedScene:
                    target = string.IsNullOrWhiteSpace(data.sceneName) ? SceneRoutes.Map : data.sceneName;
                    break;

                case ContinueDestination.ShopOnlyElseMap:
                    target = (data.sceneName == SceneRoutes.Shop) ? SceneRoutes.Shop : SceneRoutes.Map;
                    break;

                case ContinueDestination.MapAlways:
                default:
                    target = SceneRoutes.Map;
                    break;
            }

            if (target == SceneRoutes.Boot || target == SceneRoutes.Title)
                target = SceneRoutes.Map;

            if (validateSceneWithNodeType)
                target = ValidateTargetScene(target, RunSession.I);

            SceneManager.LoadScene(target);
            return true;
        }

        public bool TryPeekSummary(out RunSaveSummary summary)
        {
            summary = default;
            if (!HasSave()) return false;

            var data = LoadRaw();
            if (data == null) return false;

            summary = new RunSaveSummary
            {
                savedAtUtc = data.savedAtUtc,
                oathId = data.oathId,
                sceneName = data.sceneName,
                nodeIndex = data.state != null ? data.state.nodeIndex : 0,
                hp = data.state != null ? data.state.hp : 0,
                maxHp = data.state != null ? data.state.maxHP : 0,
                gold = data.state != null ? data.state.gold : 0
            };
            return true;
        }

        // -------------------------
        // Capture / Load
        // -------------------------

        private RunSaveData Capture(RunSession run, string sceneName)
        {
            if (run == null || run.State == null) return null;

            var s = run.State;

            var d = new RunSaveData
            {
                version = 1,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                sceneName = sceneName,

                oathId = s.oathId,
                balanceName = run.Balance != null ? run.Balance.name : "",
                planTemplateName = run.PlanTemplate != null ? run.PlanTemplate.name : "",

                mapSeed = run.MapSeed,
                planNodes = new List<int>(),
                state = new RunStateSave()
            };

            if (run.Plan != null && run.Plan.nodes != null)
            {
                for (int i = 0; i < run.Plan.nodes.Count; i++)
                    d.planNodes.Add((int)run.Plan.nodes[i]);
            }

            d.state.oathId = s.oathId;
            d.state.seed = s.seed;
            d.state.maxHP = s.maxHP;
            d.state.hp = s.hp;
            d.state.gold = s.gold;
            d.state.nodeIndex = s.nodeIndex;
            d.state.runClearedBattles = s.runClearedBattles;
            d.state.rewardRollCount = s.rewardRollCount;
            d.state.lastOutcome = (int)s.lastOutcome;

            d.state.deckCardIds = new List<string>();
            if (s.deck != null)
            {
                for (int i = 0; i < s.deck.Count; i++)
                {
                    var c = s.deck[i];
                    if (c == null || string.IsNullOrWhiteSpace(c.id)) continue;
                    d.state.deckCardIds.Add(c.id);
                }
            }

            d.state.shopOfferSold = (s.shopOfferSold != null) ? new List<bool>(s.shopOfferSold) : new List<bool>();
            d.state.shopRerollCount = s.shopRerollCount;

            d.state.shopNodeIndex = s.shopNodeIndex;
            d.state.shopSeed = s.shopSeed;
            d.state.shopOfferIds = (s.shopOfferIds != null) ? new List<string>(s.shopOfferIds) : new List<string>();
            d.state.shopRemoveUsed = s.shopRemoveUsed;

            return d;
        }

        private RunSaveData LoadRaw()
        {
            try
            {
                string json = PlayerPrefs.GetString(SaveKey, "");
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<RunSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSaveManager] Load failed: {e.Message}");
                return null;
            }
        }

        private void EnsureRunSessionExists()
        {
            if (RunSession.I != null) return;
            var go = new GameObject("RunSession");
            go.AddComponent<RunSession>();
        }

        // -------------------------
        // Resolve helpers
        // -------------------------

        private OathDefinition ResolveOath(string oathId)
        {
            if (string.IsNullOrWhiteSpace(oathId)) return null;

            for (int i = 0; i < knownOaths.Count; i++)
            {
                var o = knownOaths[i];
                if (o != null && o.id == oathId) return o;
            }

            var all = Resources.LoadAll<OathDefinition>("");
            for (int i = 0; i < all.Length; i++)
            {
                var o = all[i];
                if (o != null && o.id == oathId) return o;
            }

            return null;
        }

        private RunBalanceDefinition ResolveBalance(string balanceName)
        {
            if (string.IsNullOrWhiteSpace(balanceName)) return null;

            for (int i = 0; i < knownBalances.Count; i++)
            {
                var b = knownBalances[i];
                if (b != null && b.name == balanceName) return b;
            }

            var all = Resources.LoadAll<RunBalanceDefinition>("");
            for (int i = 0; i < all.Length; i++)
            {
                var b = all[i];
                if (b != null && b.name == balanceName) return b;
            }

            return null;
        }

        private MapPlanDefinition ResolvePlan(string planName)
        {
            if (string.IsNullOrWhiteSpace(planName)) return null;

            for (int i = 0; i < knownMapPlans.Count; i++)
            {
                var p = knownMapPlans[i];
                if (p != null && p.name == planName) return p;
            }

            var all = Resources.LoadAll<MapPlanDefinition>("");
            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if (p != null && p.name == planName) return p;
            }

            return null;
        }

        private string ValidateTargetScene(string target, RunSession run)
        {
            if (run == null || run.State == null) return SceneRoutes.Map;

            var nodeType = run.GetNodeType(run.State.nodeIndex);

            if (target == SceneRoutes.Battle)
            {
                if (nodeType != MapNodeType.Battle && nodeType != MapNodeType.Boss)
                    return SceneRoutes.Map;
            }

            if (target == SceneRoutes.Shop)
            {
                if (nodeType != MapNodeType.Shop)
                    return SceneRoutes.Map;
            }

            if (target == SceneRoutes.End)
            {
                if (run.State.lastOutcome == RunEndOutcome.None)
                    return SceneRoutes.Map;
            }

            return target;
        }

        // -------------------------
        // DTO
        // -------------------------

        [Serializable]
        public class RunSaveData
        {
            public int version = 1;
            public string savedAtUtc;
            public string sceneName;

            public string oathId;
            public string balanceName;
            public string planTemplateName;

            public int mapSeed;
            public List<int> planNodes;
            public RunStateSave state;
        }

        [Serializable]
        public class RunStateSave
        {
            public string oathId;

            public int seed;
            public int maxHP;
            public int hp;
            public int gold;

            public int nodeIndex;
            public int runClearedBattles;
            public int rewardRollCount;
            public int lastOutcome;

            public List<string> deckCardIds;

            public List<bool> shopOfferSold;
            public int shopRerollCount;

            public int shopNodeIndex;
            public int shopSeed;
            public List<string> shopOfferIds;
            public bool shopRemoveUsed;
        }

        public struct RunSaveSummary
        {
            public string savedAtUtc;
            public string oathId;
            public string sceneName;
            public int nodeIndex;
            public int hp;
            public int maxHp;
            public int gold;
        }
    }
}
