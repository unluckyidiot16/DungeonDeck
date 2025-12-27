// Assets/_Project/Scripts/Run/RunSession.cs

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;
using DungeonDeck.Config.Balance;
using DungeonDeck.Config.Map;
using DungeonDeck.Config.Oaths;
using DungeonDeck.Config.Cards;
using DungeonDeck.Config.Meta;
using DungeonDeck.Map;
using Random = UnityEngine.Random;

namespace DungeonDeck.Run
{
    public class RunSession : MonoBehaviour
    {
        public static RunSession I { get; private set; }

        public RunState State { get; private set; }
        public MapPlanDefinition Plan { get; private set; }
        public RunBalanceDefinition Balance { get; private set; }
        public OathDefinition Oath { get; private set; }
        
        public enum CardPoolContext { Reward, Shop }
        
        [Header("Databases")]
        [SerializeField] private CardLibraryDefinition cardLibrary;
        
        [Header("Reward / Card Pools")]
        [Tooltip("(선택) 메타 해금 풀 리졸버. null이면 Oath.basePools(+globalBasePools)만 사용")]
        [SerializeField] private RewardPoolResolver rewardPoolResolver;
            
        [Tooltip("(선택) 모든 서약 공통으로 항상 포함되는 풀들. 예: CommonPool")]
        [SerializeField] private List<CardPoolDefinition> globalBasePools = new();
        
        [Tooltip("(선택) 보상 전용으로 추가할 글로벌 풀들")]
        [SerializeField] private List<CardPoolDefinition> globalRewardPools = new();
            
        [Tooltip("(선택) 상점 전용으로 추가할 글로벌 풀들")]
        [SerializeField] private List<CardPoolDefinition> globalShopPools = new();
            
        [Header("Resources Auto-Load (Optional)")]
        [Tooltip("RunSession을 코드로 생성하는 경우(=인스펙터 세팅이 불가) Resources에서 자동 로드할 경로")]
        [SerializeField] private string cardLibraryResourcesPath = "CardLibraryDefinition";
            
        [Tooltip("RunSession을 코드로 생성하는 경우 Resources에서 자동 로드할 RewardPoolResolver 경로")]
        [SerializeField] private string rewardPoolResolverResourcesPath = "RewardPoolResolver";
            
            
        // 런타임 맵 플랜(재시작마다 새로 생성)
        public MapPlanDefinition PlanTemplate { get; private set; }
        public int MapSeed { get; private set; } = 1;
        private MapPlanDefinition _runtimePlan;

        // Battle context (minimal)
        public MapNodeType PendingBattleType { get; private set; } = MapNodeType.Battle;

        private void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);
            EnsureDatabasesLoaded();
        }

        public void StartNewRun(OathDefinition oath, RunBalanceDefinition balance, MapPlanDefinition plan)
        {
            Oath = oath;
            Balance = balance;
            PlanTemplate = plan != null ? plan : PlanTemplate;
            
            RegeneratePlanWithNewSeed();
            State = RunFactory.CreateNewRun(oath, balance);
            
            ApplyMetaRunStartBonuses(State);
            
            if (State != null) State.lastOutcome = RunEndOutcome.None;
            PendingBattleType = MapNodeType.Battle;
        }

        /// <summary>
        /// End 씬에서: 같은 Oath/Balance/Plan으로 런을 즉시 재시작하고 Map으로 이동 (Boot 스킵)
        /// </summary>
        public void RestartSameRunAndGoToMap()
        {
            // 기존 호출부 호환: 이제 "새 맵 + 메타 보너스" 리스타트로 동작
            RestartSameOathNewMapAndGoToMap();
        }
        
        public bool IsNodeCleared(int index) => State != null && index < State.nodeIndex;

        public MapNodeType GetNodeType(int index)
        {
            if (Plan == null || Plan.nodes == null || Plan.nodes.Count == 0)
                return MapNodeType.Battle;

            index = Mathf.Clamp(index, 0, Plan.nodes.Count - 1);
            return Plan.nodes[index];
        }

        public void EnterNode(int index)
        {
            if (State == null) return;

            // Lock index (M1: linear)
            index = Mathf.Clamp(index, 0, Plan.nodes.Count - 1);

            var type = GetNodeType(index);
            PendingBattleType = type;

            // We mark "current node" by index; clear happens when finished
            // For M1: Battle scene handles win/lose then advances or stays
            State.nodeIndex = index;

            // Scene load is handled by MapController (keeps RunSession simple)
        }

        public void MarkNodeClearedAndAdvance()
        {
            if (State == null || Plan == null) return;

            // ✅ "클리어한 노드" 타입 기준으로 전투 승리 카운트 증가 (Shop/Rest/Event는 제외)
            int clearedIndex = State.nodeIndex;
            var clearedType = GetNodeType(clearedIndex);
            if (clearedType == MapNodeType.Battle || clearedType == MapNodeType.Boss)
                State.runClearedBattles += 1;
            
            // cleared nodeIndex => next index
            State.nodeIndex += 1;
            State.nodeIndex = Mathf.Clamp(State.nodeIndex, 0, Plan.nodes.Count);
            // ✅ 마지막 노드(보스 포함)까지 끝냈다면 승리 결과를 기록 (아직 outcome이 없을 때만)
            if (IsRunFinished() && State.lastOutcome == RunEndOutcome.None)
                State.lastOutcome = RunEndOutcome.Victory;
        }

        public bool IsRunFinished()
        {
            if (Plan == null || State == null) return false;
            return State.nodeIndex >= Plan.nodes.Count;
        }
        
        /// <summary>
        /// ✅ End에서 Restart: 같은 Oath/Balance 유지 + 맵은 새 seed로 재생성 + 메타 보너스 적용 후 Map으로
        /// </summary>
        public void RestartSameOathNewMapAndGoToMap()
        {
            if (Oath == null || Balance == null) 
            {
                Debug.LogWarning("[RunSession] Restart failed: missing Oath/Balance. Going Boot.");
                SceneManager.LoadScene(SceneRoutes.Boot);
                return;
            }

            StartNewRun(Oath, Balance, PlanTemplate != null ? PlanTemplate : Plan);
            SceneManager.LoadScene(SceneRoutes.Map);
        }

        private void RegeneratePlanWithNewSeed()
        {
            var template = PlanTemplate != null ? PlanTemplate : Plan;
            if (template == null) return;

            MapSeed = Random.Range(1, int.MaxValue);

            if (_runtimePlan != null)
                Destroy(_runtimePlan);

            _runtimePlan = MapPlanRuntimeFactory.CreateRuntimePlan(template, MapSeed);
            Plan = _runtimePlan;
        }

        private void ApplyMetaRunStartBonuses(RunState state)
        {
            if (state == null) return;

            var meta = PlayerMetaProgress.LoadOrCreate();
            if (meta == null) return;

            if (meta.startGoldBonus != 0)
                state.gold += meta.startGoldBonus;

            if (meta.startMaxHpBonus != 0)
            {
                state.maxHP += meta.startMaxHpBonus;
                state.hp = state.maxHP; // 시작은 풀피로
            }

            if (meta.startBonusCardIds != null && meta.startBonusCardIds.Count > 0)
            {

                for (int i = 0; i < meta.startBonusCardIds.Count; i++)
                {
                    var id = meta.startBonusCardIds[i];
                   if (TryResolveCardById(id, out var card) && card != null)
                        state.deck.Add(card);
                }
            }

        }


        // -------------------------
        // Card Pools (Reward/Shop)
        // -------------------------

        private void EnsureDatabasesLoaded()
        {
            if (cardLibrary == null && !string.IsNullOrWhiteSpace(cardLibraryResourcesPath))
                cardLibrary = Resources.Load<CardLibraryDefinition>(cardLibraryResourcesPath);

            if (rewardPoolResolver == null && !string.IsNullOrWhiteSpace(rewardPoolResolverResourcesPath))
                rewardPoolResolver = Resources.Load<RewardPoolResolver>(rewardPoolResolverResourcesPath);
        }

        // RunSession.cs 내부에 추가
    // RunSession.cs 내부에 추가/교체
public void LoadFromSaveData(
    RunSaveManager.RunSaveData data,
    OathDefinition oath,
    RunBalanceDefinition balance,
    MapPlanDefinition planTemplate)
{
    if (data == null)
    {
        Debug.LogWarning("[RunSession] LoadFromSaveData: data null");
        return;
    }

    EnsureDatabasesLoaded();

    Oath = oath;
    Balance = balance;
    PlanTemplate = planTemplate;

    // 1) Plan 복원 (nodes snapshot 우선)
    MapSeed = (data.mapSeed != 0) ? data.mapSeed : UnityEngine.Random.Range(1, int.MaxValue);

    if (_runtimePlan != null)
        Destroy(_runtimePlan);

    if (data.planNodes != null && data.planNodes.Count > 0)
    {
        var loadedPlan = ScriptableObject.CreateInstance<MapPlanDefinition>();
        loadedPlan.hideFlags = HideFlags.DontSave;

        loadedPlan.nodes = new List<MapNodeType>(data.planNodes.Count);

        // enum max 안전화
        int maxEnum = Enum.GetValues(typeof(MapNodeType)).Length - 1;

        for (int i = 0; i < data.planNodes.Count; i++)
        {
            int v = Mathf.Clamp(data.planNodes[i], 0, maxEnum);
            loadedPlan.nodes.Add((MapNodeType)v);
        }

        _runtimePlan = loadedPlan;
        Plan = _runtimePlan;
    }
    else
    {
        // 스냅샷이 없으면 템플릿+시드로 재생성
        var template = PlanTemplate != null ? PlanTemplate : Plan;
        if (template != null)
        {
            _runtimePlan = MapPlanRuntimeFactory.CreateRuntimePlan(template, MapSeed);
            Plan = _runtimePlan;
        }
    }

    // 2) State 복원
    var s = new RunState();
    if (data.state != null)
    {
        s.oathId = string.IsNullOrWhiteSpace(data.state.oathId) ? (oath != null ? oath.id : "unknown") : data.state.oathId;

        s.seed = data.state.seed;
        s.maxHP = data.state.maxHP;
        s.hp = data.state.hp;
        s.gold = data.state.gold;

        s.nodeIndex = data.state.nodeIndex;
        s.runClearedBattles = data.state.runClearedBattles;
        s.rewardRollCount = data.state.rewardRollCount;
        s.lastOutcome = (RunEndOutcome)data.state.lastOutcome;

        // deck ids -> CardDefinition
        s.deck = new List<CardDefinition>();
        if (data.state.deckCardIds != null)
        {
            for (int i = 0; i < data.state.deckCardIds.Count; i++)
            {
                var id = data.state.deckCardIds[i];
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (TryResolveCardById(id, out var card) && card != null)
                    s.deck.Add(card);
                else
                    Debug.LogWarning($"[RunSession] Missing card id in load: {id}");
            }
        }

        // shop
        s.shopOfferSold = data.state.shopOfferSold != null ? new List<bool>(data.state.shopOfferSold) : new List<bool>();
        s.shopRerollCount = data.state.shopRerollCount;

        s.shopNodeIndex = data.state.shopNodeIndex;
        s.shopSeed = data.state.shopSeed;
        s.shopOfferIds = data.state.shopOfferIds != null ? new List<string>(data.state.shopOfferIds) : new List<string>();
        s.shopRemoveUsed = data.state.shopRemoveUsed;
    }
    else
    {
        s.oathId = oath != null ? oath.id : "unknown";
        s.seed = UnityEngine.Random.Range(1, int.MaxValue);
        s.maxHP = balance != null ? balance.startMaxHP : 60;
        s.hp = s.maxHP;
        s.gold = balance != null ? balance.startGold : 0;
        s.nodeIndex = 0;
        s.lastOutcome = RunEndOutcome.None;
    }

    State = s;

    // 3) PendingBattleType 안전 복원
    PendingBattleType = GetNodeType(State.nodeIndex);
}

        
        /// <summary>
        /// 현재 Run에서 사용할 카드 풀들(서약 기본 풀 + 글로벌 풀 + 메타 해금 풀).
        /// </summary>
        public IReadOnlyList<CardPoolDefinition> GetActiveCardPools()
            => GetActiveCardPools(CardPoolContext.Reward);
            
        public IReadOnlyList<CardPoolDefinition> GetActiveCardPools(CardPoolContext ctx)
        {
            EnsureDatabasesLoaded();

            var result = new List<CardPoolDefinition>();
            AddUniquePools(result, globalBasePools);
            if (ctx == CardPoolContext.Reward) AddUniquePools(result, globalRewardPools);
            if (ctx == CardPoolContext.Shop)   AddUniquePools(result, globalShopPools);
            
            if (Oath != null)
            {
                // 레거시(공용) 풀은 일단 둘 다 포함
                AddUniquePools(result, Oath.basePools);
                
                // 신규: rewardPools/shopPools가 있으면 컨텍스트별로 추가
                if (ctx == CardPoolContext.Reward) AddUniquePools(result, Oath.rewardPools);
                if (ctx == CardPoolContext.Shop)   AddUniquePools(result, Oath.shopPools);
            }

            var meta = PlayerMetaProgress.LoadOrCreate();
            if (rewardPoolResolver != null && meta != null && meta.unlockedPoolIds != null)
            {
                for (int i = 0; i < meta.unlockedPoolIds.Count; i++)
                {
                    var id = meta.unlockedPoolIds[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    var pool = rewardPoolResolver.FindKnownPool(id);
                    if (pool == null) continue;
                    
                    bool allow = (ctx == CardPoolContext.Reward) ? pool.AllowsReward : pool.AllowsShop;
                    if (allow && !result.Contains(pool))
                        result.Add(pool);
                }
            }

            return result;
        }

        /// <summary>
        /// 현재 Run에서 등장 가능한 카드들의 유니크 리스트(id 기준).
        /// - 풀 기반이 있으면 풀에서
        /// - 없으면 CardLibrary -> 없으면 현재 덱
        /// </summary>
        public List<CardDefinition> GetActiveCardCandidatesUnique()
            => GetActiveCardCandidatesUnique(CardPoolContext.Reward);
            
        public List<CardDefinition> GetActiveCardCandidatesUnique(CardPoolContext ctx)
        {
            var pools = GetActiveCardPools(ctx);
            if (pools != null && pools.Count > 0)
                return BuildUniqueCardsFromPools(pools);

            EnsureDatabasesLoaded();
            if (cardLibrary != null && cardLibrary.cards != null && cardLibrary.cards.Count > 0)
                return DedupById(cardLibrary.cards);

            if (State != null && State.deck != null && State.deck.Count > 0)
                return DedupById(State.deck);

            return new List<CardDefinition>();
        }

        public bool TryResolveCardById(string id, out CardDefinition card)
        {
            card = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            EnsureDatabasesLoaded();
            if (cardLibrary != null && cardLibrary.TryGet(id, out card) && card != null)
                return true;

            var candidates = GetActiveCardCandidatesUnique(CardPoolContext.Reward);
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c != null && c.id == id) { card = c; return true; }
            }

            candidates = GetActiveCardCandidatesUnique(CardPoolContext.Shop);
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c != null && c.id == id) { card = c; return true; }
            }
            
            return false;
        }

        private void AddUniquePools(List<CardPoolDefinition> dst, List<CardPoolDefinition> src)
        {
            if (dst == null || src == null) return;
            for (int i = 0; i < src.Count; i++)
            {
                var p = src[i];
                if (p == null) continue;
                if (!dst.Contains(p)) dst.Add(p);
            }
        }

        private List<CardDefinition> BuildUniqueCardsFromPools(IReadOnlyList<CardPoolDefinition> pools)
        {
            var map = new Dictionary<string, CardDefinition>();
            var outList = new List<CardDefinition>();

            for (int p = 0; p < pools.Count; p++)
            {
                var pool = pools[p];
                if (pool == null || pool.entries == null) continue;

                for (int i = 0; i < pool.entries.Count; i++)
                {
                    var e = pool.entries[i];
                    if (e == null || e.cardAsset == null) continue;

                    var cd = e.cardAsset;
                    if (cd == null) continue;
                    if (string.IsNullOrWhiteSpace(cd.id)) continue;
                    if (map.ContainsKey(cd.id)) continue;
                    map[cd.id] = cd;
                    outList.Add(cd);
                }
            }

            return outList; 
        }
 
        private List<CardDefinition> DedupById(IReadOnlyList<CardDefinition> src)
        {
            var map = new Dictionary<string, CardDefinition>();
            var outList = new List<CardDefinition>();
            if (src == null) return outList;

            for (int i = 0; i < src.Count; i++)
            {
                var c = src[i];
                if (c == null) continue;
                if (string.IsNullOrWhiteSpace(c.id)) continue;
                if (map.ContainsKey(c.id)) continue;

                map[c.id] = c;
                outList.Add(c);
            }

            return outList;
        }


        private void OnDestroy()
        {
            if (_runtimePlan != null)
                Destroy(_runtimePlan);
        }
        
        public void EndRun(RunEndOutcome outcome)
        {
            if (State == null) return;
            if (State.lastOutcome != RunEndOutcome.None) return; // 중복 종료 방지
            
            State.lastOutcome = outcome;
            SceneManager.LoadScene(SceneRoutes.End);
        }
        
    }
}
