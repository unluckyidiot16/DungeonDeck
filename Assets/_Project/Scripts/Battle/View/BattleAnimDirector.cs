using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using DungeonDeck.Config.Cards;
using DungeonDeck.Debugging;
using System.Diagnostics;
using System;
using Random = UnityEngine.Random;


namespace DungeonDeck.Battle.View
{
    public class BattleAnimDirector : MonoBehaviour
    {
        [Header("Player")]
        public Transform playerView;
        public Animator playerAnimator;
        public string playerHitTrigger = "Hit";
        public float playerHitShakeDuration = 0.12f;
        public float playerHitShakeStrength = 0.10f;

        [Header("Enemies (by index)")]
        public Transform[] enemyViews = new Transform[3];
        public Animator[] enemyAnimators = new Animator[3];
        public string enemyAttackTrigger = "Attack";

        [Header("Enemy Lunge")]
        public float lungeDistance = 0.25f;
        public float lungeOutTime = 0.10f;
        public float lungeBackTime = 0.12f;

        [Header("Player Card Triggers")]
        public string playerAttackTrigger = "Attack";
        public string playerBlockTrigger = "Block";
        public string playerCastTrigger = "Cast";
        
        [Header("Player Move Anim (Run)")]
        [Tooltip("Animator bool param. True=RunBegin/RunLoop, False=RunEnd/Idle")]
        public string playerRunBool = "Run";
        [Tooltip("이동이 끝나고 Run=false로 바꾼 뒤 잠깐 멈추는 시간(원하면 RunEnd 프레임이 보임). 기본 0")]
        public float runStopDelay = 0f;
        
        // ✅ TurnLeft_Begin/End 삭제 + 복귀는 BackDash로 통일 (옵션 제거)
        
        [Header("Timing Guard (Attack 스킵 방지)")]
        [Tooltip("✅ AnimatorStateInfo.length 기반으로 Attack 모션 가시 시간을 자동 보장")]
        public bool attackGuardUseAnimatorLength = true;
        [Tooltip("Attack 상태에 붙여둔 Tag (Animator State Tag)")]
        public string attackStateTag = "Attack";
        [Range(0.5f, 1f)]
        [Tooltip("Attack가 이 normalizedTime 이상 진행된 뒤에만 Return/리포지션 허용(0.85~0.95 추천)")]
        public float attackGuardNormalizedTime = 0.90f;
        [Tooltip("Length 기반 가드 최대 대기(안전 캡)")]
        public float attackGuardMaxWait = 0.80f;
        [Tooltip("가드 계산 후 약간의 여유(전환 프레임 안전)")]
        public float attackGuardEpsilon = 0.01f;
            
        [Tooltip("태그/전환 감지 실패 시 최소 대기(안전장치)")]
        public float postAttackGuardSeconds = 0.08f;
        [Tooltip("레거시 호환: Return 시작 전 최소 가시 시간 (postAttackGuardSeconds와 max로 합쳐 사용)")]
        public float minAttackVisibleBeforeReturn = 0.08f;
            
        [Header("Return Motion Profile")]
        [Tooltip("true면 복귀 이동 속도를 '접근 속도'와 맞춤(턴 종료 복귀가 너무 빠른 문제 해결)")]
        public bool returnMatchApproachSpeed = true;
        
        [Header("Attack Guard (Animator Tag, optional)")]
        [Tooltip("true면 시간 가드 후, Animator가 Attack 태그 상태면 일정 지점까지 더 기다림(Attack 씹힘 방지 강화)")]
        public bool guardWaitAnimatorAttackTag = true;
        [Range(0f, 1f)]
        public float attackGuardMinNormalizedTime = 0.85f;
        [Tooltip("태그 기반 대기 최대 시간(무한 대기 방지)")]
        public float attackGuardTimeout = 0.45f;

        [Header("Player Card FX (DOTween)")]
        public float playerCardFxDuration = 0.14f;
        public float playerPunchScale = 0.07f;
        public float playerPunchPosX = 0.12f;

        [Header("Enemy Focus")]
        public float focusMove = 0.25f;
        public float focusScale = 0.06f;
        public float focusTime = 0.12f;
        public string enemyFocusTrigger = "Focus";

        [Header("Player Approach Combo")]
        public float approachStopDistance = 1f;   // 적 앞에서 멈추는 거리
        public float approachMoveTime = 0.14f;
        public float returnMoveTime = 0.18f;
        
        [Header("Approach Motion Profile")]
        [Tooltip("true면 approachMoveTime 대신 '속도(유닛/초)'로 이동 시간을 계산")] 
        public bool approachUseSpeedBased = true;
        
        [Header("Return Speed Sync")]
        [Tooltip("true면 복귀(턴종료 Run복귀 포함) 속도를 접근(approach)과 동일한 프로필(속도/클램프)로 맞춤")]
        public bool returnMatchApproachProfile = true;
        
        [Tooltip("첫 접근 이동 속도(유닛/초). 낮출수록 Run이 더 잘 보임")]
        public float approachSpeed = 4.0f;
        [Tooltip("속도 기반일 때 이동 시간 클램프")]
        public float approachMinDuration = 0.18f;
        public float approachMaxDuration = 0.55f;
        [Tooltip("RunLoop 구간 이동 Ease. InOut 계열이면 '포물선 가감속' 느낌")]
        public Ease approachMoveEase = Ease.InOutSine;
        
        [Tooltip("RunBegin/RunEnd에서는 모션만 보여주고, RunLoop에서만 이동")]
        public bool approachUseRunBeginEndHold = true;
        [Tooltip("Run=true 후, 실제 이동 시작 전 홀드(=RunBegin 보여주기)")] 
        public float runBeginHold = 0.06f;
        [Tooltip("Run=false 후, 입력/다음 트리거 전에 홀드(=RunEnd 보여주기)")]
        public float runEndHold = 0.04f;
        [Tooltip("너무 가까우면 홀드로 답답해지니, 이 거리 이상일 때만 Begin/End 홀드 적용")]
        public float runHoldMinDistance = 0.30f;
        [Tooltip("이 거리 이상이면 Begin/End 홀드를 100%로 적용(=긴 접근일수록 더 확실히 보이게)")]
        public float runHoldMaxDistance = 1.40f;
        [Tooltip("MinDistance 근처에서는 홀드를 이 비율만 적용(짧은 접근에서 Begin/End가 과해지는 것 방지)")]
        [Range(0f, 1f)] public float runHoldMinScale = 0.25f;
        [Tooltip("Loop 이동 구간 최소 시간(너무 짧으면 또 씹힘)")]
        public float approachLoopMinDuration = 0.08f;
        
        [Header("Melee Reposition (Dash / BackDash)")]
        [Tooltip("근접 상태에서 타겟 변경 시 이동 시간")]
        public float meleeRepositionMoveTime = 0.10f;
        [Tooltip("너무 가까우면 이동 없이 페이싱만 전환")]
        public float meleeRepositionMinDistance = 0.05f;
        [Tooltip("도착 후 페이싱 전환 딜레이(백대쉬 느낌용)")]
        public float meleeTurnDelay = 0.03f;
        public string playerDashTrigger = "Dash";
        public string playerBackDashTrigger = "BackDash";
        
        [Header("Return To Base (BackDash / TrickTurn)")]
        [Tooltip("forced==false일 때 사용할 트리거(기본 TrickTurn). Animator에 없으면 자동으로 BackDash로 폴백")]
        public string playerTrickTurnTrigger = "TrickTurn";
        [Tooltip("forced==false일 때 TrickTurn/BackDash를 랜덤으로 섞고 싶으면 true")]
        public bool returnRandomWhenNotForced = false;
        [Range(0f, 1f)]
        public float trickTurnChance = 1.0f; // 랜덤일 때 TrickTurn 확률
        [Tooltip("TrickTurn 트리거를 준 뒤, 프레임을 잠깐 보여주기 위한 홀드")]
        public float trickTurnHold = 0.06f;
            
        [Header("Return Motion Profile")]
        [Tooltip("true면 returnMoveTime 대신 '속도(유닛/초)'로 이동 시간을 계산")]
        public bool returnUseSpeedBased = true;
        public float returnSpeed = 6.0f;
        public float returnMinDuration = 0.10f;
        public float returnMaxDuration = 0.35f;
        public Ease returnMoveEase = Ease.OutQuad;

        public float comboPunchX = 0.10f;
        public float comboPunchTime = 0.10f;

        private readonly Vector3[] _enemyBasePos = new Vector3[3];
        private readonly Vector3[] _enemyBaseScale = new Vector3[3];

        private Vector3 _playerBaseLocalPos;
        private Vector3 _playerBaseLocalScale;

        [Header("Base Position Guard")]
        [Tooltip("플레이어가 Base(원위치)로 간주되는 거리(로컬). Approach는 이 상태에서만 허용")]
        public float baseLocalEpsilon = 0.02f;
        
        [Header("Debug Trace (who requested Attack?)")]
        public bool traceAttackRequests = false;
        public bool traceFullStack = false;
        [Tooltip("If true, also trace non-attack card requests (very noisy).")]
        public bool traceAllRequests = false;
        
        private int _reqSeq = 0;
        
        [Header("Refs (optional)")]
        public BattleTargetManager targetManager;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void TraceRequest(string label, CardDefinition card, int targetEnemyIndex)
        {
            if (!traceAttackRequests && !traceAllRequests) return;

            bool isAttack =
                label.Contains("Attack") ||
                (card != null && card.effectKind == CardEffectKind.Attack);

            if (!traceAllRequests && !isAttack) return;

            int id = ++_reqSeq;
            string cardId = card != null ? card.id : "(null)";
            UnityEngine.Debug.Log(
                $"[AnimReq#{id}] dir={name}#{GetInstanceID()} frame={Time.frameCount} t={Time.time:F3} " +
                $"{label} card={cardId} target={targetEnemyIndex}", this);

            var st = new StackTrace(2, true); // skip TraceRequest + its direct caller frame
            string s = st.ToString();

            if (!traceFullStack)
            {
                // Trim to first ~20 lines to keep console readable
                var lines = s.Split('\n');
                int take = Mathf.Min(lines.Length, 20);
                s = string.Join("\n", lines, 0, take);
            }

            UnityEngine.Debug.Log(s, this);
        }

        // ================================
// 1) 필드 추가 (상단 변수들 근처 아무 Header 밑에)
// ================================
[Header("Approach Style (Base -> Enemy only)")]
public bool approachOnlyFromBaseOnAttack = true;
public string defaultApproachId = "Sprint";

[System.Serializable]
public struct ApproachPreset
{
    public string id;              // e.g. Sprint, Slide, Jump...
    public string trigger;         // Animator Trigger (optional)
    public bool useRunBool;        // true=기존 Run(bool) 접근, false=trigger 1회 + 이동
    public Ease moveEase;          // Ease.Unset이면 approachMoveEase 사용
    public float speedMultiplier;  // 1.0 = 기본, 1.2 = 더 빠름
}

public List<ApproachPreset> approachPresets = new();

[System.Serializable]
public struct CardAnimOverride
{
    public string cardId;          // CardDefinition.id
    public string approachId;      // ApproachPreset.id
    public string attackTrigger;   // Attack trigger override
}

public List<CardAnimOverride> cardAnimOverrides = new();

private Dictionary<string, ApproachPreset> _approachPresetById;
private Dictionary<string, CardAnimOverride> _cardAnimById;

private void Awake()
{
    BuildAnimMaps();
}

private void BuildAnimMaps()
{
    _approachPresetById = new Dictionary<string, ApproachPreset>(StringComparer.Ordinal);
    for (int i = 0; i < approachPresets.Count; i++)
    {
        var p = approachPresets[i];
        if (!string.IsNullOrEmpty(p.id))
            _approachPresetById[p.id] = p;
    }

    _cardAnimById = new Dictionary<string, CardAnimOverride>(StringComparer.Ordinal);
    for (int i = 0; i < cardAnimOverrides.Count; i++)
    {
        var o = cardAnimOverrides[i];
        if (!string.IsNullOrEmpty(o.cardId))
            _cardAnimById[o.cardId] = o;
    }
}

private bool TryGetCardOverride(CardDefinition card, out CardAnimOverride ov)
{
    ov = default;
    if (card == null) return false;
    if (_cardAnimById == null) return false;
    return _cardAnimById.TryGetValue(card.id, out ov);
}

private ApproachPreset GetApproachPreset(CardDefinition card)
{
    // fallback = "기존 Run(bool) 접근"
    ApproachPreset preset = new ApproachPreset
    {
        id = defaultApproachId,
        trigger = "",
        useRunBool = true,
        moveEase = Ease.Unset,
        speedMultiplier = 1f
    };

    if (_approachPresetById != null && !string.IsNullOrEmpty(defaultApproachId) &&
        _approachPresetById.TryGetValue(defaultApproachId, out var defP))
    {
        preset = defP;
    }

    if (TryGetCardOverride(card, out var ov) &&
        _approachPresetById != null &&
        !string.IsNullOrEmpty(ov.approachId) &&
        _approachPresetById.TryGetValue(ov.approachId, out var cardP))
    {
        preset = cardP;
    }

    if (preset.speedMultiplier <= 0f) preset.speedMultiplier = 1f;
    return preset;
}

private string GetAttackTrigger(CardDefinition card)
{
    if (TryGetCardOverride(card, out var ov) && !string.IsNullOrEmpty(ov.attackTrigger))
        return ov.attackTrigger;
    return playerAttackTrigger;
}


        
        
        // -------------------------
        // Player FSM (single source of truth)
        // -------------------------
        private enum PlayerState
        {
            Idle,
            Approaching,
            MeleeIdle,
            Attacking,
            Casting,
            Returning
        }

        private enum PlayerCmdKind
        {
            Attack,
            Cast,
            Return,
            ApproachOnly
        }

        private struct PlayerCommand
        {
            public int token;
            public PlayerCmdKind kind;
            public CardDefinition card;   // Cast에서만 사용
            public int targetIndex;
            public bool forced;  // Return 전용 플래그
            public int frame;

            public PlayerCommand(int token, PlayerCmdKind kind, CardDefinition card, int targetIndex, bool forced, int frame)
            {
                this.token = token;
                this.kind = kind;
                this.card = card;
                this.targetIndex = targetIndex;
                this.forced = forced;
                this.frame = frame;
            }
        }

        private PlayerState _pState = PlayerState.Idle;
        private bool _isMelee = false;
        private int _meleeTargetIndex = -1;
        
        // run bool 상태 추적(Animator.GetBool 호출 없이도 판단)
        private bool _runOn = false;
        // 마지막 Attack 트리거 시간

        private readonly Queue<PlayerCommand> _cmdQ = new Queue<PlayerCommand>();
        private readonly Dictionary<int, bool> _done = new Dictionary<int, bool>();
        private Coroutine _runner;
        private int _cmdSeq = 0;

        // “같은 프레임에 같은 커맨드가 2번 들어오는” UI/입력 중복 방지 (FSM 이벤트 디듀프)
        private int _lastEnqueueFrame = -999;
        private PlayerCmdKind _lastEnqueueKind = PlayerCmdKind.Cast;
        private int _lastEnqueueTarget = -999;
        
        // Attack 중복(1장인데 2번 트리거되는 케이스) 방지: 인접 프레임 중복 제거
        private int _lastAttackEnqueueFrame = -999;
        private int _lastAttackEnqueueTarget = -999;
        private const int AttackDedupFrameWindow = 2;
        
        private float _lastAttackTriggerTime = -999f;

        private void MarkAttackTriggered()
        {
            _lastAttackTriggerTime = Time.time;
        }

        private bool IsAtBaseLocal()
        {
            if (playerView == null) return true;
            return Vector3.Distance(playerView.localPosition, _playerBaseLocalPos) <= Mathf.Max(0.0001f, baseLocalEpsilon);
        }
        
        // -------------------------
        // Attack length-based guard
        // -------------------------
        private bool TryGetAttackStateInfo(out AnimatorStateInfo st, out float effectiveSpeed)
        {
            st = default;
            effectiveSpeed = 1f;
            if (!attackGuardUseAnimatorLength) return false;
            if (playerAnimator == null) return false;
            if (string.IsNullOrEmpty(attackStateTag)) return false;
            
            // transition 중이면 Next를 우선(Attack 트리거 직후엔 여기서 잡히는 경우가 많음)
            bool inTrans = playerAnimator.IsInTransition(0);
            AnimatorStateInfo cur = playerAnimator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = inTrans ? playerAnimator.GetNextAnimatorStateInfo(0) : default;
            
            bool curIsAttack  = cur.IsTag(attackStateTag);
            bool nextIsAttack = inTrans && next.IsTag(attackStateTag);
            
            if (!curIsAttack && !nextIsAttack) return false;
            
            st = nextIsAttack ? next : cur;
            
            // 실제 재생 속도(0 방지)
            float sp = Mathf.Abs(playerAnimator.speed * st.speed * st.speedMultiplier);
            effectiveSpeed = Mathf.Max(0.0001f, sp);
            return true;
        }
    
        private float ComputeAttackGuardSeconds()
        {
            if (!TryGetAttackStateInfo(out var st, out var effSpeed))
                return 0f;
        
            // normalizedTime은 1을 넘어갈 수 있으므로 0..1로 정규화
            float n = st.normalizedTime;
            n = n - Mathf.Floor(n); // 0..1
        
            float target = Mathf.Clamp01(attackGuardNormalizedTime);
            float need = target - n;
            if (need <= 0f) return 0f;
        
            // length(clip 길이) / effSpeed로 실제 초 계산
            float sec = (need * st.length) / effSpeed;
            sec += Mathf.Max(0f, attackGuardEpsilon);
            if (attackGuardMaxWait > 0f) sec = Mathf.Min(sec, attackGuardMaxWait);
            return Mathf.Max(0f, sec);
        }

        private IEnumerator WaitPostAttackGuardCo(float minSeconds)
        {
            if (minSeconds <= 0f) yield break;

            float dt = Time.time - _lastAttackTriggerTime;
            if (dt < minSeconds)
                yield return new WaitForSeconds(minSeconds - dt);
        }

        private float ComputeHoldScaleByDistance(float dist)
        {
            dist = Mathf.Max(0f, dist);

            // 0..minDistance 구간은 0..minScale로 아주 작게
            if (dist <= runHoldMinDistance)
            {
                if (runHoldMinDistance <= 0.0001f) return 0f;
                float t0 = dist / runHoldMinDistance;
                return Mathf.Lerp(0f, runHoldMinScale, t0);
            }

            // minDistance..maxDistance 구간은 minScale..1로
            if (dist < runHoldMaxDistance)
            {
                float t = Mathf.InverseLerp(runHoldMinDistance, runHoldMaxDistance, dist);
                return Mathf.Lerp(runHoldMinScale, 1f, t);
            }

            return 1f;
        }


        private void OnDisable()
        {
            if (_runner != null)
            {
                StopCoroutine(_runner);
                _runner = null;
            }

            _cmdQ.Clear();
            _done.Clear();

            if (playerView != null)
            {
                playerView.DOKill(true);
                var v = playerView.GetComponent<BattleActorView>();
                if (v != null) v.drivenByDirector = false;
            }
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

        private void ResetPlayerFsm()
        {
            _cmdQ.Clear();
            _done.Clear();

            _pState = PlayerState.Idle;
            _isMelee = false;
            _meleeTargetIndex = -1;

            if (playerView != null)
            {
                playerView.DOKill(true);
                playerView.localPosition = _playerBaseLocalPos;
                playerView.localScale = _playerBaseLocalScale;
            }
            SetRun(false);
        }

        private void EnsureRunner()
        {
            if (_runner == null && isActiveAndEnabled)
                _runner = StartCoroutine(PlayerRunnerCo());
        }
        
        private int Enqueue(PlayerCmdKind kind, CardDefinition card, int targetIndex, bool forced = false)
        {
            
            int token = ++_cmdSeq;
            
            // 트레이스: Attack 커맨드가 실제로 몇 번 큐에 들어오는지(호출 스택 포함)
            if (kind == PlayerCmdKind.Attack)
                TraceRequest("Enqueue(Attack)", card, targetIndex);

            // 디듀프: 같은 프레임에 동일 커맨드(종류+타겟)가 중복으로 들어오면 즉시 완료 처리
            int f = Time.frameCount;
            
            // Attack은 입력/이벤트 중복으로 1~2프레임 뒤에 한 번 더 들어오는 경우가 있어 별도 디듀프
            if (kind == PlayerCmdKind.Attack && (f - _lastAttackEnqueueFrame) <= AttackDedupFrameWindow && targetIndex == _lastAttackEnqueueTarget)
            {
                TraceRequest("Enqueue(Attack)", card, targetIndex);
                _done[token] = true;
                return token;
            }
            
            if (f == _lastEnqueueFrame && kind == _lastEnqueueKind && targetIndex == _lastEnqueueTarget)
            {
                _done[token] = true;
                return token;
            }

            _lastEnqueueFrame = f;
            _lastEnqueueKind = kind;
            _lastEnqueueTarget = targetIndex;
            
            if (kind == PlayerCmdKind.Attack)
            {
                _lastAttackEnqueueFrame = f;
                _lastAttackEnqueueTarget = targetIndex;
            }

            _done[token] = false;
            _cmdQ.Enqueue(new PlayerCommand(token, kind, card, targetIndex, forced, f));
            EnsureRunner();
            return token;
        }

        private IEnumerator WaitToken(int token)
        {
            while (true)
            {
                bool done;
                if (_done.TryGetValue(token, out done) && done)
                    break;
                yield return null;
            }

            _done.Remove(token);
        }

        private IEnumerator PlayerRunnerCo()
        {
            while (true)
            {
                if (_cmdQ.Count == 0)
                {
                    yield return null;
                    continue;
                }

                var cmd = _cmdQ.Dequeue();
                yield return ExecuteCmdCo(cmd);

                // 완료 마크
                _done[cmd.token] = true;
            }
        }

        private bool IsValidEnemyIndex(int idx)
        {
            return enemyViews != null && idx >= 0 && idx < enemyViews.Length && enemyViews[idx] != null;
        }
        
        // targetIndex가 -1(미지정)으로 들어오는 경우 정규화
        // 같은 카드 입력에서 (-1 공격 요청) + (선택 타겟 공격 요청)이 동시에 들어오면
        // Attack이 2번 큐에 쌓여 2연타처럼 보일 수 있음.
        private int ResolveTargetIndex(int requested)
        {
            if (IsValidEnemyIndex(requested)) return requested;
            
            // 이미 근접 상태라면 현재 근접 타겟 우선
            if (_isMelee && IsValidEnemyIndex(_meleeTargetIndex))
                return _meleeTargetIndex;
            
            // TargetManager가 있으면 현재 선택 타겟 사용
            if (targetManager != null && IsValidEnemyIndex(targetManager.SelectedIndex))
                return targetManager.SelectedIndex;
            
            // 그 외: 첫 번째 유효 적 fallback
            for (int i = 0; i < enemyViews.Length; i++)
                if (IsValidEnemyIndex(i)) return i;
            
            return -1;
        }

        private float ComputeDirToEnemy(int targetIndex)
        {
            if (!IsValidEnemyIndex(targetIndex) || playerView == null) return 1f;
            float dir = Mathf.Sign(enemyViews[targetIndex].position.x - playerView.position.x);
            if (Mathf.Approximately(dir, 0f)) dir = 1f;
            return dir;
        }

        private void FaceDir(float dir)
        {
            if (playerView == null) return;
            if (_playerBaseLocalScale == Vector3.zero) _playerBaseLocalScale = playerView.localScale;

            var sca = _playerBaseLocalScale;
            sca.x = Mathf.Abs(sca.x) * (dir >= 0 ? 1f : -1f);
            playerView.localScale = sca;
        }

        private float CurrentFacingDir()
        {
            if (playerView == null) return 1f;
            float d = Mathf.Sign(playerView.localScale.x);
            if (Mathf.Approximately(d, 0f)) d = 1f;
            return d;
        }
    
        private bool AnimatorHasParam(Animator a, string paramName) 
        {
            if (a == null || string.IsNullOrEmpty(paramName)) return false;
            var ps = a.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == paramName) return true;
            }
            return false;
        }
        
        private void SetPlayerBoolIfExists(string param, bool value)
        {
            if (playerAnimator == null) return;
            if (string.IsNullOrEmpty(param)) return;
            if (!AnimatorHasParam(playerAnimator, param)) return;
            playerAnimator.SetBool(param, value);
        }
    
        private void SetRun(bool value)
        {
            _runOn = value;
            SetPlayerBoolIfExists(playerRunBool, value);
        }
    
        private void TriggerPlayerIfExists(string trig)
        {
            if (playerAnimator == null) return;
            if (string.IsNullOrEmpty(trig)) return;
            if (!AnimatorHasParam(playerAnimator, trig)) return;
            AnimTriggerTrace.ResetAndSetTrigger(playerAnimator, trig, this);
        }
        
        private void TriggerPlayer(string trig)
        {
            if (playerAnimator == null) return;
            if (string.IsNullOrEmpty(trig)) return;

            AnimTriggerTrace.ResetAndSetTrigger(playerAnimator, trig, this);
        }

        private IEnumerator ExecuteCmdCo(PlayerCommand cmd)
        {
            switch (cmd.kind)
            {
                case PlayerCmdKind.ApproachOnly:
                    yield return Co_ApproachOnly(cmd.targetIndex);
                    break;

                case PlayerCmdKind.Attack:
                    yield return Co_Attack(cmd.card, cmd.targetIndex);
                    break;

                case PlayerCmdKind.Cast:
                    yield return Co_Cast(cmd.card, cmd.targetIndex);
                    break;

                case PlayerCmdKind.Return:
                    yield return Co_Return(cmd.forced);
                    break;
            }
        }

        private IEnumerator Co_ApproachOnly(int targetIndex)
        {
            if (!IsValidEnemyIndex(targetIndex) || playerView == null)
                yield break;

            // ✅ Rule 고정: Approach(돌진)은 "Base(원위치) -> Enemy" 구간에서만, 그리고 Attack 흐름에서만 사용.
            // 외부에서 EnsureApproachCo(legacy)로 접근을 시키는 케이스를 차단하기 위해,
            // 여기서는 "이미 Melee 상태"에서의 타겟 변경(BackDash Reposition)만 허용한다.
            if (!_isMelee)
                yield break;

            // 이미 같은 타겟 근접이면 스킵
            if (_isMelee && _meleeTargetIndex == targetIndex)
            {
                _pState = PlayerState.MeleeIdle;
                yield break;
            }

            _pState = PlayerState.Approaching;
            // 근접 상태에서 타겟만 바뀐 경우: Dash/BackDash 리포지션
            if (_isMelee)
                yield return DoMeleeRepositionCo(targetIndex);
            else
                yield return DoApproachCo(targetIndex, GetApproachPreset(null));
            _pState = PlayerState.MeleeIdle;
        }

        // ================================
// 3) Co_Attack 시그니처/본문 교체
// ================================
        private IEnumerator Co_Attack(CardDefinition card, int targetIndex)
        {
            if (playerView == null)
                yield break;

            // 카드별 approach preset / attack trigger
            var preset = GetApproachPreset(card);
            string atkTrig = GetAttackTrigger(card);

            // 타겟이 유효하면 접근 상태로 만든다 (접근 유지가 핵심)
            if (IsValidEnemyIndex(targetIndex))
            {
                if (!_isMelee || _meleeTargetIndex != targetIndex)
                {
                    _pState = PlayerState.Approaching;

                    // ✅ 근접이면 Reposition(BackDash Only)
                    if (_isMelee)
                        yield return DoMeleeRepositionCo(targetIndex);
                    else
                    {
                        // ✅ Rule 고정: Approach는 "Base -> Enemy"에서만.
                        // base가 아니라면(예: 이전 tween 잔여/이상 케이스) 먼저 BackDash로 base 정렬 후 접근.
                        if (approachOnlyFromBaseOnAttack && !IsAtBaseLocal())
                            yield return DoReturnCo(true);

                        yield return DoApproachCo(targetIndex, preset);
                    }
                }
                else
                {
                    _pState = PlayerState.MeleeIdle;
                }
            }
            else
            {
                _pState = PlayerState.Idle;
            }

            _pState = PlayerState.Attacking;

            // ✅ 카드별 공격 트리거(없으면 기본 Attack)
            if (AnimatorHasParam(playerAnimator, atkTrig))
                TriggerPlayer(atkTrig);
            else
                TriggerPlayer(playerAttackTrigger);

            MarkAttackTriggered();

            // 콤보 punch (근접 상태 유지)
            float dir = (_isMelee && IsValidEnemyIndex(_meleeTargetIndex))
                ? ComputeDirToEnemy(_meleeTargetIndex)
                : (playerView.localScale.x >= 0 ? 1f : -1f);

            if (Mathf.Approximately(dir, 0f)) dir = 1f;

            playerView.DOKill(true);
            var seq = DOTween.Sequence();
            seq.Join(playerView.DOPunchPosition(new Vector3(dir * comboPunchX, 0f, 0f), comboPunchTime, 10, 0.8f));
            seq.Join(playerView.DOPunchScale(Vector3.one * playerPunchScale, comboPunchTime, 10, 0.8f));
            yield return seq.WaitForCompletion();

            _pState = _isMelee ? PlayerState.MeleeIdle : PlayerState.Idle;
        }


        private IEnumerator WaitRecentAttackVisibleCo()
        {
            float min = Mathf.Max(minAttackVisibleBeforeReturn, postAttackGuardSeconds);
            if (min > 0f)
                yield return WaitPostAttackGuardCo(min);
            
            // ✅ length 기반 추가 대기(Attack이 짧으면 거의 0, 길면 자연스럽게 늘어남)
            float extra = ComputeAttackGuardSeconds();
            if (extra > 0f)
                yield return new WaitForSeconds(extra);
            yield return WaitAnimatorAttackSafeWindowCo();
        }
        
        private IEnumerator WaitAnimatorAttackSafeWindowCo()
        {
            if (!guardWaitAnimatorAttackTag) yield break;
            if (playerAnimator == null) yield break;
            if (string.IsNullOrEmpty(attackStateTag)) yield break;
            
            float t0 = Time.time;
            while (Time.time - t0 < Mathf.Max(0.01f, attackGuardTimeout))
            {
                var st = playerAnimator.GetCurrentAnimatorStateInfo(0);
                // Tag가 없으면 IsTag가 false라 즉시 빠짐(안전한 폴백)
                if (!st.IsTag(attackStateTag))
                    break;
                
                // normalizedTime은 0..1(비루프) 기준. 1 넘으면 사실상 끝난 뒤라고 봐도 됨.
                if (st.normalizedTime >= attackGuardMinNormalizedTime)
                    break;
                
                yield return null;
            }
        }

        private float HoldWeightByDistance(float dist)
        {
            if (runHoldMaxDistance <= runHoldMinDistance) return 1f;
            float t = Mathf.InverseLerp(runHoldMinDistance, runHoldMaxDistance, dist);
            return Mathf.Lerp(runHoldMinScale, 1f, Mathf.Clamp01(t));
        }

        private IEnumerator Co_Cast(CardDefinition card, int targetIndex)
        {
            if (card == null) yield break;

            // 안전: Attack 카드가 여기로 들어오면 Attack 커맨드로 흡수
            if (card.effectKind == CardEffectKind.Attack)
            {
                yield return Co_Attack(card, targetIndex);
                yield break;
            }

            // 비공격 카드는 항상 원위치에서
            if (_isMelee)
            {
                _pState = PlayerState.Returning;
                yield return DoReturnCo(true);
            }

            _pState = PlayerState.Casting;

            // 트리거
            if (card.effectKind == CardEffectKind.Block) TriggerPlayer(playerBlockTrigger);
            else TriggerPlayer(playerCastTrigger);

            // 짧은 punch (타겟 방향 기반이면 시원함)
            float dir = 1f;
            if (IsValidEnemyIndex(targetIndex) && playerView != null)
                dir = ComputeDirToEnemy(targetIndex);

            if (playerView != null)
            {
                playerView.DOKill(true);
                var seq = DOTween.Sequence();
                seq.Join(playerView.DOPunchPosition(new Vector3(dir * playerPunchPosX, 0f, 0f), playerCardFxDuration, 10, 0.8f));
                seq.Join(playerView.DOPunchScale(Vector3.one * playerPunchScale, playerCardFxDuration, 10, 0.8f));
                yield return seq.WaitForCompletion();
            }
            else
            {
                yield return new WaitForSeconds(playerCardFxDuration);
            }

            // 시전 후에는 Idle 유지(원위치)
            _pState = PlayerState.Idle;
        }

        private IEnumerator Co_Return(bool forced)
        {
            if (!_isMelee || playerView == null)
            {
                _pState = PlayerState.Idle;
                yield break;
            }

            _pState = PlayerState.Returning;
            yield return DoReturnCo(forced);
            _pState = PlayerState.Idle;
        }

            // ================================
            // 4) DoApproachCo 시그니처 변경 + 내부에서 preset 처리
            // ================================
            // 기존: private IEnumerator DoApproachCo(int targetIndex)
            // 변경:
            private IEnumerator DoApproachCo(int targetIndex, ApproachPreset preset)
            {
                if (!IsValidEnemyIndex(targetIndex) || playerView == null)
                    yield break;

                // ✅ Attack 도중 이동이 Attack을 끊지 않도록
                yield return WaitRecentAttackVisibleCo();

                var enemy = enemyViews[targetIndex];

                float dir = Mathf.Sign(enemy.position.x - playerView.position.x);
                if (Mathf.Approximately(dir, 0f)) dir = 1f;
                FaceDir(dir);

                Vector3 targetWorld = enemy.position - new Vector3(dir * approachStopDistance, 0f, 0f);
                Transform parent = playerView.parent != null ? playerView.parent : playerView;
                Vector3 targetLocal = parent.InverseTransformPoint(targetWorld);

                float dist = Vector3.Distance(playerView.localPosition, targetLocal);

                // duration 계산(속도 기반 유지)
                float baseDur = approachMoveTime;
                if (approachUseSpeedBased)
                {
                    float spd = Mathf.Max(0.01f, approachSpeed) * Mathf.Max(0.01f, preset.speedMultiplier);
                    baseDur = dist / spd;
                    baseDur = Mathf.Clamp(baseDur, approachMinDuration, approachMaxDuration);
                }

                Ease ease = (preset.moveEase == Ease.Unset) ? approachMoveEase : preset.moveEase;

                // ✅ preset.useRunBool == true => 기존 Run(bool) 접근
                if (preset.useRunBool)
                {
                    SetRun(true);

                    bool useHold = approachUseRunBeginEndHold && dist >= runHoldMinDistance;
                    float beginHold = 0f;
                    float endHold = 0f;
                    if (useHold)
                    {
                        float t = (runHoldMaxDistance > runHoldMinDistance)
                            ? Mathf.InverseLerp(runHoldMinDistance, runHoldMaxDistance, dist)
                            : 1f;
                        float w = Mathf.Lerp(runHoldMinScale, 1f, Mathf.Clamp01(t));
                        beginHold = Mathf.Max(0f, runBeginHold) * w;
                        endHold   = Mathf.Max(0f, runEndHold)   * w;
                    }

                    float loopDur = baseDur - beginHold - endHold;
                    if (loopDur < approachLoopMinDuration) loopDur = approachLoopMinDuration;

                    if (beginHold > 0f)
                        yield return new WaitForSeconds(beginHold);

                    playerView.DOKill(true);
                    yield return playerView
                        .DOLocalMove(targetLocal, loopDur)
                        .SetEase(ease)
                        .WaitForCompletion();

                    SetRun(false);

                    float stop = Mathf.Max(runStopDelay, endHold);
                    if (stop > 0f)
                        yield return new WaitForSeconds(stop);
                }
                else
                {
                    // ✅ 트리거 기반 돌진(점프/슬라이딩 등)
                    SetRun(false);
                    if (!string.IsNullOrEmpty(preset.trigger))
                        TriggerPlayerIfExists(preset.trigger);

                    playerView.DOKill(true);
                    yield return playerView
                        .DOLocalMove(targetLocal, baseDur)
                        .SetEase(ease)
                        .WaitForCompletion();
                }

                _isMelee = true;
                _meleeTargetIndex = targetIndex;
            }

        
         /// <summary>
        /// 근접 상태에서 타겟이 바뀌었을 때: Dash(정면) vs BackDash(후퇴) 선택.
        /// - 이동 중 페이스 유지(특히 BackDash)
        /// - 도착 후 새 타겟 방향으로 페이스 전환
        /// </summary>
        // ================================
// 5) DoMeleeRepositionCo : BackDash ONLY로 교체
// ================================
        private IEnumerator DoMeleeRepositionCo(int targetIndex)
        {
            if (!IsValidEnemyIndex(targetIndex) || playerView == null)
                yield break;

            // ✅ Attack 직후 이동이 Attack을 끊지 않도록
            yield return WaitRecentAttackVisibleCo();

            var enemy = enemyViews[targetIndex];

            float moveDir = Mathf.Sign(enemy.position.x - playerView.position.x);
            if (Mathf.Approximately(moveDir, 0f)) moveDir = 1f;

            Vector3 targetWorld = enemy.position - new Vector3(moveDir * approachStopDistance, 0f, 0f);
            Transform parent = playerView.parent != null ? playerView.parent : playerView;
            Vector3 targetLocal = parent.InverseTransformPoint(targetWorld);

            float dist = Vector3.Distance(playerView.localPosition, targetLocal);
            if (dist <= meleeRepositionMinDistance)
            {
                _isMelee = true;
                _meleeTargetIndex = targetIndex;
                FaceDir(moveDir);
                yield break;
            }

            // ✅ 항상 BackDash 트리거만
            TriggerPlayerIfExists(playerBackDashTrigger);

            playerView.DOKill(true);
            yield return playerView
                .DOLocalMove(targetLocal, meleeRepositionMoveTime)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();

            _isMelee = true;
            _meleeTargetIndex = targetIndex;

            if (meleeTurnDelay > 0f)
                yield return new WaitForSeconds(meleeTurnDelay);

            FaceDir(moveDir);
        }

        private bool HasParamOrTrigger(string paramOrTrigger)
        {
            return playerAnimator != null && !string.IsNullOrEmpty(paramOrTrigger) && AnimatorHasParam(playerAnimator, paramOrTrigger);
        }
    
        private float ComputeReturnDuration(Vector3 fromLocal, Vector3 toLocal)
        {
            float dist = Vector3.Distance(fromLocal, toLocal);
            if (!returnUseSpeedBased) return Mathf.Max(0.01f, returnMoveTime);
        
            // ✅ 턴종료(Run 복귀 포함) 속도 체감이 너무 빠르면 접근과 동일 프로필로 맞추기
            if (returnMatchApproachProfile && approachUseSpeedBased)
            {
                float spd = Mathf.Max(0.01f, returnSpeed);
                // ✅ 접근 속도와 복귀 속도 통일(턴 종료 복귀가 너무 빠른 문제 해결)
                if (returnMatchApproachSpeed && approachUseSpeedBased)
                    spd = Mathf.Max(0.01f, approachSpeed);
                float dur = dist / spd;
                return Mathf.Clamp(dur, approachMinDuration, approachMaxDuration);
            }
            
            float spd2 = Mathf.Max(0.01f, returnSpeed);
            float dur2 = dist / spd2;
            return Mathf.Clamp(dur2, returnMinDuration, returnMaxDuration);
        }
    
        private IEnumerator DoReturnCo(bool forced)
        {
            if (playerView == null) yield break;
            
            // ✅ Attack 스킵 방지: 직전 Attack 트리거가 있으면 잠깐 기다렸다가 Return 시작
            yield return WaitRecentAttackVisibleCo();
            
            // ✅ 복귀는 BackDash로 통일 (TurnLeft_Begin/End 삭제 전제)
            SetRun(false);

            TriggerPlayerIfExists(playerBackDashTrigger);
            
            float dur = ComputeReturnDuration(playerView.localPosition, _playerBaseLocalPos);
            
            playerView.DOKill(true);
            yield return playerView
                .DOLocalMove(_playerBaseLocalPos, dur)
                .SetEase(returnMoveEase)
                .WaitForCompletion();
            
            playerView.localScale = _playerBaseLocalScale;
            _isMelee = false;
            _meleeTargetIndex = -1;
        }

        // -------------------------
        // Auto binding helpers
        // -------------------------

        // Legacy (single enemy) compatibility
        public void Bind(BattleActorView player, BattleActorView enemy)
        {
            if (enemy != null)
                Bind(player, new List<BattleActorView> { enemy });
            else
                Bind(player, (IList<BattleActorView>)null);
        }

        // Multi enemy bind
        public void Bind(BattleActorView player, IList<BattleActorView> enemies)
        {
            if (player != null)
            {
                player.drivenByDirector = true;
                playerView = player.transform;
                // Prefer the explicit reference (BattleActorView.animator) if set
                playerAnimator = (player.animator != null) ? player.animator : player.GetComponentInChildren<Animator>(true);

                // Optional auto-find
                if (targetManager == null) 
                    targetManager = FindObjectOfType<BattleTargetManager>(true);
                        
                _playerBaseLocalPos = playerView.localPosition;
                _playerBaseLocalScale = playerView.localScale;
            }

            // init enemy arrays
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
                    enemyAnimators[i] = (e.animator != null) ? e.animator : e.GetComponentInChildren<Animator>(true);
                }
            }

            CacheEnemyBases();
            ResetPlayerFsm();
        }

        // -------------------------
        // Target focus (enemy only)
        // -------------------------
        public void OnTargetChanged(int selectedIndex)
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, enemyViews.Length - 1);

            for (int i = 0; i < enemyViews.Length; i++)
            {
                var v = enemyViews[i];
                if (v == null) continue;

                v.DOKill(true);

                // 기본값 복귀
                Vector3 basePos = _enemyBasePos[i];
                Vector3 baseScale = _enemyBaseScale[i];

                if (i == selectedIndex)
                {
                    // 플레이어 방향으로 살짝 전진
                    float dir = 1f;
                    if (playerView != null)
                        dir = Mathf.Sign((playerView.position - v.position).x);

                    Vector3 focusPos = basePos + new Vector3(dir * focusMove, 0f, 0f);

                    v.DOLocalMove(focusPos, focusTime).SetEase(Ease.OutQuad);
                    v.DOScale(baseScale + Vector3.one * focusScale, focusTime).SetEase(Ease.OutQuad);

                    var anim = (enemyAnimators != null && i < enemyAnimators.Length) ? enemyAnimators[i] : null;
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

        // -------------------------
        // Enemy attack
        // -------------------------
        public IEnumerator PlayEnemyAttackCo()
        {
            yield return PlayEnemyAttackCo(0);
        }

        public IEnumerator PlayEnemyAttackCo(int enemyIndex)
        {
            if (enemyIndex < 0) yield break;
            if (enemyViews == null || enemyIndex >= enemyViews.Length) yield break;

            var view = enemyViews[enemyIndex];
            var anim = (enemyAnimators != null && enemyIndex < enemyAnimators.Length) ? enemyAnimators[enemyIndex] : null;

            if (anim != null && !string.IsNullOrEmpty(enemyAttackTrigger))
                AnimTriggerTrace.SetTrigger(anim, enemyAttackTrigger, this);

            if (view == null)
            {
                yield return new WaitForSeconds(lungeOutTime + lungeBackTime);
                yield break;
            }

            Vector3 start = view.localPosition;

            // move slightly toward player (left/right only)
            float dir = 1f;
            if (playerView != null)
                dir = Mathf.Sign((playerView.position - view.position).x);

            Vector3 outPos = start + new Vector3(dir * lungeDistance, 0f, 0f);

            view.DOKill(true);
            var seq = DOTween.Sequence();
            seq.Join(view.DOLocalMove(outPos, lungeOutTime).SetEase(Ease.OutQuad));
            seq.Append(view.DOLocalMove(start, lungeBackTime).SetEase(Ease.InQuad));

            yield return seq.WaitForCompletion();
        }

        // -------------------------
        // Player hit
        // -------------------------
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

        // -------------------------
        // Public API (backward compatible)
        // -------------------------
        public IEnumerator EnsureApproachCo(int targetEnemyIndex)
        {
            // ✅ Rule 고정: base->enemy 접근은 Attack 흐름에서만 사용.
            // Legacy API는 "이미 Melee일 때" 타겟 변경용 Reposition만 허용.
            if (!_isMelee)
                yield break;

            int token = Enqueue(PlayerCmdKind.ApproachOnly, null, targetEnemyIndex);
            yield return WaitToken(token);
        }

        public IEnumerator ReturnToBaseCo()
        {
            // ✅ 호환용: 기존 호출은 forced=true로 처리(=턴종료/강제 복귀로 간주)
            yield return ReturnToBaseCo(true);
        }
        
        public IEnumerator ReturnToBaseCo(bool forced)
        { 
            int token = Enqueue(PlayerCmdKind.Return, null, -1, forced);
            yield return WaitToken(token);
        }

        // ================================
        // 6) Attack 커맨드에 card를 실어 보내기 (오버로드 추가)
        // ================================

        // 기존 PlayPlayerAttackComboCo(int) 유지
        public IEnumerator PlayPlayerAttackComboCo(int targetEnemyIndex)
        {
            yield return PlayPlayerAttackComboCo(null, targetEnemyIndex);
        }

        // 신규: 카드 전달
        public IEnumerator PlayPlayerAttackComboCo(CardDefinition attackCard, int targetEnemyIndex)
        {
            targetEnemyIndex = ResolveTargetIndex(targetEnemyIndex);
            TraceRequest("PlayPlayerAttackComboCo REQUEST", attackCard, targetEnemyIndex);
            int token = Enqueue(PlayerCmdKind.Attack, attackCard, targetEnemyIndex);
            yield return WaitToken(token);
        }

        public IEnumerator PlayPlayerCardCo(CardDefinition card)
        {
            yield return PlayPlayerCardCo(card, ResolveTargetIndex(-1));
        }

        // -------------------------
        // Readonly state (for BattleController hybrid rule)
        // -------------------------
        public bool IsMelee => _isMelee;
        public int MeleeTargetIndex => _meleeTargetIndex;
        
        public IEnumerator PlayPlayerCardCo(CardDefinition card, int targetEnemyIndex)
        {
            if (card == null) yield break;
            
            targetEnemyIndex = ResolveTargetIndex(targetEnemyIndex);
            TraceRequest("PlayPlayerCardCo REQUEST", card, targetEnemyIndex);

            // Attack은 항상 Attack 커맨드로만 처리 (중복 호출도 여기서 흡수)
            if (card.effectKind == CardEffectKind.Attack)
            {
                // ✅ 카드별 approachId / attackTrigger override를 살리려면 card를 전달해야 함
                yield return PlayPlayerAttackComboCo(card, targetEnemyIndex);
                yield break;
            }

            int token = Enqueue(PlayerCmdKind.Cast, card, targetEnemyIndex);
            yield return WaitToken(token);
        }
    }
}
