// Assets/_Project/Scripts/Battle/BattleController.cs (Refactored v4 - Slot Index Based)
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
    /// 전투 흐름 제어 (v4 - 슬롯 인덱스 기반).
    /// 슬롯 인덱스를 일관되게 사용하여 AnimDirector/TargetManager와 동기화합니다.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Header("Debug")]
        public bool autoWinForTest = false;

        [Header("Reward")]
        [SerializeField] private CardChoicePanel rewardPanel;
        [SerializeField] private List<CardDefinition> fallbackRewardCandidates = new();
        [SerializeField] private bool useRunCardPools = true;
        [SerializeField] private bool allowSkipReward = false;

        [Header("FX")]
        [SerializeField] private View.BattleAnimDirector animDirector;
        [SerializeField] private View.HitPopupSpawner hitPopups;

        [Header("Target UI")]
        [Tooltip("현재 선택된 적을 표시하는 타겟 UI 매니저")]
        public View.BattleTargetManager targetManager;

        // ─────────────────────────────────────────────────
        // Sub-systems
        // ─────────────────────────────────────────────────
        private BattlePlayerState _player;
        private BattleEnemyManager _enemies;
        private BattleActionQueue _actionQueue;
        private DeckRuntime _deck;

        private bool _subsystemsReady = false;
        private bool _eventsWired = false;
        
        // ─────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────
        private bool _isPlayerTurn = false;
        private bool _resolving = false;
        private bool _endingFlow = false;
        private Coroutine _actionRunner = null;

        public event Action StateChanged;

        // ─────────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────────
        public bool IsPlayerTurn => _isPlayerTurn;
        public bool IsResolving => _resolving;

        public int Energy => _player?.Energy ?? 0;
        public int PlayerHP => _player?.HP ?? 0;
        public int PlayerMaxHP => _player?.MaxHP ?? 0;
        public int PlayerBlock => _player?.Block ?? 0;

        public int EnemyCount => _enemies?.Count ?? 0;
        public BattleEnemy SelectedEnemy => _enemies?.Selected;
        
        /// <summary>
        /// ✅ 선택된 적의 슬롯 인덱스 (AnimDirector/TargetManager와 동기화용)
        /// </summary>
        public int SelectedEnemyIndex => _enemies?.SelectedSlotIndex ?? 0;

        // 하위 호환용 프로퍼티 (선택된 적 기준)
        public int EnemyHP => SelectedEnemy?.HP ?? 0;
        public int EnemyMaxHP => SelectedEnemy?.MaxHP ?? 0;
        public int EnemyBlock => SelectedEnemy?.Block ?? 0;
        public int EnemyVulnerableTurns => SelectedEnemy?.VulnerableTurns ?? 0;

        public int HandCount => _deck?.HandCount ?? 0;

        public int PendingActionCount => _actionQueue?.PendingCount ?? 0;
        public bool IsActionQueueRunning => _actionRunner != null;
        public bool HasQueuedEndTurn => _actionQueue?.HasQueuedEndTurn ?? false;
        
        // ─────────────────────────────────────────────────
        // Enemy Query (슬롯 인덱스 기반)
        // ─────────────────────────────────────────────────
        /// <summary>
        /// 슬롯 인덱스로 적 생존 여부 확인
        /// </summary>
        public bool IsEnemyAlive(int slotIndex)
        {
            var enemy = _enemies?.GetBySlot(slotIndex);
            return enemy != null && enemy.IsAlive;
        }

        /// <summary>
        /// 슬롯 인덱스로 적 HP 조회
        /// </summary>
        public int GetEnemyHP(int slotIndex)
        {
            var enemy = _enemies?.GetBySlot(slotIndex);
            return enemy?.HP ?? 0;
        }

        /// <summary>
        /// 슬롯 인덱스로 적 MaxHP 조회
        /// </summary>
        public int GetEnemyMaxHP(int slotIndex)
        {
            var enemy = _enemies?.GetBySlot(slotIndex);
            return enemy?.MaxHP ?? 0;
        }

        // ─────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────
        
        private void Awake()
        { 
            EnsureSubsystemsReady();
        }
        
        private void Start()
        {
            if (RunSession.I == null || RunSession.I.State == null)
            {
                Debug.LogError("[Battle] RunSession missing. Start from Boot.");
                SceneManager.LoadScene(SceneRoutes.Boot);
                return;
            }

            EnsureSubsystemsReady();
            SetupBattle();
            EnsureEventsWired();

            if (animDirector == null)
                animDirector = FindObjectOfType<View.BattleAnimDirector>(true);
            if (hitPopups == null)
                hitPopups = FindObjectOfType<View.HitPopupSpawner>(true);
            if (targetManager == null)
                targetManager = FindObjectOfType<View.BattleTargetManager>(true);

            // 서약 선택(런 상태)을 배틀 애니메이션에 반영
            if (animDirector != null)
            {
                string oathId = null;
                var run = RunSession.I;
                if (run?.State != null && !string.IsNullOrWhiteSpace(run.State.oathId))
                    oathId = run.State.oathId;
                else if (run?.Oath != null && !string.IsNullOrWhiteSpace(run.Oath.id))
                    oathId = run.Oath.id;

                animDirector.ApplyOathAnimatorOverride(oathId);
            }

            // ✅ 초기 타겟 설정 (슬롯 인덱스 기반)
            if (SelectedEnemy != null)
            {
                int slotIdx = SelectedEnemy.SlotIndex;
                Debug.Log($"[BattleController] Initial target: {SelectedEnemy.name} at slot {slotIdx}");
                animDirector?.OnTargetChanged(slotIdx);
            }

            BeginPlayerTurn();
            NotifyStateChanged();

            if (autoWinForTest)
                EndBattle(true);
        }

        
        private void EnsureSubsystemsReady()
        {
            if (_subsystemsReady) return;
            _player = new BattlePlayerState();
            _enemies = new BattleEnemyManager();
            _actionQueue = new BattleActionQueue();
            _subsystemsReady = true;
        }
        
        private void InitializeSubsystems()
        {
            // legacy entrypoint (kept for compatibility)
            EnsureSubsystemsReady();
        }

        private void EnsureEventsWired()
        { 
            if (_eventsWired) return;
            WireEvents();
            _eventsWired = true;
        }
        
        private void WireEvents()
        {
            _enemies.SelectionChanged += () =>
            {
                var selected = _enemies.Selected;
                if (selected != null)
                {
                    // ✅ 슬롯 인덱스 사용
                    int slotIdx = selected.SlotIndex;
                    Debug.Log($"[BattleController] Selection changed to: {selected.name} at slot {slotIdx}");
                    animDirector?.OnTargetChanged(slotIdx);
                    SyncTargetSelection(selected);
                }
                NotifyStateChanged();
            };

            _enemies.EnemyDefeated += (deadEnemy) =>
            {
                if (_endingFlow) return;
                StartCoroutine(EnemyDefeatedFlowCo(deadEnemy));
            };

            _player.Died += () => EndBattle(false);

            _enemies.AllEnemiesDefeated += () => EndBattle(true);

            _actionQueue.OnExecuteCard += ExecuteQueuedCardCo;
            _actionQueue.OnExecuteEndTurn += EndTurnFlowCo;
            _actionQueue.QueueChanged += NotifyStateChanged;
        }

        private void OnEnemyDefeated(BattleEnemy enemy)
        {
            if (_enemies == null || enemy == null) return;
            _enemies.NotifyEnemyDefeated(enemy);
            NotifyStateChanged();
        }
    
        private void OnEnemyStatsChanged(BattleEnemy enemy)
        {
            // HP/Block/Vulnerable UI 갱신 트리거
            NotifyStateChanged();
        }
        
        private int GetFallbackEnemyDamage()
        {
            int dmg = 8;
            try
            {
                if (RunSession.I.PendingBattleType == MapNodeType.Elite)
                    dmg = 12;
            }
            catch { }
            return dmg;
        }
        
        private void SyncTargetSelection(BattleEnemy selected)
        {
            if (targetManager == null) return;
            targetManager.SetSelectedFromBattle(selected);
            targetManager.Refresh();
        }

        private IEnumerator EnemyDefeatedFlowCo(BattleEnemy deadEnemy)
        {
            // ✅ 슬롯 인덱스 사용
            int deadSlotIndex = deadEnemy.SlotIndex;
            Debug.Log($"[BattleController] Enemy defeated: {deadEnemy.name} at slot {deadSlotIndex}");

            // 1) 사망 애니메이션 재생 + 잠깐 노출 후 슬롯 비활성화
            if (animDirector != null && deadSlotIndex >= 0)
                yield return animDirector.PlayEnemyDieThenHideCo(deadSlotIndex, destroy: false);
            else
                yield return null;

            // 2) Disable 이후 한 번 더 "현재 선택"을 강제로 재동기화
            if (_endingFlow) yield break;

            var nextSelected = _enemies.Selected;
            if (nextSelected != null)
            {
                int slotIdx = nextSelected.SlotIndex;
                animDirector?.OnTargetChanged(slotIdx);
                SyncTargetSelection(nextSelected);
            }
        }

        private void SetupBattle()
        {
            var run = RunSession.I;

            _player.Initialize(run);
            // 적 초기화는 이제 각 BattleEnemy가 스스로 등록함
            _deck = new DeckRuntime(run.State.deck);
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        // ─────────────────────────────────────────────────
        // Enemy Management (객체 참조 기반)
        // ─────────────────────────────────────────────────
        /// <summary>
        /// BattleEnemy 등록 (씬의 적 오브젝트가 호출)
        /// </summary>
        public void RegisterEnemy(BattleEnemy enemy)
        {
            if (enemy == null) return;
            
            EnsureSubsystemsReady();

            // RunSession 기반으로 초기화
            enemy.SetBattleController(this);
            
            // 이벤트 중복 방지
            enemy.OnDefeated -= OnEnemyDefeated;
            enemy.OnDefeated += OnEnemyDefeated;
            enemy.OnStatsChanged -= OnEnemyStatsChanged;
            enemy.OnStatsChanged += OnEnemyStatsChanged;
            
            // ✅ SlotIndex 기반으로 초기화
            int slotIndex = enemy.SlotIndex;
            enemy.Initialize(RunSession.I, slotIndex);

            _enemies.Register(enemy);
            
            Debug.Log($"[BattleController] Registered: {enemy.name} (SlotIndex={slotIndex})");
            
            NotifyStateChanged();
        }

        /// <summary>
        /// BattleEnemy 등록 해제
        /// </summary>
        public void UnregisterEnemy(BattleEnemy enemy)
        {
            EnsureSubsystemsReady();
            if (enemy == null) return;
            
            enemy.OnDefeated -= OnEnemyDefeated;
            enemy.OnStatsChanged -= OnEnemyStatsChanged;
            
            _enemies.Unregister(enemy);
            NotifyStateChanged();
        }

        /// <summary>
        /// 특정 적 선택 (객체 참조)
        /// </summary>
        public bool SelectEnemy(BattleEnemy enemy)
        {
            bool changed = _enemies.Select(enemy);
            if (changed) NotifyStateChanged();
            return changed;
        }

        /// <summary>
        /// 슬롯 인덱스로 선택
        /// </summary>
        public bool SelectEnemy(int slotIndex)
        {
            bool changed = _enemies.SelectBySlot(slotIndex);
            if (changed) NotifyStateChanged();
            return changed;
        }

        /// <summary>
        /// 하위 호환용 - 더 이상 적 수를 강제하지 않음
        /// </summary>
        [Obsolete("Use RegisterEnemy instead")]
        public void EnsureEnemyCount(int count)
        {
            // 이제 각 BattleEnemy가 스스로 등록하므로 이 메서드는 no-op
            Debug.LogWarning("[BattleController] EnsureEnemyCount is deprecated. Enemies register themselves.");
        }

        // ─────────────────────────────────────────────────
        // Card Access
        // ─────────────────────────────────────────────────
        public CardDefinition GetHandCard(int index) => _deck?.PeekHand(index);

        // ─────────────────────────────────────────────────
        // Card Play
        // ─────────────────────────────────────────────────
        public bool TryPlayCardAt(int handIndex)
        {
            if (_endingFlow || !_isPlayerTurn) return false;
            if (_deck == null || _actionQueue == null) return false;
            if (_actionQueue.HasQueuedEndTurn) return false;
            if (handIndex < 0 || handIndex >= _deck.HandCount) return false;

            var card = _deck.PeekHand(handIndex);
            if (card == null) return false;

            // ✅ 슬롯 인덱스 사용
            bool enqueued = _actionQueue.EnqueueCard(
                card,
                handIndex,
                SelectedEnemyIndex,  // 슬롯 인덱스
                _player.Energy
            );

            if (!enqueued) return false;

            EnsureActionRunner();
            return true;
        }

        // ─────────────────────────────────────────────────
        // End Turn
        // ─────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────
        // Action Queue Runner
        // ─────────────────────────────────────────────────
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

            // 현재 손패에서 카드 위치 찾기
            int handIndex = FindCurrentHandIndex(action);
            if (handIndex < 0) yield break;

            if (!_player.HasEnergy(action.Card.cost))
            {
                Debug.Log("[Battle] Not enough energy (exec).");
                yield break;
            }

            // ✅ 타겟 결정 (슬롯 인덱스 기반)
            BattleEnemy target = ResolveTarget(action.TargetIndex);
            int targetSlotIndex = target != null ? target.SlotIndex : 0;

            // 1) 에너지 소비
            _player.TrySpendEnergy(action.Card.cost);

            // 2) 애니메이션 (슬롯 인덱스 사용)
            if (animDirector != null)
            {
                if (action.Card.effectKind == CardEffectKind.Attack)
                    StartCoroutine(animDirector.PlayPlayerAttackComboCo(action.Card, targetSlotIndex));
                else
                    StartCoroutine(animDirector.PlayPlayerCardCo(action.Card, targetSlotIndex));
            }

            // 3) 효과 적용
            ApplyCardEffect(action.Card, target);

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

            // 6) 개선된 자동 복귀 로직
            if (ShouldAutoReturnToBase())
            {
                StartCoroutine(animDirector.ReturnToBaseCo(false));
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

        /// <summary>
        /// ✅ 슬롯 인덱스 기반 타겟 결정
        /// </summary>
        private BattleEnemy ResolveTarget(int requestedSlotIndex)
        {
            if (_enemies == null || _enemies.Count == 0) return null;

            // 요청된 슬롯의 적이 살아있는지 확인
            var requested = _enemies.GetBySlot(requestedSlotIndex);
            if (requested != null && requested.IsAlive)
                return requested;

            // 아니면 현재 선택된 적 반환
            var selected = _enemies.Selected;
            if (selected != null && selected.IsAlive)
                return selected;

            // 살아있는 첫 번째 적
            return _enemies.FindFirstAlive();
        }

        /// <summary>
        /// 개선된 자동 복귀 조건 체크
        /// </summary>
        private bool ShouldAutoReturnToBase()
        {
            if (_endingFlow || !_isPlayerTurn) return false;
            if (_player == null || _player.Energy > 0) return false;
            if (_actionQueue.HasQueuedEndTurn) return false;
            if (_actionQueue.PendingCount > 0) return false;
            if (animDirector == null) return false;
            if (!animDirector.IsMelee) return false;

            // 공격 중이면 복귀하지 않음
            if (animDirector.IsAttacking) return false;

            return true;
        }

        // ─────────────────────────────────────────────────
        // Card Effects (객체 참조 기반)
        // ─────────────────────────────────────────────────
        private void ApplyCardEffect(CardDefinition card, BattleEnemy target)
        {
            switch (card.effectKind)
            {
                case CardEffectKind.Attack:
                    if (target != null)
                    {
                        int hpLoss = target.TakeDamage(card.value);
                        if (hitPopups != null && hpLoss > 0)
                        {
                            // ✅ 슬롯 인덱스 사용
                            int slotIdx = target.SlotIndex;
                            hitPopups.SpawnEnemy(hpLoss, slotIdx);
                        }
                    }
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
                    if (target != null)
                        target.ApplyVulnerable(card.value);
                    break;

                case CardEffectKind.Heal:
                    _player.Heal(card.value);
                    break;
            }

            NotifyStateChanged();
        }

        // ─────────────────────────────────────────────────
        // Turn Flow
        // ─────────────────────────────────────────────────
        private void BeginPlayerTurn()
        {
            _player.BeginTurn(RunSession.I);
            _deck.Draw(_player.DrawPerTurn);
            _isPlayerTurn = true;
            _resolving = false;
            
            // Enemy intent preview (Pattern Peek)
            _enemies.RefreshIntentAll(GetFallbackEnemyDamage());

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
            int fallbackDamage = GetFallbackEnemyDamage();

            foreach (var enemy in _enemies.All)
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                // ✅ 슬롯 인덱스 사용
                int slotIndex = enemy.SlotIndex;
                int damage = enemy.PlannedDamage > 0 ? enemy.PlannedDamage : fallbackDamage;

                if (animDirector != null)
                    yield return animDirector.PlayEnemyAttackCo(slotIndex);

                animDirector?.PlayPlayerHitFx();

                int hpLoss = _player.TakeDamage(damage);
                if (hitPopups != null && hpLoss > 0)
                    hitPopups.SpawnPlayer(hpLoss);

                NotifyStateChanged();

                if (!_player.IsAlive)
                {
                    EndBattle(false);
                    yield break;
                }
                
                enemy.AdvancePatternStep(fallbackDamage);
            }
        }

        // ─────────────────────────────────────────────────
        // Battle End
        // ─────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────
        // Debug Helpers
        // ─────────────────────────────────────────────────
        public void DebugPlayFirstCard() => TryPlayCardAt(0);
        public void DebugEndTurn() => EndTurn();
    }
}
