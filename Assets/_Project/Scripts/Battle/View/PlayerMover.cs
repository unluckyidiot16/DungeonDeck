// Assets/_Project/Scripts/Battle/View/PlayerMover.cs
using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 플레이어 이동 로직.
    /// Approach, Return, Reposition 등.
    /// </summary>
    [Serializable]
    public class MoveProfile
    {
        [Header("Speed-Based Movement")]
        public bool useSpeedBased = true;
        public float speed = 4.0f;
        public float minDuration = 0.18f;
        public float maxDuration = 0.55f;
        public Ease moveEase = Ease.InOutSine;

        [Header("Fixed Duration (if not speed-based)")]
        public float fixedDuration = 0.14f;

        public float ComputeDuration(float distance)
        {
            if (!useSpeedBased)
                return Mathf.Max(0.01f, fixedDuration);

            float spd = Mathf.Max(0.01f, speed);
            float dur = distance / spd;
            return Mathf.Clamp(dur, minDuration, maxDuration);
        }
    }

    [Serializable]
    public class RunAnimProfile
    {
        [Tooltip("Use Run bool for approach animation")]
        public bool useRunBool = true;
        public string runBoolParam = "Run";

        [Header("Hold Times")]
        public float beginHold = 0.06f;
        public float endHold = 0.04f;
        public float loopMinDuration = 0.08f;

        [Header("Distance Scaling")]
        public float holdMinDistance = 0.30f;
        public float holdMaxDistance = 1.40f;
        [Range(0f, 1f)] public float holdMinScale = 0.25f;

        public float ComputeHoldScale(float distance)
        {
            if (distance <= holdMinDistance)
            {
                if (holdMinDistance <= 0.0001f) return 0f;
                float t0 = distance / holdMinDistance;
                return Mathf.Lerp(0f, holdMinScale, t0);
            }

            if (distance < holdMaxDistance)
            {
                float t = Mathf.InverseLerp(holdMinDistance, holdMaxDistance, distance);
                return Mathf.Lerp(holdMinScale, 1f, t);
            }

            return 1f;
        }
    }

    public class PlayerMover
    {
        // ─────────────────────────────────────────
        // References
        // ─────────────────────────────────────────
        private Transform _playerView;
        private Animator _playerAnimator;
        private Transform[] _enemyViews;

        private Vector3 _baseLocalPos;
        private Vector3 _baseLocalScale;

        // ─────────────────────────────────────────
        // Settings
        // ─────────────────────────────────────────
        public MoveProfile ApproachProfile { get; set; } = new MoveProfile();
        public MoveProfile ReturnProfile { get; set; } = new MoveProfile
        {
            speed = 6.0f,
            minDuration = 0.10f,
            maxDuration = 0.35f,
            moveEase = Ease.OutQuad
        };
        public RunAnimProfile RunProfile { get; set; } = new RunAnimProfile();

        public float StopDistance { get; set; } = 1f;
        public float BaseLocalEpsilon { get; set; } = 0.02f;
        public float RepositionMinDistance { get; set; } = 0.05f;
        public float RepositionMoveTime { get; set; } = 0.10f;

        // Triggers
        public string BackDashTrigger { get; set; } = "BackDash";
        public string DashTrigger { get; set; } = "Dash";

        // ─────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────
        public void Initialize(Transform playerView, Animator animator, Transform[] enemyViews)
        {
            _playerView = playerView;
            _playerAnimator = animator;
            _enemyViews = enemyViews;

            if (_playerView != null)
            {
                _baseLocalPos = _playerView.localPosition;
                _baseLocalScale = _playerView.localScale;
            }
        }

        public void SetBasePosition(Vector3 localPos, Vector3 localScale)
        {
            _baseLocalPos = localPos;
            _baseLocalScale = localScale;
        }

        public void ResetToBase()
        {
            if (_playerView == null) return;

            _playerView.DOKill(true);
            _playerView.localPosition = _baseLocalPos;
            _playerView.localScale = _baseLocalScale;
        }

        // ─────────────────────────────────────────
        // Queries
        // ─────────────────────────────────────────
        public bool IsAtBase()
        {
            if (_playerView == null) return true;
            return Vector3.Distance(_playerView.localPosition, _baseLocalPos) <= BaseLocalEpsilon;
        }

        public bool IsValidEnemyIndex(int index)
        {
            return _enemyViews != null && index >= 0 && index < _enemyViews.Length && _enemyViews[index] != null;
        }

        public float ComputeDirToEnemy(int targetIndex)
        {
            if (!IsValidEnemyIndex(targetIndex) || _playerView == null) return 1f;
            float dir = Mathf.Sign(_enemyViews[targetIndex].position.x - _playerView.position.x);
            return Mathf.Approximately(dir, 0f) ? 1f : dir;
        }

        public float CurrentFacingDir()
        {
            if (_playerView == null) return 1f;
            float d = Mathf.Sign(_playerView.localScale.x);
            return Mathf.Approximately(d, 0f) ? 1f : d;
        }

        // ─────────────────────────────────────────
        // Facing
        // ─────────────────────────────────────────
        public void FaceDir(float dir)
        {
            if (_playerView == null) return;

            var scale = _baseLocalScale;
            scale.x = Mathf.Abs(scale.x) * (dir >= 0 ? 1f : -1f);
            _playerView.localScale = scale;
        }

        // ─────────────────────────────────────────
        // Movement Coroutines
        // ─────────────────────────────────────────
        public IEnumerator ApproachCo(int targetIndex, Action<bool> onRunBool = null)
        {
            if (!IsValidEnemyIndex(targetIndex) || _playerView == null)
                yield break;

            var enemy = _enemyViews[targetIndex];
            float dir = ComputeDirToEnemy(targetIndex);
            FaceDir(dir);

            Vector3 targetWorld = enemy.position - new Vector3(dir * StopDistance, 0f, 0f);
            Transform parent = _playerView.parent ?? _playerView;
            Vector3 targetLocal = parent.InverseTransformPoint(targetWorld);

            float dist = Vector3.Distance(_playerView.localPosition, targetLocal);
            float duration = ApproachProfile.ComputeDuration(dist);

            if (RunProfile.useRunBool)
            {
                onRunBool?.Invoke(true);

                bool useHold = dist >= RunProfile.holdMinDistance;
                float holdScale = RunProfile.ComputeHoldScale(dist);

                float beginHold = useHold ? RunProfile.beginHold * holdScale : 0f;
                float endHold = useHold ? RunProfile.endHold * holdScale : 0f;

                float loopDur = duration - beginHold - endHold;
                if (loopDur < RunProfile.loopMinDuration)
                    loopDur = RunProfile.loopMinDuration;

                if (beginHold > 0f)
                    yield return new WaitForSeconds(beginHold);

                _playerView.DOKill(true);
                yield return _playerView
                    .DOLocalMove(targetLocal, loopDur)
                    .SetEase(ApproachProfile.moveEase)
                    .WaitForCompletion();

                onRunBool?.Invoke(false);

                if (endHold > 0f)
                    yield return new WaitForSeconds(endHold);
            }
            else
            {
                _playerView.DOKill(true);
                yield return _playerView
                    .DOLocalMove(targetLocal, duration)
                    .SetEase(ApproachProfile.moveEase)
                    .WaitForCompletion();
            }
        }

        public IEnumerator ReturnToBaseCo(bool forced, Action onTriggerBackDash = null)
        {
            if (_playerView == null) yield break;

            onTriggerBackDash?.Invoke();

            float dist = Vector3.Distance(_playerView.localPosition, _baseLocalPos);
            float duration = ReturnProfile.ComputeDuration(dist);

            _playerView.DOKill(true);
            yield return _playerView
                .DOLocalMove(_baseLocalPos, duration)
                .SetEase(ReturnProfile.moveEase)
                .WaitForCompletion();

            _playerView.localScale = _baseLocalScale;
        }

        public IEnumerator RepositionCo(int targetIndex, Action onTriggerBackDash = null)
        {
            if (!IsValidEnemyIndex(targetIndex) || _playerView == null)
                yield break;

            var enemy = _enemyViews[targetIndex];
            float moveDir = Mathf.Sign(enemy.position.x - _playerView.position.x);
            if (Mathf.Approximately(moveDir, 0f)) moveDir = 1f;

            Vector3 targetWorld = enemy.position - new Vector3(moveDir * StopDistance, 0f, 0f);
            Transform parent = _playerView.parent ?? _playerView;
            Vector3 targetLocal = parent.InverseTransformPoint(targetWorld);

            float dist = Vector3.Distance(_playerView.localPosition, targetLocal);

            if (dist <= RepositionMinDistance)
            {
                FaceDir(moveDir);
                yield break;
            }

            onTriggerBackDash?.Invoke();

            _playerView.DOKill(true);
            yield return _playerView
                .DOLocalMove(targetLocal, RepositionMoveTime)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();

            FaceDir(moveDir);
        }

        // ─────────────────────────────────────────
        // Punch Effects
        // ─────────────────────────────────────────
        public IEnumerator PunchCo(float punchX, float punchScale, float duration)
        {
            if (_playerView == null) yield break;

            float dir = CurrentFacingDir();

            _playerView.DOKill(true);
            var seq = DOTween.Sequence();
            seq.Join(_playerView.DOPunchPosition(new Vector3(dir * punchX, 0f, 0f), duration, 10, 0.8f));
            seq.Join(_playerView.DOPunchScale(Vector3.one * punchScale, duration, 10, 0.8f));
            yield return seq.WaitForCompletion();
        }
    }
}
