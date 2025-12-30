// Assets/_Project/Scripts/Battle/View/BattleAnimDirector.cs (Refactored v4)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DG.Tweening;
using DungeonDeck.Config.Cards;
using DungeonDeck.Debugging;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 전투 애니메이션 감독 (v4 - 프로파일 기반)
    /// 
    /// v4 변경사항:
    /// - ApproachAnimProfile / AttackAnimProfile 사용
    /// - 공격 애니메이션 완료 대기 로직 추가
    /// - 타겟 변경 시 리포지션 타이밍 개선
    /// - AttackAnimType 지원
    /// </summary>
    public class BattleAnimDirector : MonoBehaviour
    {
        [Serializable]
        public class OathAnimatorOverride
        { 
            public string oathId; 
            public AnimatorOverrideController overrideController;
        }
        
        [Header("Enemy Death Hide")]
        [Tooltip("Die 트리거 후, 이 시간만큼 보여준 뒤 Disable/Destroy 합니다.")]
        public float enemyDieHideDelay = 0.6f;
            
        /// <summary>
        /// 적 사망 트리거 → 잠깐 대기 → Disable(기본) 또는 Destroy.
        /// </summary>
        public IEnumerator PlayEnemyDieThenHideCo(int enemyIndex, bool destroy = false)
        {
            // Die 트리거는 기존 로직(있다면) 그대로 사용
            PlayEnemyDieFx(enemyIndex);
            
            float wait = Mathf.Max(0.05f, enemyDieHideDelay);
            yield return new WaitForSeconds(wait);
            
            if (enemyViews == null || enemyIndex < 0 || enemyIndex >= enemyViews.Length) yield break;
            var v = enemyViews[enemyIndex];
            if (v == null) yield break;
            
            // 배열 인덱스 안정성을 위해 기본은 Disable 추천
            if (destroy) Destroy(v.gameObject);
            else v.gameObject.SetActive(false);
        }
        
        // ─────────────────────────────────────────
        // Inspector References
        // ─────────────────────────────────────────
        [Header("Player")]
        public Transform playerView;
        public Animator playerAnimator;

        [Header("Oath Stance (Idle Override)")]
        [Tooltip("RunSession.State.oathId -> AnimatorOverrideController 매핑. Idle만 교체하는 용도로 사용하세요.")]
        public List<OathAnimatorOverride> oathOverrides = new();
        
        // Apply가 먼저 호출되었는데 animator가 아직 없을 때를 대비
        private string _pendingOathId = null;
            
        private RuntimeAnimatorController _playerBaseController;
        
        [Header("Enemies")]
        public Transform[] enemyViews = new Transform[3];
        public Animator[] enemyAnimators = new Animator[3];

        [Header("Target Manager")]
        public BattleTargetManager targetManager;

        // ─────────────────────────────────────────
        // Inspector Settings - Approach Bool Parameters
        // ─────────────────────────────────────────
        [Header("Player Bool Parameters (접근 애니메이션)")]
        public string playerRunBool = "Run";
        public string playerDashBool = "Dash";
        public string playerSprintBool = "Sprint";
        public string playerSlideBool = "Slide";
        public string playerRunBlockingBool = "RunBlocking";

        // ─────────────────────────────────────────
        // Inspector Settings - Attack
        // ─────────────────────────────────────────
        [Header("Player Attack")]
        [Tooltip("공격 진입 트리거. Animator에서 AttackHub(모션 없는 허브)로 연결하는 용도로 사용하세요.")]
        public string playerAttackTrigger = "Attack";
        
        [Tooltip("AttackHub에서 실제 공격 모션으로 분기할 Int 파라미터 이름. 값은 (int)AttackAnimType 입니다.")]
        public string playerAttackTypeInt = "AttackType";
        
        [Header("Player Other Triggers")]
        public string playerBlockTrigger = "Block";
        public string playerCastTrigger = "Cast";
        public string playerHitTrigger = "Hit";
        public string playerBackDashTrigger = "BackDash";

        [Header("Enemy Triggers")]
        public string enemyAttackTrigger = "Attack";
        public string enemyFocusTrigger = "Focus";
        
        [Tooltip("적 사망 트리거. Enemy Animator에 이 이름의 Trigger 파라미터가 있어야 합니다.")]
        public string enemyDieTrigger = "Die";
            
        [Tooltip("사망 트리거 후 최소 노출 시간(다른 곳에서 즉시 Disable/Destroy 되는 경우 대비용).")]
        public float enemyDieMinShowTime = 0.35f;
        

        // ─────────────────────────────────────────
        // Inspector Settings - Movement (기본값, 프로파일 없을 때 사용)
        // ─────────────────────────────────────────
        [Header("Approach (Fallback)")]
        public float approachStopDistance = 1f;
        public float approachSpeed = 4.0f;
        public float approachMinDuration = 0.18f;
        public float approachMaxDuration = 0.55f;
        public Ease approachMoveEase = Ease.InOutSine;

        [Header("Return")]
        public float returnSpeed = 6.0f;
        public float returnMinDuration = 0.10f;
        public float returnMaxDuration = 0.35f;
        public Ease returnMoveEase = Ease.OutQuad;

        [Header("Combat FX (Fallback)")]
        public float comboPunchX = 0.10f;
        public float comboPunchScale = 0.07f;
        public float comboPunchTime = 0.10f;
        public float playerHitShakeDuration = 0.12f;
        public float playerHitShakeStrength = 0.10f;

        [Header("Enemy Lunge")]
        public float lungeDistance = 0.25f;
        public float lungeOutTime = 0.10f;
        public float lungeBackTime = 0.12f;

        [Header("Focus")]
        public float focusMove = 0.25f;
        public float focusScale = 0.06f;
        public float focusTime = 0.12f;

        // ─────────────────────────────────────────
        // Sub-systems
        // ─────────────────────────────────────────
        private PlayerAnimFSM _fsm;
        private PlayerMover _mover;
        private Coroutine _runner;

        // Cached
        private readonly Vector3[] _enemyBasePos = new Vector3[3];
        private readonly Vector3[] _enemyBaseScale = new Vector3[3];
        private float _lastAttackTriggerTime = -999f;
        private float _lastAttackDuration = 0f;

        // 현재 활성화된 접근 애니메이션 타입 추적
        private ApproachAnimType _currentApproachType = ApproachAnimType.None;

        // ─────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────
        public bool IsMelee => _fsm?.IsMelee ?? false;
        public int MeleeTargetIndex => _fsm?.MeleeTargetIndex ?? -1;
        public ApproachAnimType CurrentApproachType => _currentApproachType;
        public bool IsAttacking => _fsm?.CurrentState == PlayerAnimFSM.State.Attacking;

        // ─────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────
        private void Awake()
        {
            _fsm = new PlayerAnimFSM();
            _mover = new PlayerMover();

            ConfigureMover();
        }

        private void CachePlayerBaseControllerIfNeeded()
        {
            if (_playerBaseController != null) return;
            if (playerAnimator == null) return;

            var cur = playerAnimator.runtimeAnimatorController;
            if (cur is AnimatorOverrideController aoc && aoc.runtimeAnimatorController != null)
                _playerBaseController = aoc.runtimeAnimatorController;
            else
                _playerBaseController = cur;
        }

        /// <summary>
        /// 서약(oathId)에 맞는 AnimatorOverrideController를 적용합니다.
        /// (Idle만 교체하는 스탠스 연출에 사용)
        /// </summary>
        public void ApplyOathAnimatorOverride(string oathId)
        {
            Debug.Log($"[BattleAnimDirector] ApplyOathAnimatorOverride called. oathId=\"{oathId}\"");
            
            if (playerAnimator == null)
            {
                Debug.LogWarning($"[BattleAnimDirector] playerAnimator is NULL. Storing pending oathId=\"{oathId}\"");
                _pendingOathId = oathId;   // ✅ animator가 아직 없으면 보류
                return;
            }
            _pendingOathId = null;         // ✅ 적용 가능해졌으니 pending 해제

            CachePlayerBaseControllerIfNeeded();
            if (_playerBaseController == null)
            {
                Debug.LogError($"[BattleAnimDirector] _playerBaseController is NULL after cache attempt!");
                return;
            }
            
            Debug.Log($"[BattleAnimDirector] Base controller cached: \"{_playerBaseController.name}\"");
            Debug.Log($"[BattleAnimDirector] oathOverrides count: {(oathOverrides != null ? oathOverrides.Count : 0)}");

            AnimatorOverrideController chosen = null;
            if (!string.IsNullOrEmpty(oathId) && oathOverrides != null)
            {
                for (int i = 0; i < oathOverrides.Count; i++)
                {
                    var e = oathOverrides[i];
                    if (e == null) continue;
                    
                    Debug.Log($"[BattleAnimDirector]   [{i}] oathId=\"{e.oathId}\", controller={(e.overrideController != null ? e.overrideController.name : "NULL")}");
                    
                    if (string.Equals(e.oathId, oathId, StringComparison.OrdinalIgnoreCase))
                    {
                        chosen = e.overrideController;
                        Debug.Log($"[BattleAnimDirector] ✓ MATCHED! Using override: \"{chosen?.name}\"");
                        break;
                    }
                }
            }

            // 매칭이 없으면 base로 복귀
            if (chosen == null)
            {
                Debug.LogWarning($"[BattleAnimDirector] No matching override for oathId=\"{oathId}\". Reverting to base.");
                if (playerAnimator.runtimeAnimatorController != _playerBaseController)
                {
                    playerAnimator.runtimeAnimatorController = _playerBaseController;
                    playerAnimator.Rebind();
                    playerAnimator.Update(0f);
                }
                return;
            }

            // 베이스 컨트롤러가 다르면 경고 (애니 파라미터 구조가 어긋날 수 있음)
            if (chosen.runtimeAnimatorController != _playerBaseController)
            {
                Debug.LogWarning($"[BattleAnimDirector] Oath override base mismatch. oathId={oathId}, " +
                                 $"overrideBase={(chosen.runtimeAnimatorController != null ? chosen.runtimeAnimatorController.name : "null")}, " +
                                 $"expectedBase={_playerBaseController.name}");
            }

            if (playerAnimator.runtimeAnimatorController != chosen)
            {
                Debug.Log($"[BattleAnimDirector] ✓ Applying override controller: \"{chosen.name}\"");
                playerAnimator.runtimeAnimatorController = chosen;
                playerAnimator.Rebind();
                playerAnimator.Update(0f);
                Debug.Log($"[BattleAnimDirector] ✓ Override applied successfully!");
            }
            else
            {
                Debug.Log($"[BattleAnimDirector] Override already applied (same controller).");
            }
        }
        
        private void OnDisable()
        {
            StopRunner();
            _fsm?.Reset();
            _mover?.ResetToBase();

            ClearAllApproachBools();

            if (playerView != null)
            {
                playerView.DOKill(true);
                var actorView = playerView.GetComponent<BattleActorView>();
                if (actorView != null) actorView.drivenByDirector = false;
            }
        }

        private void ConfigureMover()
        {
            _mover.ApproachProfile = new MoveProfile
            {
                useSpeedBased = true,
                speed = approachSpeed,
                minDuration = approachMinDuration,
                maxDuration = approachMaxDuration,
                moveEase = approachMoveEase
            };

            _mover.ReturnProfile = new MoveProfile
            {
                useSpeedBased = true,
                speed = returnSpeed,
                minDuration = returnMinDuration,
                maxDuration = returnMaxDuration,
                moveEase = returnMoveEase
            };

            _mover.StopDistance = approachStopDistance;
            _mover.BackDashTrigger = playerBackDashTrigger;
        }

        // ─────────────────────────────────────────
        // Binding
        // ─────────────────────────────────────────
        public void Bind(BattleActorView player, BattleActorView enemy)
        {
            Bind(player, enemy != null ? new List<BattleActorView> { enemy } : null);
        }

        public void Bind(BattleActorView player, IList<BattleActorView> enemies)
        {
            if (player != null)
            {
                player.drivenByDirector = true;
                playerView = player.transform;
                playerAnimator = player.animator ?? player.GetComponentInChildren<Animator>(true);
            }

            for (int i = 0; i < enemyViews.Length; i++)
            {
                enemyViews[i] = null;
                enemyAnimators[i] = null;
            }

            if (enemies != null)
            {
                int n = Mathf.Min(enemies.Count, enemyViews.Length);
                for (int i = 0; i < n; i++)
                {
                    var e = enemies[i];
                    if (e == null) continue;
                    e.drivenByDirector = false;

                    enemyViews[i] = e.transform;
                    enemyAnimators[i] = e.animator ?? e.GetComponentInChildren<Animator>(true);
                }
            }

            CacheEnemyBases();
            InitializeMover();
            _fsm.Reset();

            if (targetManager == null)
                targetManager = FindObjectOfType<BattleTargetManager>(true);
            
            // ✅ Bind로 playerAnimator가 확보된 시점: base 캐시 + pending oath 적용
            CachePlayerBaseControllerIfNeeded();
            if (!string.IsNullOrEmpty(_pendingOathId)) 
                ApplyOathAnimatorOverride(_pendingOathId);
        }
        
        /// <summary>
        /// 슬롯 인덱스를 유지하며 바인딩 (null 슬롯 허용)
        /// BattleStageSpawner에서 호출합니다.
        /// </summary>
        public void BindWithSlots(BattleActorView player, BattleActorView[] enemySlots)
        {
            if (player != null)
            {
                player.drivenByDirector = true;
                playerView = player.transform;
                playerAnimator = player.animator ?? player.GetComponentInChildren<Animator>(true);
            }

            // 기존 배열 초기화
            for (int i = 0; i < enemyViews.Length; i++)
            {
                enemyViews[i] = null;
                enemyAnimators[i] = null;
            }

            // ✅ 슬롯 인덱스를 유지하며 할당 (null 슬롯은 건너뜀)
            if (enemySlots != null)
            {
                int n = Mathf.Min(enemySlots.Length, enemyViews.Length);
                for (int i = 0; i < n; i++)
                {
                    var e = enemySlots[i];
                    if (e == null) continue;  // null 슬롯은 건너뜀
                    e.drivenByDirector = false;

                    enemyViews[i] = e.transform;
                    enemyAnimators[i] = e.animator ?? e.GetComponentInChildren<Animator>(true);
                    
                    Debug.Log($"[BattleAnimDirector] BindWithSlots: slot {i} = {e.name}");
                }
            }

            CacheEnemyBases();
            InitializeMover();
            _fsm.Reset();

            if (targetManager == null)
                targetManager = FindObjectOfType<BattleTargetManager>(true);
            
            // Bind로 playerAnimator가 확보된 시점: base 캐시 + pending oath 적용
            CachePlayerBaseControllerIfNeeded();
            if (!string.IsNullOrEmpty(_pendingOathId)) 
                ApplyOathAnimatorOverride(_pendingOathId);
        }

        private void CacheEnemyBases()
        {
            for (int i = 0; i < enemyViews.Length; i++)
            {
                var v = enemyViews[i];
                if (v == null) continue;
                _enemyBasePos[i] = v.localPosition;
                _enemyBaseScale[i] = v.localScale;
            }
        }

        private void InitializeMover()
        {
            Vector3 basePos = playerView != null ? playerView.localPosition : Vector3.zero;
            Vector3 baseScale = playerView != null ? playerView.localScale : Vector3.one;

            _mover.Initialize(playerView, playerAnimator, enemyViews);
            _mover.SetBasePosition(basePos, baseScale);
        }

        // ─────────────────────────────────────────
        // Profile Helpers
        // ─────────────────────────────────────────
        
        private string GetApproachBoolName(ApproachAnimType type)
        {
            switch (type)
            {
                case ApproachAnimType.None: return null;
                case ApproachAnimType.Run: return playerRunBool;
                case ApproachAnimType.DashB: return playerDashBool;
                case ApproachAnimType.Sprint: return playerSprintBool;
                case ApproachAnimType.Slide: return playerSlideBool;
                case ApproachAnimType.RunBlocking: return playerRunBlockingBool;
                default: return playerRunBool;
            }
        }

        private ApproachAnimProfile GetApproachProfile(CardDefinition card)
        {
            var type = card != null ? card.approachAnimType : ApproachAnimType.Run;
            return AnimProfileManager.GetApproachProfile(type);
        }

        private AttackAnimProfile GetAttackProfile(CardDefinition card)
        {
            var type = card != null ? card.attackAnimType : AttackAnimType.Slash;
            return AnimProfileManager.GetAttackProfile(type);
        }

        private void SetApproachBool(ApproachAnimType type, bool value)
        {
            if (playerAnimator == null) return;
            if (type == ApproachAnimType.None) return;

            string boolName = GetApproachBoolName(type);
            if (string.IsNullOrEmpty(boolName)) return;

            if (value && type != _currentApproachType)
            {
                ClearAllApproachBools();
            }

            playerAnimator.SetBool(boolName, value);
            
            if (value)
                _currentApproachType = type;
        }

        private void ClearAllApproachBools()
        {
            if (playerAnimator == null) return;

            if (!string.IsNullOrEmpty(playerRunBool))
                playerAnimator.SetBool(playerRunBool, false);
            if (!string.IsNullOrEmpty(playerDashBool))
                playerAnimator.SetBool(playerDashBool, false);
            if (!string.IsNullOrEmpty(playerSprintBool))
                playerAnimator.SetBool(playerSprintBool, false);
            if (!string.IsNullOrEmpty(playerSlideBool))
                playerAnimator.SetBool(playerSlideBool, false);
            if (!string.IsNullOrEmpty(playerRunBlockingBool))
                playerAnimator.SetBool(playerRunBlockingBool, false);
            
            _currentApproachType = ApproachAnimType.None;
        }

        // ─────────────────────────────────────────
        // Public API - Attack
        // ─────────────────────────────────────────
        public IEnumerator PlayPlayerAttackComboCo(int targetEnemyIndex)
        {
            yield return PlayPlayerAttackComboCo(null, targetEnemyIndex);
        }

        public IEnumerator PlayPlayerAttackComboCo(CardDefinition card, int targetEnemyIndex)
        {
            targetEnemyIndex = ResolveTargetIndex(targetEnemyIndex);

            int token = _fsm.Enqueue(PlayerAnimFSM.CommandKind.Attack, card, targetEnemyIndex);
            EnsureRunner();
            yield return _fsm.WaitForToken(token);
        }

        // ─────────────────────────────────────────
        // Public API - Card
        // ─────────────────────────────────────────
        public IEnumerator PlayPlayerCardCo(CardDefinition card)
        {
            yield return PlayPlayerCardCo(card, ResolveTargetIndex(-1));
        }

        public IEnumerator PlayPlayerCardCo(CardDefinition card, int targetEnemyIndex)
        {
            if (card == null) yield break;

            targetEnemyIndex = ResolveTargetIndex(targetEnemyIndex);

            if (card.effectKind == CardEffectKind.Attack)
            {
                yield return PlayPlayerAttackComboCo(card, targetEnemyIndex);
                yield break;
            }

            int token = _fsm.Enqueue(PlayerAnimFSM.CommandKind.Cast, card, targetEnemyIndex);
            EnsureRunner();
            yield return _fsm.WaitForToken(token);
        }

        // ─────────────────────────────────────────
        // Public API - Return
        // ─────────────────────────────────────────
        public IEnumerator ReturnToBaseCo() => ReturnToBaseCo(true);

        public IEnumerator ReturnToBaseCo(bool forced)
        {
            int token = _fsm.Enqueue(PlayerAnimFSM.CommandKind.Return, null, -1, forced);
            EnsureRunner();
            yield return _fsm.WaitForToken(token);
        }

        // ─────────────────────────────────────────
        // Public API - Enemy
        // ─────────────────────────────────────────
        public IEnumerator PlayEnemyAttackCo(int enemyIndex = 0)
        {
            if (enemyIndex < 0 || enemyViews == null || enemyIndex >= enemyViews.Length)
                yield break;

            var view = enemyViews[enemyIndex];
            var anim = GetEnemyAnimator(enemyIndex);

            if (anim != null && !string.IsNullOrEmpty(enemyAttackTrigger))
                AnimTriggerTrace.SetTrigger(anim, enemyAttackTrigger, this);

            if (view == null)
            {
                yield return new WaitForSeconds(lungeOutTime + lungeBackTime);
                yield break;
            }

            Vector3 start = view.localPosition;
            float dir = playerView != null
                ? Mathf.Sign((playerView.position - view.position).x)
                : 1f;

            Vector3 outPos = start + new Vector3(dir * lungeDistance, 0f, 0f);

            view.DOKill(true);
            var seq = DOTween.Sequence();
            seq.Join(view.DOLocalMove(outPos, lungeOutTime).SetEase(Ease.OutQuad));
            seq.Append(view.DOLocalMove(start, lungeBackTime).SetEase(Ease.InQuad));
            yield return seq.WaitForCompletion();
        }

        public void PlayPlayerHitFx()
        {
            if (playerAnimator != null && !string.IsNullOrEmpty(playerHitTrigger))
                AnimTriggerTrace.SetTrigger(playerAnimator, playerHitTrigger, this);

            if (playerView != null)
            {
                playerView.DOKill(true);
                playerView.DOShakePosition(playerHitShakeDuration, playerHitShakeStrength, 12, 90f, false, true);
            }
        }

        public void OnTargetChanged(int selectedIndex)
        {
            if (enemyViews == null || enemyViews.Length == 0) return;
            
            // base cache가 비어있으면(0스케일 등) 현재 값으로 보정
            for (int i = 0; i < enemyViews.Length; i++)
                EnsureEnemyBaseCached(i);
            
            selectedIndex = Mathf.Clamp(selectedIndex, 0, enemyViews.Length - 1);
            selectedIndex = ResolveExistingEnemyIndex(selectedIndex);
            
            // ✅ 타겟 이미지(타겟 매니저)도 동기화
            SyncTargetManagerSelectedIndex(selectedIndex);

            for (int i = 0; i < enemyViews.Length; i++)
            {
                var v = enemyViews[i];
                if (v == null || !v.gameObject.activeInHierarchy) continue;

                v.DOKill(true);

                Vector3 basePos = _enemyBasePos[i];
                Vector3 baseScale = _enemyBaseScale[i];

                if (i == selectedIndex)
                {
                    float dir = playerView != null
                        ? Mathf.Sign((playerView.position - v.position).x)
                        : 1f;

                    Vector3 focusPos = basePos + new Vector3(dir * focusMove, 0f, 0f);

                    v.DOLocalMove(focusPos, focusTime).SetEase(Ease.OutQuad);
                    v.DOScale(baseScale + Vector3.one * focusScale, focusTime).SetEase(Ease.OutQuad);

                    var anim = GetEnemyAnimator(i);
                    if (anim != null && !string.IsNullOrEmpty(enemyFocusTrigger))
                        AnimTriggerTrace.SetTrigger(anim, enemyFocusTrigger, this);
                }
                else
                {
                    v.DOLocalMove(basePos, focusTime).SetEase(Ease.OutQuad);
                    v.DOScale(baseScale, focusTime).SetEase(Ease.OutQuad);
                }
            }
        }
        
        
        // ─────────────────────────────────────────
        // Public API - Enemy Death
        // ─────────────────────────────────────────
        public void PlayEnemyDieFx(int enemyIndex)
        {
            if (enemyIndex < 0 || enemyViews == null || enemyIndex >= enemyViews.Length) return;
            var anim = GetEnemyAnimator(enemyIndex);
            if (anim == null) return;
            if (string.IsNullOrEmpty(enemyDieTrigger)) return;

            // Reset+Set로 확실히 발사
            AnimTriggerTrace.ResetAndSetTrigger(anim, enemyDieTrigger, this);
        }

        public IEnumerator PlayEnemyDieCo(int enemyIndex)
        {
            PlayEnemyDieFx(enemyIndex);
            if (enemyDieMinShowTime > 0f)
                yield return new WaitForSeconds(enemyDieMinShowTime);
        }

        // ─────────────────────────────────────────
        // Helpers (Target/Enemy Animator Cache)
        // ─────────────────────────────────────────
        private Animator GetEnemyAnimator(int index)
        {
            if (enemyAnimators != null &&
                index >= 0 && index < enemyAnimators.Length &&
                enemyAnimators[index] != null)
                return enemyAnimators[index];

            var v = (enemyViews != null && index >= 0 && index < enemyViews.Length) ? enemyViews[index] : null;
            if (v == null) return null;

            var a = v.GetComponentInChildren<Animator>(true);
            if (enemyAnimators != null && index >= 0 && index < enemyAnimators.Length)
                enemyAnimators[index] = a;
            return a;
        }

        private void EnsureEnemyBaseCached(int i)
        {
            if (enemyViews == null || i < 0 || i >= enemyViews.Length) return;
            var v = enemyViews[i];
            if (v == null) return;

            // 스케일이 0이면(=미캐시 가능성이 높음) 현재 값으로 캐시
            if (_enemyBaseScale[i] == Vector3.zero)
            {
                _enemyBasePos[i] = v.localPosition;
                _enemyBaseScale[i] = v.localScale;
            }
        }

        private int ResolveExistingEnemyIndex(int preferred)
        {
            if (enemyViews == null || enemyViews.Length == 0) return preferred;

            if (preferred >= 0 && preferred < enemyViews.Length)
            {
                var v = enemyViews[preferred];
                if (v != null && v.gameObject.activeInHierarchy) return preferred;
            }

            for (int i = 0; i < enemyViews.Length; i++)
            {
                var v = enemyViews[i];
                if (v != null && v.gameObject.activeInHierarchy) return i;
            }

            for (int i = 0; i < enemyViews.Length; i++)
                if (enemyViews[i] != null) return i;

            return preferred;
        }

        private void SyncTargetManagerSelectedIndex(int selectedIndex)
        {
            if (targetManager == null) return;
            try
            {
                var t = targetManager.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var prop = t.GetProperty("SelectedIndex", flags);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(int))
                {
                    prop.SetValue(targetManager, selectedIndex);
                }
                else
                {
                    var field = t.GetField("SelectedIndex", flags);
                    if (field != null && field.FieldType == typeof(int))
                        field.SetValue(targetManager, selectedIndex);
                }
            }
            catch { /* targetManager 구현 차이 허용 */ }

            // 구현체가 어떤 이름을 쓰든 안전하게 한 번 더 흔들어주기
            targetManager.SendMessage("OnTargetChanged", selectedIndex, SendMessageOptions.DontRequireReceiver);
            targetManager.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
        }

        // ─────────────────────────────────────────
        // Command Runner
        // ─────────────────────────────────────────
        private void EnsureRunner()
        {
            if (_runner == null && isActiveAndEnabled)
                _runner = StartCoroutine(CommandRunnerCo());
        }

        private void StopRunner()
        {
            if (_runner != null)
            {
                StopCoroutine(_runner);
                _runner = null;
            }
            _fsm?.ClearQueue();
        }

        private IEnumerator CommandRunnerCo()
        {
            while (true)
            {
                if (!_fsm.TryDequeue(out var cmd))
                {
                    yield return null;
                    continue;
                }

                yield return ExecuteCommandCo(cmd);
                _fsm.MarkCompleted(cmd.Token);
            }
        }

        private IEnumerator ExecuteCommandCo(PlayerAnimFSM.Command cmd)
        {
            switch (cmd.Kind)
            {
                case PlayerAnimFSM.CommandKind.Attack:
                    yield return ExecuteAttackCo(cmd);
                    break;

                case PlayerAnimFSM.CommandKind.Cast:
                    yield return ExecuteCastCo(cmd);
                    break;

                case PlayerAnimFSM.CommandKind.Return:
                    yield return ExecuteReturnCo(cmd.Forced);
                    break;

                case PlayerAnimFSM.CommandKind.ApproachOnly:
                    yield return ExecuteApproachOnlyCo(cmd.TargetIndex);
                    break;
            }
        }

        // ─────────────────────────────────────────
        // Execute Commands
        // ─────────────────────────────────────────
        private IEnumerator ExecuteAttackCo(PlayerAnimFSM.Command cmd)
        {
            if (playerView == null) yield break;

            int targetIndex = cmd.TargetIndex;
            var card = cmd.Card;
            
            // 프로파일 가져오기
            var approachProfile = GetApproachProfile(card);
            var attackProfile = GetAttackProfile(card);
            var approachType = card != null ? card.approachAnimType : ApproachAnimType.Run;
            var attackType = card != null ? card.attackAnimType : AttackAnimType.Slash;

            // ─────────────────────────────────────────
            // 1. 접근 없음 (원거리 공격)
            // ─────────────────────────────────────────
            if (approachType == ApproachAnimType.None)
            {
                // 근접 상태였으면 먼저 복귀
                if (_fsm.IsMelee)
                {
                    yield return ExecuteReturnCo(true);
                }
                
                _fsm.SetState(PlayerAnimFSM.State.Attacking);
                
                // 적 방향으로 회전만
                if (_mover.IsValidEnemyIndex(targetIndex))
                {
                    float dir = _mover.ComputeDirToEnemy(targetIndex);
                    _mover.FaceDir(dir);
                }
                
                // 공격 트리거
                SetPlayerAttackType(attackType);
                TriggerPlayer(playerAttackTrigger);
                _lastAttackTriggerTime = Time.time;
                _lastAttackDuration = attackProfile.TotalDuration;
                
                // ✅ 히트 딜레이 대기
                if (attackProfile.hitDelay > 0f)
                    yield return new WaitForSeconds(attackProfile.hitDelay);
                
                // Punch effect
                yield return _mover.PunchCo(attackProfile.punchX, attackProfile.punchScale, attackProfile.punchTime);
                
                // ✅ 복구 시간 대기
                if (attackProfile.recoveryTime > 0f)
                    yield return new WaitForSeconds(attackProfile.recoveryTime);
                
                _fsm.SetState(PlayerAnimFSM.State.Idle);
                yield break;
            }

            // ─────────────────────────────────────────
            // 2. 접근이 필요한 경우
            // ─────────────────────────────────────────
            if (_mover.IsValidEnemyIndex(targetIndex))
            {
                if (!_fsm.IsMelee || _fsm.MeleeTargetIndex != targetIndex)
                {
                    _fsm.SetState(PlayerAnimFSM.State.Approaching);

                    if (_fsm.IsMelee)
                    {
                        // 이미 근접 중이지만 다른 타겟 → 리포지션
                        // ✅ 프로파일의 리포지션 시간 사용
                        yield return RepositionWithProfileCo(targetIndex, approachProfile);
                    }
                    else
                    {
                        // 기지에서 출발
                        if (!_mover.IsAtBase())
                            yield return ExecuteReturnCo(true);

                        // ✅ 프로파일 기반 접근
                        yield return ApproachWithProfileCo(targetIndex, approachProfile, approachType);
                    }

                    _fsm.SetMeleeState(true, targetIndex);
                }
            }

            _fsm.SetState(PlayerAnimFSM.State.Attacking);

            // ─────────────────────────────────────────
            // 3. 공격 트리거 (프로파일 기반)
            // ─────────────────────────────────────────
            SetPlayerAttackType(attackType);
            TriggerPlayer(playerAttackTrigger);
            _lastAttackTriggerTime = Time.time;
            _lastAttackDuration = attackProfile.TotalDuration;

            // ✅ 접근 bool 해제 (Attack 상태로 전환 후)
            yield return new WaitForSeconds(Mathf.Max(0.02f, attackProfile.hitDelay * 0.3f));
            ClearAllApproachBools();

            // ✅ 히트 타이밍까지 대기 (남은 시간)
            float remainHitDelay = attackProfile.hitDelay - (attackProfile.hitDelay * 0.3f);
            if (remainHitDelay > 0f)
                yield return new WaitForSeconds(remainHitDelay);

            // Punch effect
            yield return _mover.PunchCo(attackProfile.punchX, attackProfile.punchScale, attackProfile.punchTime);

            // ✅ 화면 흔들림 (있으면)
            if (attackProfile.hitShakeStrength > 0f && attackProfile.hitShakeDuration > 0f)
            {
                if (playerView != null)
                    playerView.DOShakePosition(attackProfile.hitShakeDuration, attackProfile.hitShakeStrength, 10, 90f, false, false);
            }

            // ✅ 복구 시간 대기
            if (attackProfile.recoveryTime > 0f)
                yield return new WaitForSeconds(attackProfile.recoveryTime);

            _fsm.SetState(_fsm.IsMelee ? PlayerAnimFSM.State.MeleeIdle : PlayerAnimFSM.State.Idle);
        }

        /// <summary>
        /// 프로파일 기반 접근 코루틴
        /// </summary>
        private IEnumerator ApproachWithProfileCo(int targetIndex, ApproachAnimProfile profile, ApproachAnimType type)
        {
            if (!_mover.IsValidEnemyIndex(targetIndex) || playerView == null)
                yield break;

            var enemy = enemyViews[targetIndex];
            float dir = _mover.ComputeDirToEnemy(targetIndex);
            _mover.FaceDir(dir);

            Vector3 targetWorld = enemy.position - new Vector3(dir * approachStopDistance, 0f, 0f);
            Transform parent = playerView.parent ?? playerView;
            Vector3 targetLocal = parent.InverseTransformPoint(targetWorld);

            float dist = Vector3.Distance(playerView.localPosition, targetLocal);
            float duration = profile.ComputeDuration(dist);

            // ✅ 접근 애니메이션 시작
            SetApproachBool(type, true);

            // ✅ 시작 홀드 (애니메이션 준비)
            if (profile.beginHold > 0f)
                yield return new WaitForSeconds(profile.beginHold);

            // 이동
            float loopDur = Mathf.Max(profile.loopMinDuration, duration - profile.beginHold - profile.endHold);

            playerView.DOKill(true);
            yield return playerView
                .DOLocalMove(targetLocal, loopDur)
                .SetEase(profile.moveEase)
                .WaitForCompletion();

            // ✅ 끝 홀드 (착지/정지 모션)
            if (profile.endHold > 0f)
                yield return new WaitForSeconds(profile.endHold);

            // 접근 bool은 공격 트리거 후에 해제
        }

        /// <summary>
        /// 프로파일 기반 리포지션 코루틴
        /// </summary>
        private IEnumerator RepositionWithProfileCo(int targetIndex, ApproachAnimProfile profile)
        {
            if (!_mover.IsValidEnemyIndex(targetIndex) || playerView == null)
                yield break;

            var enemy = enemyViews[targetIndex];
            float moveDir = Mathf.Sign(enemy.position.x - playerView.position.x);
            if (Mathf.Approximately(moveDir, 0f)) moveDir = 1f;

            Vector3 targetWorld = enemy.position - new Vector3(moveDir * approachStopDistance, 0f, 0f);
            Transform parent = playerView.parent ?? playerView;
            Vector3 targetLocal = parent.InverseTransformPoint(targetWorld);

            float dist = Vector3.Distance(playerView.localPosition, targetLocal);

            if (dist <= 0.05f)
            {
                _mover.FaceDir(moveDir);
                yield break;
            }

            TriggerPlayer(playerBackDashTrigger);

            // ✅ 프로파일의 리포지션 시간 사용
            float repoTime = Mathf.Max(0.08f, profile.repositionTime);

            playerView.DOKill(true);
            yield return playerView
                .DOLocalMove(targetLocal, repoTime)
                .SetEase(profile.repositionEase)
                .WaitForCompletion();

            _mover.FaceDir(moveDir);
        }

        private IEnumerator ExecuteCastCo(PlayerAnimFSM.Command cmd)
        {
            var card = cmd.Card;
            if (card == null) yield break;

            // Non-attack from base
            if (_fsm.IsMelee)
            {
                _fsm.SetState(PlayerAnimFSM.State.Returning);
                yield return ExecuteReturnCo(true);
            }

            _fsm.SetState(PlayerAnimFSM.State.Casting);

            // Trigger
            if (card.effectKind == CardEffectKind.Block)
                TriggerPlayer(playerBlockTrigger);
            else
                TriggerPlayer(playerCastTrigger);

            // Small punch
            yield return _mover.PunchCo(comboPunchX * 0.8f, comboPunchScale * 0.7f, comboPunchTime);

            _fsm.SetState(PlayerAnimFSM.State.Idle);
        }

        private IEnumerator ExecuteReturnCo(bool forced)
        {
            if (!_fsm.IsMelee || playerView == null)
            {
                _fsm.SetState(PlayerAnimFSM.State.Idle);
                yield break;
            }

            // ✅ 공격 애니메이션 완료 대기
            yield return WaitPostAttackGuard();

            _fsm.SetState(PlayerAnimFSM.State.Returning);
            
            ClearAllApproachBools();

            yield return _mover.ReturnToBaseCo(forced, () => TriggerPlayer(playerBackDashTrigger));

            _fsm.SetMeleeState(false, -1);
            _fsm.SetState(PlayerAnimFSM.State.Idle);
        }

        private IEnumerator ExecuteApproachOnlyCo(int targetIndex)
        {
            if (!_fsm.IsMelee) yield break;
            if (_fsm.MeleeTargetIndex == targetIndex)
            {
                _fsm.SetState(PlayerAnimFSM.State.MeleeIdle);
                yield break;
            }

            _fsm.SetState(PlayerAnimFSM.State.Approaching);
            
            var profile = ApproachAnimProfile.CreateDefault(ApproachAnimType.Run);
            yield return RepositionWithProfileCo(targetIndex, profile);
            
            _fsm.SetMeleeState(true, targetIndex);
            _fsm.SetState(PlayerAnimFSM.State.MeleeIdle);
        }

        // ─────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────
        private int ResolveTargetIndex(int requested)
        {
            if (_mover.IsValidEnemyIndex(requested)) return requested;

            if (_fsm.IsMelee && _mover.IsValidEnemyIndex(_fsm.MeleeTargetIndex))
                return _fsm.MeleeTargetIndex;

            if (targetManager != null && _mover.IsValidEnemyIndex(targetManager.SelectedIndex))
                return targetManager.SelectedIndex;

            for (int i = 0; i < enemyViews.Length; i++)
                if (_mover.IsValidEnemyIndex(i)) return i;

            return -1;
        }

        private void TriggerPlayer(string trigger)
        {
            if (playerAnimator == null || string.IsNullOrEmpty(trigger)) return;
            AnimTriggerTrace.ResetAndSetTrigger(playerAnimator, trigger, this);
        }

        private void SetPlayerAttackType(AttackAnimType type)
        {
            if (playerAnimator == null || string.IsNullOrEmpty(playerAttackTypeInt)) return;
            playerAnimator.SetInteger(playerAttackTypeInt, (int)type);
        }
        
        /// <summary>
        /// ✅ 공격 애니메이션 완료까지 대기
        /// </summary>
        private IEnumerator WaitPostAttackGuard()
        {
            float elapsed = Time.time - _lastAttackTriggerTime;
            float required = _lastAttackDuration;
            
            // 최소 대기 시간 보장
            if (required < 0.1f) required = 0.1f;
            
            if (elapsed < required)
            {
                float wait = required - elapsed;
                yield return new WaitForSeconds(wait);
            }
        }
    }
}
