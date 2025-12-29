// Assets/_Project/Scripts/Battle/BattleController.cs (Refactored v2)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;
using DungeonDeck.Run;
using DungeonDeck.Config.Cards;
using DungeonDeck.Config.Map;
using DungeonDeck.UI.Widgets;
using DungeonDeck.Rewards;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 전투 흐름 제어 (리팩토링됨).
    /// 상태 관리는 BattlePlayerState, BattleEnemyManager에 위임.
    /// 액션 큐는 BattleActionQueue에 위임.
    /// 
    /// v2 변경사항:
    /// - ShouldAutoReturnToBase() 로직 개선: animDirector.IsAttacking 체크 추가
    /// - 공격 완료 후에만 자동 복귀
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Header("Debug")]
        public bool autoWinForTest = false;

        [Header("Enemies")]
        [SerializeField, Range(1, 3)] private int debugEnemyCount = 3;

        [Header("Reward")]
        [SerializeField] private CardChoicePanel rewardPanel;
        [SerializeField] private List<CardDefinition> fallbackRewardCandidates = new();
        [SerializeField] private bool useRunCardPools = true;
        [SerializeField] private bool allowSkipReward = false;

        [Header("FX")]
        [SerializeField] private View.BattleAnimDirector animDirector;
        [SerializeField] private View.HitPopupSpawner hitPopups;

        // ─────────────────────────────────────────
        // Sub-systems (분리된 책임)
        // ─────────────────────────────────────────
        private BattlePlayerState _player;
        private BattleEnemyManager _enemies;
        private BattleActionQueue _actionQueue;
        private DeckRuntime _deck;

        // ─────────────────────────────────────────
        // State
        // ─────────────────────────────────────────
        private bool _isPlayerTurn = false;
        private bool _resolving = false;
        private bool _endingFlow = false;
        private Coroutine _actionRunner = null;

        public event Action StateChanged;

        // ─────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────
        public bool IsPlayerTurn => _isPlayerTurn;
        public bool IsResolving => _resolving;

        public int Energy => _player?.Energy ?? 0;
        public int PlayerHP => _player?.HP ?? 0;
        public int PlayerMaxHP => _player?.MaxHP ?? 0;
        public int PlayerBlock => _player?.Block ?? 0;

        public int EnemyCount => _enemies?.Count ?? 0;
        public int SelectedEnemyIndex => _enemies?.SelectedIndex ?? 0;

        public int EnemyHP => _enemies?.GetHP(SelectedEnemyIndex) ?? 0;
        public int EnemyMaxHP => _enemies?.GetMaxHP(SelectedEnemyIndex) ?? 0;
        public int EnemyBlock => _enemies?.GetBlock(SelectedEnemyIndex) ?? 0;
        public int EnemyVulnerableTurns => _enemies?.GetVulnerableTurns(SelectedEnemyIndex) ?? 0;

        public int HandCount => _deck?.HandCount ?? 0;

        public int PendingActionCount => _actionQueue?.PendingCount ?? 0;
        public bool IsActionQueueRunning => _actionRunner != null;
        public bool HasQueuedEndTurn => _actionQueue?.HasQueuedEndTurn ?? false;

        // ─────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────
        private void Start()
        {
            if (RunSession.I == null || RunSession.I.State == null)
            {
                Debug.LogError("[Battle] RunSession missing. Start from Boot.");
                SceneManager.LoadScene(SceneRoutes.Boot);
                return;
            }

            InitializeSubsystems();
            SetupBattle();
            WireEvents();

            if (animDirector == null)
                animDirector = FindObjectOfType<View.BattleAnimDirector>(true);
            if (hitPopups == null)
                hitPopups = FindObjectOfType<View.HitPopupSpawner>(true);

            animDirector?.OnTargetChanged(SelectedEnemyIndex);
            
            BeginPlayerTurn();
            NotifyStateChanged();

            if (autoWinForTest)
                EndBattle(true);
        }

        private void InitializeSubsystems()
        {
            _player = new BattlePlayerState();
            _enemies = new BattleEnemyManager();
            _actionQueue = new BattleActionQueue();
        }

        private void WireEvents()
        {
            _player.StateChanged += NotifyStateChanged;
            _player.Died += () => EndBattle(false);

            _enemies.SelectionChanged += () => 
            {
                animDirector?.OnTargetChanged(_enemies.SelectedIndex);
                NotifyStateChanged();
            };
            _enemies.AllEnemiesDefeated += () => EndBattle(true);

            _actionQueue.OnExecuteCard += ExecuteQueuedCardCo;
            _actionQueue.OnExecuteEndTurn += EndTurnFlowCo;
            _actionQueue.QueueChanged += NotifyStateChanged;
        }

        private void SetupBattle()
        {
            var run = RunSession.I;

            _player.Initialize(run);
            _enemies.Initialize(debugEnemyCount, run);
            _deck = new DeckRuntime(run.State.deck);
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        // ─────────────────────────────────────────
        // Card Access
        // ─────────────────────────────────────────
        public CardDefinition GetHandCard(int index) => _deck?.PeekHand(index);

        // ─────────────────────────────────────────
        // Enemy Management (delegate)
        // ─────────────────────────────────────────
        public void EnsureEnemyCount(int count)
        {
            _enemies?.EnsureCount(count, RunSession.I);
            NotifyStateChanged();
        }

        public bool SelectEnemy(int index)
        {
            bool changed = _enemies?.Select(index) ?? false;
            if (changed) NotifyStateChanged();
            return changed;
        }

        // ─────────────────────────────────────────
        // Card Play
        // ─────────────────────────────────────────
        public bool TryPlayCardAt(int handIndex)
        {
            if (_endingFlow || !_isPlayerTurn) return false;
            if (_deck == null || _actionQueue == null) return false;
            if (_actionQueue.HasQueuedEndTurn) return false;
            if (handIndex < 0 || handIndex >= _deck.HandCount) return false;

            var card = _deck.PeekHand(handIndex);
            if (card == null) return false;

            bool enqueued = _actionQueue.EnqueueCard(
                card,
                handIndex,
                SelectedEnemyIndex,
                _player.Energy
            );

            if (!enqueued) return false;

            EnsureActionRunner();
            return true;
        }

        // ─────────────────────────────────────────
        // End Turn
        // ─────────────────────────────────────────
        public void EndTurn()
        {
            if (_endingFlow || _resolving || !_isPlayerTurn) return;

            if (_actionRunner != null || _actionQueue.PendingCount > 0)
            {
                _actionQueue.EnqueueEndTurn();
                EnsureActionRunner();
                return;
            }

            StartCoroutine(EndTurnFlowCo());
        }

        // ─────────────────────────────────────────
        // Action Queue Runner
        // ─────────────────────────────────────────
        private void EnsureActionRunner()
        {
            if (_actionRunner != null) return;
            _actionRunner = StartCoroutine(RunActionQueueCo());
        }

        private IEnumerator RunActionQueueCo()
        {
            try
            {
                yield return _actionQueue.ProcessAllCo();
            }
            finally
            {
                _actionRunner = null;
                NotifyStateChanged();
            }
        }

        private IEnumerator ExecuteQueuedCardCo(BattleActionQueue.QueuedAction action)
        {
            if (_endingFlow || !_isPlayerTurn) yield break;
            if (action.Card == null) yield break;

            // 현재 손패에서 카드 위치 찾기 (큐잉 중 인덱스 변경 대응)
            int handIndex = FindCurrentHandIndex(action);
            if (handIndex < 0) yield break;

            if (!_player.HasEnergy(action.Card.cost))
            {
                Debug.Log("[Battle] Not enough energy (exec).");
                yield break;
            }

            int targetIndex = ResolveTargetIndex(action.TargetIndex);

            // 1) 에너지 소비
            _player.TrySpendEnergy(action.Card.cost);

            // 2) 애니메이션 (✅ 완전히 완료될 때까지 대기)
            if (animDirector != null)
            {
                if (action.Card.effectKind == CardEffectKind.Attack)
                    yield return animDirector.PlayPlayerAttackComboCo(action.Card, targetIndex);
                else
                    yield return animDirector.PlayPlayerCardCo(action.Card, targetIndex);
            }

            // 3) 효과 적용
            ApplyCardEffect(action.Card, targetIndex);

            // 4) 카드 이동
            if (action.Card.exhaustOnPlay)
                _deck.ExhaustFromHand(handIndex);
            else
                _deck.PlayFromHand(handIndex);

            // 5) 승리 체크
            if (_enemies.AreAllDefeated())
            {
                EndBattle(true);
                yield break;
            }

            // 6) ✅ 개선된 자동 복귀 로직
            //    - 에너지 0이고 큐가 비었고 근접 상태일 때만
            //    - animDirector.IsAttacking이 false여야 함 (공격 완료 후)
            if (ShouldAutoReturnToBase())
            {
                yield return animDirector?.ReturnToBaseCo(false);
            }

            NotifyStateChanged();
        }

        private int FindCurrentHandIndex(BattleActionQueue.QueuedAction action)
        {
            if (action.HandIndex >= 0 &&
                action.HandIndex < _deck.HandCount &&
                _deck.PeekHand(action.HandIndex) == action.Card)
            {
                return action.HandIndex;
            }

            return _deck.FindHandIndex(action.Card);
        }

        private int ResolveTargetIndex(int requested)
        {
            if (_enemies == null || _enemies.Count == 0) return 0;

            requested = Mathf.Clamp(requested, 0, _enemies.Count - 1);
            var enemy = _enemies.GetAt(requested);

            if (enemy != null && enemy.IsAlive)
                return requested;

            _enemies.AutoSelectNextAlive();
            return _enemies.SelectedIndex;
        }

        /// <summary>
        /// ✅ 개선된 자동 복귀 조건 체크
        /// </summary>
        private bool ShouldAutoReturnToBase()
        {
            if (_endingFlow || !_isPlayerTurn) return false;
            if (_player == null || _player.Energy > 0) return false;
            if (_actionQueue.HasQueuedEndTurn) return false;
            if (_actionQueue.PendingCount > 0) return false;
            if (animDirector == null) return false;
            if (!animDirector.IsMelee) return false;
            
            // ✅ 공격 중이면 복귀하지 않음
            if (animDirector.IsAttacking) return false;
            
            return true;
        }

        // ─────────────────────────────────────────
        // Card Effects
        // ─────────────────────────────────────────
        private void ApplyCardEffect(CardDefinition card, int targetIndex)
        {
            switch (card.effectKind)
            {
                case CardEffectKind.Attack:
                    int hpLoss = _enemies.DealDamage(targetIndex, card.value);
                    if (hitPopups != null && hpLoss > 0)
                        hitPopups.SpawnEnemy(hpLoss, targetIndex);
                    break;

                case CardEffectKind.Block:
                    _player.GainBlock(card.value);
                    break;

                case CardEffectKind.Draw:
                    _deck.Draw(card.value);
                    break;

                case CardEffectKind.GainEnergy:
                    _player.GainEnergy(card.value);
                    break;

                case CardEffectKind.ApplyVulnerable:
                    _enemies.ApplyVulnerable(targetIndex, card.value);
                    break;
                
                case CardEffectKind.Heal: 
                    _player.Heal(card.value);
                    break;
            }

            NotifyStateChanged();
        }

        // ─────────────────────────────────────────
        // Turn Flow
        // ─────────────────────────────────────────
        private void BeginPlayerTurn()
        {
            _player.BeginTurn(RunSession.I);
            _deck.Draw(_player.DrawPerTurn);
            _isPlayerTurn = true;
            _resolving = false;

            NotifyStateChanged();
        }

        private IEnumerator EndTurnFlowCo()
        {
            _resolving = true;
            _isPlayerTurn = false;
            _actionQueue.Clear();

            // 근접 상태면 원위치 복귀
            if (animDirector != null)
                yield return animDirector.ReturnToBaseCo(true);

            // 손패 버림
            _deck.DiscardHand();
            NotifyStateChanged();

            // 적 공격
            yield return EnemyAttackPhaseCo();

            if (_player.IsAlive)
            {
                // 적 취약 턴 감소
                _enemies.TickVulnerableAll();
                BeginPlayerTurn();
            }
        }

        private IEnumerator EnemyAttackPhaseCo()
        {
            int rawDamage = RunSession.I.PendingBattleType == MapNodeType.Boss ? 12 : 8;

            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies.GetAt(i);
                if (enemy == null || !enemy.IsAlive) continue;

                if (animDirector != null)
                    yield return animDirector.PlayEnemyAttackCo(i);

                animDirector?.PlayPlayerHitFx();

                int hpLoss = _player.TakeDamage(rawDamage);
                if (hitPopups != null && hpLoss > 0)
                    hitPopups.SpawnPlayer(hpLoss);

                NotifyStateChanged();

                if (!_player.IsAlive)
                {
                    EndBattle(false);
                    yield break;
                }
            }
        }

        // ─────────────────────────────────────────
        // Battle End
        // ─────────────────────────────────────────
        private void EndBattle(bool win)
        {
            if (_endingFlow) return;
            _endingFlow = true;
            _isPlayerTurn = false;
            _resolving = true;

            NotifyStateChanged();

            var run = RunSession.I;
            _player.SyncToRun(run);

            if (!win)
            {
                Debug.Log("[Battle] LOSE");
                run.EndRun(RunEndOutcome.Defeat);
                RunSaveManager.ClearSave();
                PlayerPrefs.Save();
                SceneManager.LoadScene(SceneRoutes.End);
                return;
            }

            Debug.Log("[Battle] WIN");

            if (run.Balance != null)
                run.State.gold += run.Balance.winGold;

            StartCoroutine(WinRewardFlowCo());
        }

        private IEnumerator WinRewardFlowCo()
        {
            var run = RunSession.I;
            int battleNodeIndex = run?.State?.nodeIndex ?? 0;

            var panel = rewardPanel ?? FindObjectOfType<CardChoicePanel>(true);
            if (panel == null)
            {
                AdvanceNodeAndRoute(run);
                yield break;
            }

            var candidates = BuildRewardCandidates(run);
            if (candidates == null || candidates.Count == 0)
            {
                AdvanceNodeAndRoute(run);
                yield break;
            }

            var cfg = CardRewardRollerCards.RollConfig.Default;
            cfg.duplicateWeightMultiplier = 0.35f;

            int seed = ComputeRewardSeed(run, battleNodeIndex);

            List<CardDefinition> options;
            var pools = useRunCardPools ? run.GetActiveCardPools(RunSession.CardPoolContext.Reward) : null;

            if (pools != null && pools.Count > 0)
            {
                options = CardRewardRollerCards.RollFromPools(
                    new List<CardPoolDefinition>(pools),
                    run.State.deck, 3, true, seed, cfg);
            }
            else
            {
                options = CardRewardRollerCards.RollWeighted(
                    candidates, run.State.deck, 3, true, seed, cfg);
            }

            if (options == null || options.Count == 0)
            {
                AdvanceNodeAndRoute(run);
                yield break;
            }

            bool done = false;
            CardDefinition chosen = null;

            panel.Show(options,
                c => { chosen = c; done = true; },
                allowSkipReward ? () => { done = true; } : null,
                "Reward: Choose 1 Card");

            while (!done) yield return null;

            run.State.rewardRollCount += 1;

            if (chosen != null)
            {
                run.State.deck ??= new List<CardDefinition>();
                run.State.deck.Add(chosen);
                Debug.Log($"[Battle] Reward chosen: {chosen.id}");
            }

            AdvanceNodeAndRoute(run);
        }

        private int ComputeRewardSeed(RunSession run, int nodeIndex)
        {
            int baseSeed = run.State.seed != 0 ? run.State.seed : run.State.shopSeed;
            int salt = (run.State.rewardRollCount + 1) * 1009;
            int seed = unchecked(baseSeed * 10007 + nodeIndex * 97 + run.State.runClearedBattles * 13 + salt);
            return seed != 0 ? seed : 1;
        }

        private List<CardDefinition> BuildRewardCandidates(RunSession run)
        {
            if (fallbackRewardCandidates != null && fallbackRewardCandidates.Count > 0)
            {
                return fallbackRewardCandidates
                    .Where(c => c != null)
                    .GroupBy(c => c.id)
                    .Select(g => g.First())
                    .ToList();
            }

            if (useRunCardPools && run != null)
            {
                var poolCandidates = run.GetActiveCardCandidatesUnique(RunSession.CardPoolContext.Reward);
                if (poolCandidates != null && poolCandidates.Count > 0)
                    return poolCandidates;
            }

            if (run?.State?.deck != null && run.State.deck.Count > 0)
                return run.State.deck.Where(c => c != null).Distinct().ToList();

            return new List<CardDefinition>();
        }

        private void AdvanceNodeAndRoute(RunSession run)
        {
            if (run == null) return;

            run.MarkNodeClearedAndAdvance();
            RunSaveManager.I?.SaveCurrentRun();

            SceneManager.LoadScene(run.IsRunFinished() ? SceneRoutes.End : SceneRoutes.Map);
        }

        // ─────────────────────────────────────────
        // Debug Helpers
        // ─────────────────────────────────────────
        public void DebugPlayFirstCard() => TryPlayCardAt(0);
        public void DebugEndTurn() => EndTurn();
    }
}
