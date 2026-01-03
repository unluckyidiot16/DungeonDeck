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
using DungeonDeck.Battle.Combat;
using DungeonDeck.Battle.View;
using DungeonDeck.Battle;
using DungeonDeck.Config.Enemies;


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
        
        [Header("Combatants")]
        [SerializeField] private BattlePlayer player; // ✅ 플레이어도 유닛으로 분리
            
        private DungeonDeck.Battle.View.BattleTargetManager _targetManager;

        // ─────────────────────────────────────────────────
        // Sub-systems
        // ─────────────────────────────────────────────────
        private BattlePlayerState _player;
        private BattleEnemyManager _enemies;
        private BattleActionQueue _actionQueue;
        private DeckRuntime _deck;
        private ICombatant _playerCombatant;

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
        public int PlayerVulnerableTurns => _player?.VulnerableTurns ?? 0;

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

        // BattleController.cs 내부 필드 추가
        private CardDefinition _previewCard;

        // BattleController.cs 내부 메서드 추가
        public void PreviewCardTargeting(CardDefinition card)
        {
            _previewCard = card;
            if (targetManager != null)
                targetManager.ApplyCardTargeting(card);

            // ✅ 타겟 요구 카드인데 현재 선택이 죽어있으면, 살아있는 적으로 자동 보정
            if (card != null && card.HasAnySingleEnemyTargetEffect())
            {
                var alive = ResolveTarget(SelectedEnemyIndex);
                if (alive != null && alive.IsAlive)
                    SelectEnemy(alive);
            }
        }

        public void ClearCardTargetPreview(CardDefinition card = null)
        {
            if (card != null && card != _previewCard) return;
            _previewCard = null;

            if (targetManager != null)
                targetManager.ApplyCardTargeting(null);
        }

        
        // ─────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────
        
        private void Awake()
        { 
            EnsureSubsystemsReady();
            if (player == null) player = FindObjectOfType<BattlePlayer>(true);
            if (targetManager == null) targetManager = FindObjectOfType<View.BattleTargetManager>(true);
            
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

        // ─────────────────────────────────────────────
        // ✅ 공통 전투 API (BattleEnemy/BattlePlayer 분기 제거)
        // ─────────────────────────────────────────────
        private ICombatant ResolveSelectedEnemy()
        { 
            if (targetManager == null) return null;
            return targetManager.SelectedEnemy; // BattleEnemy == ICombatant
        }
    
        private ICombatant ResolvePlayer()
        { 
            return player;
        }
    
        // "타겟이 플레이어냐/적이냐" 분기는 여기 1곳으로 몰아넣기
        private ICombatant ResolveTarget(bool targetIsPlayer)
        {
            return targetIsPlayer ? ResolvePlayer() : ResolveSelectedEnemy();
        }
    
        private int DealDamage(ICombatant target, int rawAmount)
        {
            if (target == null) return 0;
            return target.TakeDamage(rawAmount);
        }
    
        private void HealTarget(ICombatant target, int amount)
        {
            if (target == null) return;
            target.Heal(amount);
        }
    
        private void AddBlockTarget(ICombatant target, int amount)
        { 
            if (target == null) return;
            target.AddBlock(amount);
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
            SyncPlayerViewCombatant(); // ✅ 초기 HUD 동기화
            // 적 초기화는 이제 각 BattleEnemy가 스스로 등록함
            _deck = new DeckRuntime(run.State.deck);
        }

        private void NotifyStateChanged()
        {
            SyncPlayerViewCombatant(); // ✅ BattlePlayerState -> BattlePlayer 값 미러링
            StateChanged?.Invoke();
        }
        
        // ─────────────────────────────────────────────────
        // Player Combatant Adapter (BattlePlayerState -> ICombatant)
        // ─────────────────────────────────────────────────
        /// <summary>
        /// BattlePlayerState를 ICombatant로 노출하기 위한 최소 어댑터.
        /// (플레이어 스탯 권위는 BattlePlayerState에 유지)
        /// </summary>
        private sealed class PlayerCombatantAdapter : ICombatant
        {
            private readonly BattleController _c;

            public PlayerCombatantAdapter(BattleController controller)
            {
                _c = controller;
            }

            // 이 어댑터는 "카드 효과 적용" 용도라 이벤트는 당장 사용하지 않음(필요 시 확장)
            public event Action<ICombatant> OnStatsChanged { add { } remove { } }
            public event Action<ICombatant> OnDefeated { add { } remove { } }

            public int SlotIndex => -1;
            public int HP => _c.PlayerHP;
            public int MaxHP => _c.PlayerMaxHP;
            public int Block => _c.PlayerBlock; 
                
            // ✅ 현재 BattlePlayerState는 Vulnerable을 관리하지 않음.
            // (플레이어 디버프를 넣고 싶으면 BattlePlayerState에 정식 필드/로직 추가하면서 데미지 공식까지 반영하는 게 좋음)
            public int VulnerableTurns => _c._player != null ? _c._player.VulnerableTurns : 0;
            
            public bool IsAlive => HP > 0;

            public Transform PopupTarget => _c.player != null ? _c.player.transform : _c.transform;
            public int PopupSlotIndex => -1;

            public int TakeDamage(int rawAmount)
            {
                if (_c._player == null) return 0;
                return _c._player.TakeDamage(rawAmount);
            }

            public void Heal(int amount)
            {
                if (_c._player == null) return;
                _c._player.Heal(amount);
            }

            public void AddBlock(int amount)
            {
                if (_c._player == null) return;
                _c._player.GainBlock(amount);
            }

            public void ApplyVulnerable(int turns)
            {
                if (_c._player == null) return;
                _c._player.ApplyVulnerable(turns);
            }
        
           public void TickVulnerable()
           {
               if (_c._player == null) return;
               _c._player.TickVulnerable();
            }
        }

        
        
        /// <summary>
        /// BattlePlayerState(_player)에서 관리되는 HP/Block을
        /// BattlePlayer(모노비)로 미러링하여 PlayerHUDView 이벤트 갱신을 살립니다.
        /// </summary>
        private void SyncPlayerViewCombatant()
        {
            if (player == null) return;
            int maxHp = PlayerMaxHP;
            if (maxHp <= 0) return;
            player.ResetWithValues(maxHp, PlayerHP, PlayerBlock, PlayerVulnerableTurns);
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

            // ✅ 타겟 결정 (effectKind 기반: 적/플레이어 공통)
            ICombatant target = ResolveCardTargetCombatant(action.Card, action.TargetIndex);
            
            // AnimDirector는 "적 슬롯" 개념을 쓰므로, 플레이어 타겟(-1)인 경우 현재 선택 슬롯로 fallback
            int targetSlotIndex = (target != null && target.PopupSlotIndex >= 0)
                ? target.PopupSlotIndex
                : SelectedEnemyIndex;

            // 1) 에너지 소비
            _player.TrySpendEnergy(action.Card.cost);

            // 2) 애니메이션 (슬롯 인덱스 사용)
            if (animDirector != null)
            {
                if (action.Card.HasEffect(CardEffectKind.Attack))
                    StartCoroutine(animDirector.PlayPlayerAttackComboCo(action.Card, targetSlotIndex));
                else
                    StartCoroutine(animDirector.PlayPlayerCardCo(action.Card, targetSlotIndex));
            }

            // 3) 효과 적용
            ApplyCardEffect(action.Card, action.TargetIndex);

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
        /// 카드의 effectKind 기준으로 실제 적용 대상(ICombatant)을 결정합니다.
        /// - 공격/디버프: 적(요청 슬롯/선택/첫 생존)
        /// - 그 외(블록/힐/자원): 플레이어(런타임 state)
        /// </summary>
        // BattleController.cs 내부 메서드 교체
        private ICombatant ResolveCardTargetCombatant(CardDefinition card, int requestedEnemySlotIndex)
        {
            if (card == null) return null;

            // ✅ v2(Multi Effects) 기준: 적 타겟 효과가 하나라도 있으면 적을 대표 타겟으로
            bool hasEnemyTargetEffect = false;

            foreach (var e in card.EnumerateEffects())
            {
                if (e.value == 0) continue;

                // BattleController 안에 이미 있는 ResolveEffectTarget() 사용 (Auto 처리)
                var t = ResolveEffectTarget(e.target, e.kind);
                if (t == CardEffectTarget.Enemy || t == CardEffectTarget.AllEnemies)
                {
                    hasEnemyTargetEffect = true;
                    break;
                }
            }

            if (hasEnemyTargetEffect)
                return ResolveTarget(requestedEnemySlotIndex);

            // ✅ 플레이어는 BattlePlayerState 권위 → 어댑터로 노출
            return _playerCombatant ?? (_playerCombatant = new PlayerCombatantAdapter(this));
        }

    
        private void SpawnDamagePopup(ICombatant target, int amount)
        {
            if (hitPopups == null) return;
            if (target == null) return;
            if (amount <= 0) return;
        
            int slot = target.PopupSlotIndex;
            if (slot >= 0) hitPopups.SpawnEnemy(amount, slot);
            else hitPopups.SpawnPlayer(amount);
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
        private void ApplyCardEffect(CardDefinition card, int requestedEnemySlotIndex)
        {
            if (card == null) return;
            // ✅ 단일 적 타겟(필요 시) 미리 구해두기
            BattleEnemy singleEnemy = ResolveTarget(requestedEnemySlotIndex);

            // ✅ 플레이어 타겟(권위는 BattlePlayerState이므로 어댑터 사용)
            var self = _playerCombatant ?? (_playerCombatant = new PlayerCombatantAdapter(this));

            // ✅ 데미지 팝업은 타겟별로 합산해서 “한 번씩만”
            Dictionary<int, int> dmgByEnemySlot = null; // slot -> dmg
            int playerHpLoss = 0;

            foreach (var e in card.EnumerateEffects())
            {
                if (e.value == 0) continue;
                int repeat = Mathf.Max(1, e.repeat);

                // 1) 타겟 해석(Auto 포함)
                var resolvedTarget = ResolveEffectTarget(e.target, e.kind);

                // 2) 실제 대상 열거
                IEnumerable<ICombatant> targets = EnumerateTargets(resolvedTarget, self, singleEnemy);
                if (targets == null) continue;

                foreach (var t in targets)
                {
                    if (t == null || !t.IsAlive) continue;

                    switch (e.kind)
                    {
                        case CardEffectKind.Attack:
                        {
                            int sum = 0;
                            for (int i = 0; i < repeat; i++)
                                sum += t.TakeDamage(e.value);

                            int slot = t.PopupSlotIndex;
                            if (sum > 0)
                            {
                                if (slot >= 0)
                                {
                                    dmgByEnemySlot ??= new Dictionary<int, int>();
                                    dmgByEnemySlot[slot] = (dmgByEnemySlot.TryGetValue(slot, out var cur) ? cur : 0) + sum;
                                }
                                else
                                {
                                    playerHpLoss += sum;
                                }
                            }
                            break;
                        }

                        case CardEffectKind.ApplyVulnerable:
                            for (int i = 0; i < repeat; i++)
                                t.ApplyVulnerable(e.value);
                            break;

                        case CardEffectKind.Block:
                            for (int i = 0; i < repeat; i++)
                                t.AddBlock(e.value);
                            break;

                        case CardEffectKind.Heal:
                            for (int i = 0; i < repeat; i++)
                                t.Heal(e.value);
                            break;

                        case CardEffectKind.Draw:
                            // ✅ Draw/Energy는 "Self 전용" 권장 (타겟이 Self가 아니면 무시)
                            if (ReferenceEquals(t, self))
                                _deck.Draw(e.value * repeat);
                            break;

                        case CardEffectKind.GainEnergy:
                            if (ReferenceEquals(t, self))
                                _player.GainEnergy(e.value * repeat);
                            break;
                    }
                }
            }

            // ✅ 팝업 스폰(타겟별 1회)
            if (hitPopups != null)
            {
                if (dmgByEnemySlot != null)
                {
                    foreach (var kv in dmgByEnemySlot)
                        if (kv.Value > 0) hitPopups.SpawnEnemy(kv.Value, kv.Key);
                }
                if (playerHpLoss > 0) hitPopups.SpawnPlayer(playerHpLoss);
            }

            NotifyStateChanged();
         }

        private static CardEffectTarget ResolveEffectTarget(CardEffectTarget t, CardEffectKind kind)
        {
            if (t != CardEffectTarget.Auto) return t;
            switch (kind)
            {
                case CardEffectKind.Attack:
                case CardEffectKind.ApplyVulnerable:
                    return CardEffectTarget.Enemy;
                default:
                    return CardEffectTarget.Self;
            }
        }

        private IEnumerable<ICombatant> EnumerateTargets(CardEffectTarget t, ICombatant self, BattleEnemy singleEnemy)
        {
            switch (t)
            {
                case CardEffectTarget.Self:
                    yield return self;
                    yield break;

                case CardEffectTarget.Enemy:
                    if (singleEnemy != null && singleEnemy.IsAlive) yield return singleEnemy;
                    yield break;

                case CardEffectTarget.AllEnemies:
                    if (_enemies == null) yield break;
                    foreach (var e in _enemies.All)
                        if (e != null && e.IsAlive) yield return e;
                    yield break;
            }
        } 

        // ─────────────────────────────────────────────────
        // Turn Flow
        // ─────────────────────────────────────────────────
        private void BeginPlayerTurn()
        {
            _player.TickVulnerable(); // ✅ 플레이어 취약 턴 감소(턴 시작)
            _player.BeginTurn(RunSession.I);
            _deck.Draw(_player.DrawPerTurn);
            _isPlayerTurn = true;
            _resolving = false;
            
            // Enemy intent preview (Pattern Peek)
            _enemies.RefreshIntentAll(GetFallbackEnemyDamage());

            ClearCardTargetPreview();
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

            int slotIndex = enemy.SlotIndex;
            
            // ✅ 현재 패턴 Step 가져오기
            var pattern = enemy.Pattern;
            EnemyPatternDefinition.Step step = default;
            bool hasPattern = false;
            
            if (pattern != null && pattern.StepCount > 0)
            {
                step = pattern.GetStep(enemy.PatternStepIndex);
                hasPattern = true;
            }
            
            // ✅ 의도에 따른 분기
            if (!hasPattern || step.IsAttackIntent)
            {
                // 공격 의도 (기본 포함)
                yield return ExecuteEnemyAttack(enemy, slotIndex, fallbackDamage, step);
            }
            else if (step.IsDefendIntent)
            {
                // 방어 의도
                yield return ExecuteEnemyDefend(enemy, slotIndex, step);
            }
            else if (step.IsDebuffOnlyIntent)
            {
                // 디버프만 의도 (공격 없이 취약만 부여)
                yield return ExecuteEnemyDebuffOnly(enemy, slotIndex, step);
            }
            else
            {
                // 기타 의도 (버프 등) - 현재는 아무것도 안 함
                Debug.Log($"[Battle] Enemy {enemy.name} performs non-attack action: {step.intentId}");
                yield return new WaitForSeconds(0.3f);
            }

            NotifyStateChanged();

            if (!_player.IsAlive)
            {
                EndBattle(false);
                yield break;
            }
            
            // ✅ 패턴 Step 진행
            enemy.AdvancePatternStep(fallbackDamage);
        }
    }

/// <summary>
/// 적 공격 실행
/// </summary>
private IEnumerator ExecuteEnemyAttack(BattleEnemy enemy, int slotIndex, int fallbackDamage, EnemyPatternDefinition.Step step)
{
    int damage = enemy.PlannedDamage > 0 ? enemy.PlannedDamage : fallbackDamage;

    Debug.Log($"[Battle] Enemy {enemy.name} attacks for {damage} damage (ATK={enemy.ATK}, multiplier={step.damageMultiplier})");

    // 공격 애니메이션
    if (animDirector != null)
        yield return animDirector.PlayEnemyAttackCo(slotIndex);

    animDirector?.PlayPlayerHitFx();

    // 데미지 적용
    int hpLoss = _player.TakeDamage(damage);
    if (hitPopups != null && hpLoss > 0)
        hitPopups.SpawnPlayer(hpLoss);
    
    // ✅ (추가) 공격 스텝에 blockAmount가 같이 있으면 즉시 획득
    if (step.blockAmount > 0)
        enemy.AddBlock(step.blockAmount);
    
    // 취약 부여 (공격과 함께)
    int vulnTurns = enemy.PlannedVulnerableToPlayerTurns;
    if (_player.IsAlive && vulnTurns > 0)
    {
        _player.ApplyVulnerable(vulnTurns);
        Debug.Log($"[Battle] Enemy {enemy.name} applies {vulnTurns} Vulnerable to player");
    }
}

/// <summary>
/// 적 방어 실행 (블록 획득)
/// </summary>
private IEnumerator ExecuteEnemyDefend(BattleEnemy enemy, int slotIndex, EnemyPatternDefinition.Step step)
{
    int blockAmount = step.blockAmount > 0 ? step.blockAmount : 5; // 기본 블록량
    
    Debug.Log($"[Battle] Enemy {enemy.name} defends, gaining {blockAmount} block");

    // 방어 애니메이션 (Focus 트리거 사용)
    if (animDirector != null)
    {
        var animator = animDirector.GetEnemyAnimator(slotIndex);
        if (animator != null)
        {
            animator.SetTrigger("Focus");
        }
    }
    
    yield return new WaitForSeconds(0.4f);
    
    enemy.AddBlock(blockAmount);
}

/// <summary>
/// 적 디버프만 실행 (공격 없이 취약만 부여)
/// </summary>
private IEnumerator ExecuteEnemyDebuffOnly(BattleEnemy enemy, int slotIndex, EnemyPatternDefinition.Step step)
{
    int vulnTurns = step.applyVulnerableToPlayerTurns;
    
    Debug.Log($"[Battle] Enemy {enemy.name} applies debuff: {vulnTurns} Vulnerable to player");

    // 디버프 애니메이션
    if (animDirector != null)
    {
        var animator = animDirector.GetEnemyAnimator(slotIndex);
        if (animator != null)
        {
            animator.SetTrigger("Focus");
        }
    }
    
    yield return new WaitForSeconds(0.4f);
    
    if (_player.IsAlive && vulnTurns > 0)
    {
        _player.ApplyVulnerable(vulnTurns);
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
