// Assets/_Project/Scripts/Battle/View/BattleAnimDirector.cs (Refactored)
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using DungeonDeck.Config.Cards;
using DungeonDeck.Debugging;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 전투 애니메이션 감독 (리팩토링됨).
    /// 실제 로직은 PlayerAnimFSM, PlayerMover에 위임.
    /// 이 클래스는 외부 API와 조정자 역할만 담당.
    /// </summary>
    public class BattleAnimDirector : MonoBehaviour
    {
        // ─────────────────────────────────────────
        // Inspector References
        // ─────────────────────────────────────────
        [Header("Player")]
        public Transform playerView;
        public Animator playerAnimator;

        [Header("Enemies")]
        public Transform[] enemyViews = new Transform[3];
        public Animator[] enemyAnimators = new Animator[3];

        [Header("Target Manager")]
        public BattleTargetManager targetManager;

        // ─────────────────────────────────────────
        // Inspector Settings - Triggers
        // ─────────────────────────────────────────
        [Header("Player Triggers")]
        public string playerAttackTrigger = "Attack";
        public string playerBlockTrigger = "Block";
        public string playerCastTrigger = "Cast";
        public string playerHitTrigger = "Hit";
        public string playerBackDashTrigger = "BackDash";
        public string playerRunBool = "Run";

        [Header("Enemy Triggers")]
        public string enemyAttackTrigger = "Attack";
        public string enemyFocusTrigger = "Focus";

        // ─────────────────────────────────────────
        // Inspector Settings - Movement
        // ─────────────────────────────────────────
        [Header("Approach")]
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

        [Header("Combat FX")]
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

        [Header("Attack Guard")]
        public float postAttackGuardSeconds = 0.08f;
        public string attackStateTag = "Attack";
        [Range(0.5f, 1f)] public float attackGuardNormalizedTime = 0.90f;

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

        // ─────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────
        public bool IsMelee => _fsm?.IsMelee ?? false;
        public int MeleeTargetIndex => _fsm?.MeleeTargetIndex ?? -1;

        // ─────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────
        private void Awake()
        {
            _fsm = new PlayerAnimFSM();
            _mover = new PlayerMover();

            ConfigureMover();
        }

        private void OnDisable()
        {
            StopRunner();
            _fsm?.Reset();
            _mover?.ResetToBase();

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

            // Init enemy arrays
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
            var anim = enemyAnimators != null && enemyIndex < enemyAnimators.Length
                ? enemyAnimators[enemyIndex] : null;

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
            selectedIndex = Mathf.Clamp(selectedIndex, 0, enemyViews.Length - 1);

            for (int i = 0; i < enemyViews.Length; i++)
            {
                var v = enemyViews[i];
                if (v == null) continue;

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

                    var anim = enemyAnimators != null && i < enemyAnimators.Length ? enemyAnimators[i] : null;
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

            // Approach if needed
            if (_mover.IsValidEnemyIndex(targetIndex))
            {
                if (!_fsm.IsMelee || _fsm.MeleeTargetIndex != targetIndex)
                {
                    _fsm.SetState(PlayerAnimFSM.State.Approaching);

                    if (_fsm.IsMelee)
                    {
                        yield return _mover.RepositionCo(targetIndex, () => TriggerPlayer(playerBackDashTrigger));
                    }
                    else
                    {
                        if (!_mover.IsAtBase())
                            yield return ExecuteReturnCo(true);

                        yield return _mover.ApproachCo(targetIndex, on => SetRunBool(on));
                    }

                    _fsm.SetMeleeState(true, targetIndex);
                }
            }

            _fsm.SetState(PlayerAnimFSM.State.Attacking);

            // Attack trigger
            TriggerPlayer(playerAttackTrigger);
            _lastAttackTriggerTime = Time.time;

            // Punch effect
            yield return _mover.PunchCo(comboPunchX, comboPunchScale, comboPunchTime);

            _fsm.SetState(_fsm.IsMelee ? PlayerAnimFSM.State.MeleeIdle : PlayerAnimFSM.State.Idle);
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

            // Wait for attack animation if needed
            yield return WaitPostAttackGuard();

            _fsm.SetState(PlayerAnimFSM.State.Returning);
            SetRunBool(false);

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
            yield return _mover.RepositionCo(targetIndex, () => TriggerPlayer(playerBackDashTrigger));
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

        private void SetRunBool(bool value)
        {
            if (playerAnimator == null || string.IsNullOrEmpty(playerRunBool)) return;
            playerAnimator.SetBool(playerRunBool, value);
        }

        private IEnumerator WaitPostAttackGuard()
        {
            float elapsed = Time.time - _lastAttackTriggerTime;
            if (elapsed < postAttackGuardSeconds)
                yield return new WaitForSeconds(postAttackGuardSeconds - elapsed);
        }
    }
}
