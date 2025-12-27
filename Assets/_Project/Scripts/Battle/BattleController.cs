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

        public int EnemyHP => _state != null ? _state.enemyHP : 0;
        public int EnemyMaxHP => _state != null ? _state.enemyMaxHP : 0;
        public int EnemyBlock => _state != null ? _state.enemyBlock : 0;
        
        public int EnemyVulnerableTurns => _state != null ? _state.enemyVulnerableTurns : 0;

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
            if (_state.enemyHP <= 0)
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

            if (animDirector != null)
                yield return animDirector.PlayEnemyAttackCo();

            // 3) 데미지 반영 + 팝업
            int hpLoss = DealDamageToPlayer_ReturnHpLoss(raw);
            if (hitPopups != null && hpLoss > 0) hitPopups.SpawnPlayer(hpLoss);

            if (_state.enemyVulnerableTurns > 0)
                _state.enemyVulnerableTurns -= 1;

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
                enemyHP = (run.PendingBattleType == MapNodeType.Boss) ? 60 : 30,
                enemyMaxHP = (run.PendingBattleType == MapNodeType.Boss) ? 60 : 30,
                playerBlock = 0,
                enemyBlock = 0,
                energy = run.Balance != null ? run.Balance.startEnergyPerTurn : 3,
                drawPerTurn = run.Balance != null ? run.Balance.startDrawPerTurn : 5,
            };

            _deck = new DeckRuntime(run.State.deck);
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
                    int hpLoss = DealDamageToEnemy_ReturnHpLoss(card.value);
                    if (hitPopups != null && hpLoss > 0) hitPopups.SpawnEnemy(hpLoss);
                    break;

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
                    ApplyVulnerableToEnemy(card.value);
                    Debug.Log($"[Battle] Play {card.id}: Apply Vulnerable +{card.value}. EnemyVuln={_state.enemyVulnerableTurns}");
                    break;
            }
        }

        private void ApplyVulnerableToEnemy(int turns)
        {
            turns = Mathf.Max(0, turns);
            if (turns <= 0) return;
            
            // 스택(누적)
            _state.enemyVulnerableTurns = Mathf.Clamp(_state.enemyVulnerableTurns + turns, 0, 99);
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

        private int DealDamageToEnemy_ReturnHpLoss(int amount)
        {
            amount = Mathf.Max(0, amount);

            if (_state.enemyVulnerableTurns > 0 && amount > 0)
                amount = Mathf.CeilToInt(amount * 1.5f);

            int hpBefore = _state.enemyHP;

            int remain = amount;
            if (_state.enemyBlock > 0)
            {
                int used = Mathf.Min(_state.enemyBlock, remain);
                _state.enemyBlock -= used;
                remain -= used;
            }

            if (remain > 0)
                _state.enemyHP -= remain;

            if (_state.enemyHP < 0) _state.enemyHP = 0;
            return Mathf.Max(0, hpBefore - _state.enemyHP);
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
