// Assets/_Project/Scripts/Battle/BattleController.cs
using System;
using System.Collections;
using System.Linq;
using DungeonDeck.UI.Widgets;
using DungeonDeck.Rewards;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;
using DungeonDeck.Run;
using DungeonDeck.Config.Cards;
using DungeonDeck.Config.Map;
using Random = UnityEngine.Random;

namespace DungeonDeck.Battle
{
    public class BattleController : MonoBehaviour
    {
        [Header("Debug")]
        public bool autoWinForTest = false;

        private BattleState _state;
        private DeckRuntime _deck;
        
        public event Action StateChanged;
        
        [Header("Enemies")]
        [SerializeField, Range(1, 3)] private int debugEnemyCount = 1;

        [Serializable]
        public class EnemyState
        {
            public int hp;
            public int maxHp;
            public int block;
            public int vulnerableTurns;

            public bool IsAlive => hp > 0;
        }

        private readonly List<EnemyState> _enemies = new();
        private int _selectedEnemyIndex = 0;

        public int EnemyCount => _enemies.Count;
        public int SelectedEnemyIndex => _selectedEnemyIndex;

        private EnemyState GetSelectedEnemy()
        {
            if (_enemies.Count == 0) return null;
            _selectedEnemyIndex = Mathf.Clamp(_selectedEnemyIndex, 0, _enemies.Count - 1);
            return _enemies[_selectedEnemyIndex];
        }

        
        [Header("Reward (Win)")]
        [SerializeField] private CardChoicePanel rewardPanel;   // 배틀 씬 안에 비활성으로 두고 연결
        [SerializeField] private List<CardDefinition> fallbackRewardCandidates = new(); // 비었으면 현재 덱에서 후보를 뽑음
        [SerializeField] private bool useRunCardPools = true; // RunSession 카드풀 기반 후보/가중치 적용
        [SerializeField] private bool allowSkipReward = false;
        
        [Header("FX (optional)")]
        [SerializeField] private DungeonDeck.Battle.View.BattleAnimDirector animDirector;
        [SerializeField] private DungeonDeck.Battle.View.HitPopupSpawner hitPopups;

        private bool _isPlayerTurn = false;
        private bool _resolving = false;

        public bool IsPlayerTurn => _isPlayerTurn;
        public bool IsResolving => _resolving;
        
        private bool _endingFlow = false;
        
        private void AdvanceNodeAndRoute(RunSession run)
        {
            if (run == null) return;

            run.MarkNodeClearedAndAdvance();

            // Save after battle resolution + advance (or clear if run finished)
            if (RunSaveManager.I != null) RunSaveManager.I.SaveCurrentRun();

            SceneManager.LoadScene(run.IsRunFinished() ? SceneRoutes.End : SceneRoutes.Map);
        }

        
// UI/외부 조회용
        public int Energy => _state != null ? _state.energy : 0;
        public int PlayerHP => _state != null ? _state.playerHP : 0;
        public int PlayerMaxHP => _state != null ? _state.playerMaxHP : 0;
        public int PlayerBlock => _state != null ? _state.playerBlock : 0;

        public int EnemyHP => GetSelectedEnemy() != null ? GetSelectedEnemy().hp : 0;
        public int EnemyMaxHP => GetSelectedEnemy() != null ? GetSelectedEnemy().maxHp : 0;
        public int EnemyBlock => GetSelectedEnemy() != null ? GetSelectedEnemy().block : 0;

        
        public int EnemyVulnerableTurns => GetSelectedEnemy() != null ? GetSelectedEnemy().vulnerableTurns : 0;

        public int HandCount => _deck != null ? _deck.HandCount : 0;

        public CardDefinition GetHandCard(int index)
        {
            if (_deck == null) return null;
            return _deck.PeekHand(index);
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
        
        public void EnsureEnemyCount(int count)
        {
            count = Mathf.Clamp(count, 1, 3);

            // 보스는 항상 1마리(원하면 여기 규칙 삭제 가능)
            if (RunSession.I != null && RunSession.I.PendingBattleType == MapNodeType.Boss)
                count = 1;

            if (_enemies.Count == count) return;

            var run = RunSession.I;

            // 늘리기
            while (_enemies.Count < count)
                _enemies.Add(CreateDefaultEnemy(run, _enemies.Count));

            // 줄이기
            while (_enemies.Count > count)
                _enemies.RemoveAt(_enemies.Count - 1);

            _selectedEnemyIndex = Mathf.Clamp(_selectedEnemyIndex, 0, _enemies.Count - 1);
            NotifyStateChanged();
        }

        public bool SelectEnemy(int index)
        {
            if (_enemies.Count == 0) return false;
            _selectedEnemyIndex = Mathf.Clamp(index, 0, _enemies.Count - 1);
            NotifyStateChanged();
            return true;
        }

        private EnemyState CreateDefaultEnemy(RunSession run, int i)
        {
            bool boss = (run != null && run.PendingBattleType == MapNodeType.Boss);
            int baseHp = boss ? 60 : 30;

            // 살짝 변주(같은 스탯만 3개면 밋밋해서)
            int hp = boss ? baseHp : Mathf.Max(10, baseHp - i * 5);

            return new EnemyState
            {
                hp = hp,
                maxHp = hp,
                block = 0,
                vulnerableTurns = 0
            };
        }

        private bool AreAllEnemiesDefeated()
        {
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null && _enemies[i].IsAlive)
                    return false;
            return true;
        }

        private int CountAliveEnemies()
        {
            int n = 0;
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null && _enemies[i].IsAlive)
                    n++;
            return n;
        }

        private void AutoSelectNextAliveIfNeeded()
        {
            var sel = GetSelectedEnemy();
            if (sel != null && sel.IsAlive) return;

            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null && _enemies[i].IsAlive)
                {
                    _selectedEnemyIndex = i;
                    return;
                }
            }

            _selectedEnemyIndex = 0;
        }

        private EnemyState GetEnemy(int index)
        {
            if (_enemies.Count == 0) return null;
            index = Mathf.Clamp(index, 0, _enemies.Count - 1);
            return _enemies[index];
        }

        private void ApplyVulnerableToEnemy(int enemyIndex, int turns)
        {
            turns = Mathf.Max(0, turns);
            if (turns <= 0) return;

            var e = GetEnemy(enemyIndex);
            if (e == null) return;

            e.vulnerableTurns = Mathf.Clamp(e.vulnerableTurns + turns, 0, 99);
        }

        private int DealDamageToEnemy_ReturnHpLoss(int enemyIndex, int amount)
        {
            amount = Mathf.Max(0, amount);

            var e = GetEnemy(enemyIndex);
            if (e == null) return 0;

            if (e.vulnerableTurns > 0 && amount > 0)
                amount = Mathf.CeilToInt(amount * 1.5f);

            int hpBefore = e.hp;

            int remain = amount;
            if (e.block > 0)
            {
                int used = Mathf.Min(e.block, remain);
                e.block -= used;
                remain -= used;
            }

            if (remain > 0) e.hp -= remain;
            if (e.hp < 0) e.hp = 0;

            // ✅ 선택 중인 적이 죽었으면 다음 살아있는 적 자동 선택
            if (enemyIndex == _selectedEnemyIndex)
                AutoSelectNextAliveIfNeeded();

            return Mathf.Max(0, hpBefore - e.hp);
        }

        

        /// <summary>
        /// 손패 인덱스의 카드를 사용 시도. 성공/실패 반환.
        /// </summary>
        public bool TryPlayCardAt(int handIndex)
        {
            if (_endingFlow || _resolving || !_isPlayerTurn) return false;
            if (_deck == null || _state == null) return false;
            if (handIndex < 0 || handIndex >= _deck.HandCount) return false;

            var card = _deck.PeekHand(handIndex);
            if (card == null) return false;

            if (_state.energy < card.cost)
            {
                Debug.Log("[Battle] Not enough energy.");
                return false;
            }

            _state.energy -= card.cost;

            // 카드 효과 먼저 적용(드로우 등으로 손패가 늘어나도, 기존 인덱스 카드는 그대로 유지됨)
            ApplyCard(card);

            // 사용한 카드는 discard 또는 exhaust로
            if (card.exhaustOnPlay)
                _deck.ExhaustFromHand(handIndex);
            else 
                _deck.PlayFromHand(handIndex);

            // 승리 체크
            if (AreAllEnemiesDefeated())
            {
                NotifyStateChanged();
                EndBattle(true);
                return true;
            }

            NotifyStateChanged();
            return true;
        }

        /// <summary>
        /// 턴 종료 (UI 버튼 연결용)
        /// </summary>
        public void EndTurn()
        {
            if (_deck == null || _state == null) return;
            if (_endingFlow || _resolving || !_isPlayerTurn) return;

            StartCoroutine(EndTurnFlowCo());
        }

        private IEnumerator EndTurnFlowCo()
        {
            _resolving = true;
            _isPlayerTurn = false;

            // 1) 손패 버림
            _deck.DiscardHand();
            NotifyStateChanged();

            // 2) 적 공격 “연출” 먼저
            int raw = (RunSession.I.PendingBattleType == MapNodeType.Boss) ? 12 : 8;

            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e == null || !e.IsAlive) continue;

                if (animDirector != null)
                    yield return animDirector.PlayEnemyAttackCo(i);

                if (animDirector != null) animDirector.PlayPlayerHitFx();
                
                int hpLoss = DealDamageToPlayer_ReturnHpLoss(raw);
                if (hitPopups != null && hpLoss > 0) hitPopups.SpawnPlayer(hpLoss);

                NotifyStateChanged();

                if (_state.playerHP <= 0)
                {
                    EndBattle(false);
                    yield break;
                }
            }

            // ✅ 적 취약 턴 감소(전체)
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e == null) continue;
                if (e.vulnerableTurns > 0) e.vulnerableTurns -= 1;
            }


            NotifyStateChanged();

            if (_state.playerHP <= 0)
            {
                EndBattle(false);
                yield break;
            }

            // 4) 플레이어 턴 시작
            BeginPlayerTurn();
            NotifyStateChanged();
        }


        private void Start()
        {
            if (RunSession.I == null || RunSession.I.State == null)
            {
                Debug.LogError("[Battle] RunSession missing. Start from Boot.");
                SceneManager.LoadScene(SceneRoutes.Boot);
                return;
            }
            
            SetupBattle();
            
            if (animDirector == null) animDirector = FindObjectOfType<DungeonDeck.Battle.View.BattleAnimDirector>(true);
            if (hitPopups == null) hitPopups = FindObjectOfType<DungeonDeck.Battle.View.HitPopupSpawner>(true);
            
            BeginPlayerTurn();
            NotifyStateChanged();

            if (autoWinForTest)
                EndBattle(true);
        }

        private void SetupBattle()
        {
            var run = RunSession.I;

            _state = new BattleState
            {
                playerHP = run.State.hp,
                playerMaxHP = run.State.maxHP,
                playerBlock = 0,
                energy = run.Balance != null ? run.Balance.startEnergyPerTurn : 3,
                drawPerTurn = run.Balance != null ? run.Balance.startDrawPerTurn : 5,
            };

            _deck = new DeckRuntime(run.State.deck);

            // ✅ 적 리스트 초기화 (일반은 debugEnemyCount, 보스는 1)
            EnsureEnemyCount(debugEnemyCount);

            // 기본 선택
            SelectEnemy(0);
        }


        private void BeginPlayerTurn()
        {
            _state.playerBlock = 0;
            _state.energy = RunSession.I.Balance != null ? RunSession.I.Balance.startEnergyPerTurn : 3;

            _deck.Draw(_state.drawPerTurn);

            _isPlayerTurn = true;
            _resolving = false;
        }

        // Hook this to UI button later
        public void DebugPlayFirstCard()
        {
            TryPlayCardAt(0);
        }

        // Hook this to End Turn button later
        public void DebugEndTurn()
        {
            EndTurn();
        }

        private void ApplyCard(CardDefinition card)
        {
            switch (card.effectKind)
            {
                case CardEffectKind.Attack:
                {
                    int target = SelectedEnemyIndex;
                    int hpLoss = DealDamageToEnemy_ReturnHpLoss(target, card.value);
                    if (hitPopups != null && hpLoss > 0) hitPopups.SpawnEnemy(hpLoss, target);
                    break;
                }
                case CardEffectKind.Block:
                    _state.playerBlock += Mathf.Max(0, card.value);
                    Debug.Log($"[Battle] Play {card.id}: Block +{card.value}. Block={_state.playerBlock}");
                    break;

                case CardEffectKind.Draw:
                    _deck.Draw(Mathf.Max(0, card.value));
                    Debug.Log($"[Battle] Play {card.id}: Draw {card.value}. Hand={_deck.HandCount}");
                    break;

                case CardEffectKind.GainEnergy:
                    _state.energy += Mathf.Max(0, card.value);
                    Debug.Log($"[Battle] Play {card.id}: Energy +{card.value}. Energy={_state.energy}");
                    break;
                case CardEffectKind.ApplyVulnerable:
                {
                    int target = SelectedEnemyIndex;
                    ApplyVulnerableToEnemy(target, card.value);
                    var e = GetEnemy(target);
                    Debug.Log($"[Battle] Play {card.id}: Apply Vulnerable +{card.value}. TargetVuln={(e != null ? e.vulnerableTurns : 0)}");
                    break;
                }
            }
        }

        private void ApplyVulnerableToSelectedEnemy(int turns)
        {
            turns = Mathf.Max(0, turns);
            if (turns <= 0) return;

            var e = GetSelectedEnemy();
            if (e == null) return;

            e.vulnerableTurns = Mathf.Clamp(e.vulnerableTurns + turns, 0, 99);
        }

        private int DealDamageToSelectedEnemy_ReturnHpLoss(int amount)
        {
            amount = Mathf.Max(0, amount);

            var e = GetSelectedEnemy();
            if (e == null) return 0;

            if (e.vulnerableTurns > 0 && amount > 0)
                amount = Mathf.CeilToInt(amount * 1.5f);

            int hpBefore = e.hp;

            int remain = amount;
            if (e.block > 0)
            {
                int used = Mathf.Min(e.block, remain);
                e.block -= used;
                remain -= used;
            }

            if (remain > 0)
                e.hp -= remain;

            if (e.hp < 0) e.hp = 0;

            AutoSelectNextAliveIfNeeded();

            return Mathf.Max(0, hpBefore - e.hp);
        }

        private int DealDamageToPlayer_ReturnHpLoss(int amount)
        {
            amount = Mathf.Max(0, amount);

            int hpBefore = _state.playerHP;

            int remain = amount;
            if (_state.playerBlock > 0)
            {
                int used = Mathf.Min(_state.playerBlock, remain);
                _state.playerBlock -= used;
                remain -= used;
            }

            if (remain > 0)
                _state.playerHP -= remain;

            if (_state.playerHP < 0) _state.playerHP = 0;
            return Mathf.Max(0, hpBefore - _state.playerHP);
        }


        private void EndBattle(bool win)
        {
            if (_endingFlow) return;
            _endingFlow = true;

            // ✅ UI/입력 잠금 (BattleHandUI가 IsPlayerTurn/IsResolving로 버튼을 끄게)
            _isPlayerTurn = false;
            _resolving = true;
            NotifyStateChanged();

            var run = RunSession.I;

            // sync player hp back to run
            run.State.hp = _state.playerHP;

            if (!win)
            {
                Debug.Log("[Battle] LOSE");
                run.EndRun(RunEndOutcome.Defeat);

                // Ensure save is cleared on run end
                RunSaveManager.ClearSave();
                PlayerPrefs.Save();

                // ✅ End 씬으로 라우팅
                SceneManager.LoadScene(SceneRoutes.End);
                return;
            }

            Debug.Log("[Battle] WIN");

            // 골드 먼저 지급
            if (run.Balance != null) run.State.gold += run.Balance.winGold;

            // ✅ 승리 보상 선택 → 덱 추가 → 노드 클리어/진행 → 맵/엔드 라우팅
            StartCoroutine(WinRewardFlowCo());
        }

        
        private IEnumerator WinRewardFlowCo()
        {
            var run = RunSession.I;
            int battleNodeIndex = (run != null && run.State != null) ? run.State.nodeIndex : 0;

            // 패널 자동 탐색(인스펙터 연결 권장)
            var panel = rewardPanel != null ? rewardPanel : FindObjectOfType<CardChoicePanel>(true);

            // 패널이 없으면 그냥 진행(크래시 방지)
            if (panel == null)
            {
                Debug.LogWarning("[Battle] Reward panel not found. Skipping reward.");
                AdvanceNodeAndRoute(run);
                yield break;
            }

            // 1) 후보 리스트 구성
            var candidates = BuildRewardCandidates(run);
            if (candidates == null || candidates.Count == 0)
            {
                Debug.LogWarning("[Battle] No reward candidates. Advancing without reward.");
                AdvanceNodeAndRoute(run);
                yield break;
            }

            // 2) 3장 뽑기 (가중치 + 중복 패널티)
            var cfg = DungeonDeck.Rewards.CardRewardRollerCards.RollConfig.Default;
            cfg.duplicateWeightMultiplier = 0.35f;
            cfg.scaleByCopies = true;
            cfg.maxCopyExponent = 3;

            List<CardDefinition> options;
            int baseSeed = (run.State.seed != 0) ? run.State.seed : run.State.shopSeed; // fallback
            int salt = (run.State.rewardRollCount + 1) * 1009;
            int seed = unchecked(baseSeed * 10007 + battleNodeIndex * 97 + run.State.runClearedBattles * 13 + salt);
            if (seed == 0) seed = 1;
            List<CardPoolDefinition> pools = null;
            if (useRunCardPools && run != null)
            {
                var ro = run.GetActiveCardPools(RunSession.CardPoolContext.Reward);
                if (ro != null && ro.Count > 0) pools = new List<CardPoolDefinition>(ro);
            }
            if (pools != null && pools.Count > 0)
            {
                options = DungeonDeck.Rewards.CardRewardRollerCards.RollFromPools(
                    pools: pools,
                    ownedDeck: run.State.deck,
                    count: 3,
                    unique: true,
                    seed: seed,
                    configOpt: cfg
                    );
            }
            else
            {
                options = DungeonDeck.Rewards.CardRewardRollerCards.RollWeighted(
                    candidates: candidates,
                    ownedDeck: run.State.deck,
                    count: 3,
                    unique: true,
                    seed: seed,
                    configOpt: cfg
                    );
            }


            if (options == null || options.Count == 0)
            {
                Debug.LogWarning("[Battle] No reward options. Advancing without reward.");
                AdvanceNodeAndRoute(run);
                yield break;
            }

            // 3) UI Show & 선택 대기
            bool done = false;
            CardDefinition chosen = null;

            panel.Show(
                options,
                onChosen: c => { chosen = c; done = true; },
                onSkipped: allowSkipReward ? (() => { done = true; }) : null,
                title: "Reward: Choose 1 Card"
            );

            while (!done) yield return null;

            // ✅ 이번 승리 보상 롤 카운트 증가(세이브/재진입 시 같은 보상 반복 방지)
            run.State.rewardRollCount += 1;
            
            // 4) 선택 카드 덱에 추가
            if (chosen != null)
            {
                // run.State.deck는 List<CardDefinition> 가정 (현재 BattleController가 그렇게 쓰고 있음)
                if (run.State.deck == null) run.State.deck = new List<CardDefinition>();
                run.State.deck.Add(chosen);
                Debug.Log($"[Battle] Reward chosen: {chosen.id}");
            }

            AdvanceNodeAndRoute(run);
        }

private List<CardDefinition> BuildRewardCandidates(RunSession run)
{
    // 1) 인스펙터에 지정한 후보가 있으면 우선 사용
    if (fallbackRewardCandidates != null && fallbackRewardCandidates.Count > 0)
    {
        // null 제거 + 중복 제거(레퍼런스 기준)
        return fallbackRewardCandidates
                   .Where(c => c != null)
                   .GroupBy(c => c.id)
                   .Select(g => g.First())
                   .ToList();
    }

    // 2) 카드 풀 기반 후보(서약/메타 해금 포함)
    if (useRunCardPools && run != null)
    { 
        var poolCandidates = run.GetActiveCardCandidatesUnique(RunSession.CardPoolContext.Reward);
        if (poolCandidates != null && poolCandidates.Count > 0) 
            return poolCandidates;
    }
    
    // 3) 없으면 현재 덱 기반으로 후보 구성(최소 동작 보장)
    if (run != null && run.State != null && run.State.deck != null && run.State.deck.Count > 0)
    {
        return run.State.deck.Where(c => c != null).Distinct().ToList();
    }

    return new List<CardDefinition>();
}

        
    }
    
    

    // ----------------------------
    // Minimal battle model/runtime
    // ----------------------------
    public class BattleState
    {
        public int playerHP;
        public int playerMaxHP;
        public int playerBlock;

        public int enemyHP;
        public int enemyMaxHP;
        public int enemyBlock;
        public int enemyVulnerableTurns;

        public int energy;
        public int drawPerTurn;
    }

    public class DeckRuntime
    {
        private readonly List<CardDefinition> _draw = new();
        private readonly List<CardDefinition> _discard = new();
        private readonly List<CardDefinition> _hand = new();
        private readonly List<CardDefinition> _exhaust = new();

        public int HandCount => _hand.Count;

        public DeckRuntime(List<CardDefinition> sourceDeck)
        {
            if (sourceDeck != null) _draw.AddRange(sourceDeck);
            Shuffle(_draw);
        }

        public void Draw(int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (_draw.Count == 0)
                    Reshuffle();

                if (_draw.Count == 0)
                    return;

                var c = _draw[0];
                _draw.RemoveAt(0);
                _hand.Add(c);
            }
        }

        public CardDefinition PeekHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return null;
            return _hand[index];
        }

        public void PlayFromHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return;

            var c = _hand[index];
            _hand.RemoveAt(index);
            _discard.Add(c);
        }

        public void ExhaustFromHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return;
            
            var c = _hand[index];
            _hand.RemoveAt(index);
            _exhaust.Add(c);
        }
        
        public void DiscardHand()
        {
            if (_hand.Count == 0) return;
            _discard.AddRange(_hand);
            _hand.Clear();
        }

        private void Reshuffle()
        {
            if (_discard.Count == 0) return;
            _draw.AddRange(_discard);
            _discard.Clear();
            Shuffle(_draw);
        }

        private static void Shuffle(List<CardDefinition> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int r = Random.Range(i, list.Count);
                (list[i], list[r]) = (list[r], list[i]);
            }
        }
    }
}
