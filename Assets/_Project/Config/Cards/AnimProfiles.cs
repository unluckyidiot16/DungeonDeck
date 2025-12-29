// Assets/_Project/Scripts/Battle/View/AnimProfiles.cs
using System;
using UnityEngine;
using DG.Tweening;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 접근 애니메이션 타입별 설정
    /// </summary>
    [Serializable]
    public class ApproachAnimProfile
    {
        [Header("Type")]
        public ApproachAnimType type = ApproachAnimType.Run;
        public string boolParam = "Run";
        
        [Header("Movement")]
        [Tooltip("이동 속도 (units/sec)")]
        public float speed = 4.0f;
        
        [Tooltip("최소 이동 시간")]
        public float minDuration = 0.18f;
        
        [Tooltip("최대 이동 시간")]
        public float maxDuration = 0.55f;
        
        public Ease moveEase = Ease.InOutSine;
        
        [Header("Animation Timing")]
        [Tooltip("이동 시작 전 대기 (애니메이션 준비)")]
        public float beginHold = 0.06f;
        
        [Tooltip("이동 완료 후 대기 (애니메이션 마무리)")]
        public float endHold = 0.04f;
        
        [Tooltip("루프 최소 시간")]
        public float loopMinDuration = 0.08f;
        
        [Header("Reposition (타겟 변경 시)")]
        [Tooltip("리포지션 이동 시간")]
        public float repositionTime = 0.15f;
        
        [Tooltip("리포지션 이동 이즈")]
        public Ease repositionEase = Ease.OutQuad;
        
        public float ComputeDuration(float distance)
        {
            float spd = Mathf.Max(0.01f, speed);
            float dur = distance / spd;
            return Mathf.Clamp(dur, minDuration, maxDuration);
        }
        
        public static ApproachAnimProfile CreateDefault(ApproachAnimType type)
        {
            var profile = new ApproachAnimProfile { type = type };
            
            switch (type)
            {
                case ApproachAnimType.None:
                    profile.boolParam = "";
                    profile.speed = 0f;
                    break;
                    
                case ApproachAnimType.Run:
                    profile.boolParam = "Run";
                    profile.speed = 4.0f;
                    profile.beginHold = 0.06f;
                    profile.endHold = 0.04f;
                    break;
                    
                case ApproachAnimType.DashB:
                    profile.boolParam = "DashB";
                    profile.speed = 8.0f;
                    profile.minDuration = 0.12f;
                    profile.maxDuration = 0.35f;
                    profile.beginHold = 0.02f;
                    profile.endHold = 0.06f;  // 대시 후 착지 대기
                    profile.repositionTime = 0.20f;
                    profile.moveEase = Ease.OutQuad;
                    break;
                    
                case ApproachAnimType.Sprint:
                    profile.boolParam = "Sprint";
                    profile.speed = 6.0f;
                    profile.beginHold = 0.04f;
                    profile.endHold = 0.03f;
                    break;
                    
                case ApproachAnimType.Slide:
                    profile.boolParam = "Slide";
                    profile.speed = 7.0f;
                    profile.minDuration = 0.15f;
                    profile.maxDuration = 0.40f;
                    profile.beginHold = 0.03f;
                    profile.endHold = 0.08f;  // 슬라이드 후 일어나기
                    profile.moveEase = Ease.OutQuad;
                    break;
                    
                case ApproachAnimType.RunBlocking:
                    profile.boolParam = "RunBlocking";
                    profile.speed = 3.0f;
                    profile.beginHold = 0f;
                    profile.endHold = 0f;
                    break;
            }
            
            return profile;
        }
    }
    
    /// <summary>
    /// 공격 애니메이션 타입별 설정
    /// </summary>
    [Serializable]
    public class AttackAnimProfile
    {
        [Header("Type")]
        public AttackAnimType type = AttackAnimType.Slash;
        public string triggerParam = "Attack";
        
        [Header("Timing")]
        [Tooltip("공격 트리거 후 히트 판정까지 대기 시간")]
        public float hitDelay = 0.15f;
        
        [Tooltip("히트 후 추가 대기 (애니메이션 마무리)")]
        public float recoveryTime = 0.12f;
        
        [Tooltip("이 공격 후 다음 행동까지 최소 대기")]
        public float postAttackGuard = 0.08f;
        
        [Header("Punch FX")]
        [Tooltip("펀치 이펙트 X 이동량")]
        public float punchX = 0.10f;
        
        [Tooltip("펀치 이펙트 스케일")]
        public float punchScale = 0.07f;
        
        [Tooltip("펀치 이펙트 시간")]
        public float punchTime = 0.10f;
        
        [Header("Camera/Screen FX")]
        [Tooltip("히트 시 화면 흔들림 강도")]
        public float hitShakeStrength = 0f;
        
        [Tooltip("히트 시 화면 흔들림 시간")]
        public float hitShakeDuration = 0f;
        
        /// <summary>
        /// 전체 공격 애니메이션 예상 시간
        /// </summary>
        public float TotalDuration => hitDelay + punchTime + recoveryTime + postAttackGuard;
        
        public static AttackAnimProfile CreateDefault(AttackAnimType type)
        {
            var profile = new AttackAnimProfile { type = type };
            
            switch (type)
            {
                case AttackAnimType.Slash:
                    profile.triggerParam = "Attack";
                    profile.hitDelay = 0.15f;
                    profile.recoveryTime = 0.12f;
                    profile.punchX = 0.10f;
                    break;
                    
                case AttackAnimType.Thrust:
                    profile.triggerParam = "Thrust";
                    profile.hitDelay = 0.10f;
                    profile.recoveryTime = 0.15f;
                    profile.punchX = 0.15f;  // 더 긴 전진
                    profile.punchScale = 0.05f;
                    break;
                    
                case AttackAnimType.Smash:
                    profile.triggerParam = "Smash";
                    profile.hitDelay = 0.25f;  // 느린 준비
                    profile.recoveryTime = 0.20f;  // 느린 복구
                    profile.punchX = 0.08f;
                    profile.punchScale = 0.12f;  // 더 큰 임팩트
                    profile.hitShakeStrength = 0.05f;
                    profile.hitShakeDuration = 0.10f;
                    break;
                    
                case AttackAnimType.Combo:
                    profile.triggerParam = "Combo";
                    profile.hitDelay = 0.08f;  // 빠른 첫 히트
                    profile.recoveryTime = 0.25f;  // 콤보 전체 시간
                    profile.punchX = 0.06f;
                    profile.punchTime = 0.18f;  // 더 긴 펀치
                    break;
                    
                case AttackAnimType.Ranged:
                    profile.triggerParam = "RangedAttack";
                    profile.hitDelay = 0.20f;  // 발사 준비
                    profile.recoveryTime = 0.10f;
                    profile.punchX = 0.03f;  // 반동만
                    profile.punchScale = 0.03f;
                    break;
                    
                case AttackAnimType.CastAttack:
                    profile.triggerParam = "CastAttack";
                    profile.hitDelay = 0.30f;  // 시전 시간
                    profile.recoveryTime = 0.15f;
                    profile.punchX = 0f;
                    profile.punchScale = 0.08f;
                    break;
                    
                case AttackAnimType.Spin:
                    profile.triggerParam = "Spin";
                    profile.hitDelay = 0.12f;
                    profile.recoveryTime = 0.25f;  // 회전 완료
                    profile.punchX = 0.05f;
                    profile.punchScale = 0.10f;
                    break;
                    
                case AttackAnimType.SlashUp:
                    profile.triggerParam = "SlashUp";
                    profile.hitDelay = 0.12f;
                    profile.recoveryTime = 0.15f;
                    break;
                    
                case AttackAnimType.SlashDown:
                    profile.triggerParam = "SlashDown";
                    profile.hitDelay = 0.18f;
                    profile.recoveryTime = 0.12f;
                    break;
                    
                case AttackAnimType.Heavy:
                    profile.triggerParam = "HeavyAttack";
                    profile.hitDelay = 0.35f;  // 차지
                    profile.recoveryTime = 0.25f;
                    profile.punchX = 0.12f;
                    profile.punchScale = 0.15f;
                    profile.hitShakeStrength = 0.08f;
                    profile.hitShakeDuration = 0.12f;
                    break;
            }
            
            return profile;
        }
    }
    
    /// <summary>
    /// 프로파일 매니저 - 타입별 프로파일 캐시 및 조회
    /// </summary>
    public static class AnimProfileManager
    {
        private static ApproachAnimProfile[] _approachProfiles;
        private static AttackAnimProfile[] _attackProfiles;
        
        public static ApproachAnimProfile GetApproachProfile(ApproachAnimType type)
        {
            EnsureApproachProfiles();
            
            int idx = (int)type;
            if (idx >= 0 && idx < _approachProfiles.Length && _approachProfiles[idx] != null)
                return _approachProfiles[idx];
            
            return ApproachAnimProfile.CreateDefault(type);
        }
        
        public static AttackAnimProfile GetAttackProfile(AttackAnimType type)
        {
            EnsureAttackProfiles();
            
            int idx = (int)type;
            if (idx >= 0 && idx < _attackProfiles.Length && _attackProfiles[idx] != null)
                return _attackProfiles[idx];
            
            return AttackAnimProfile.CreateDefault(type);
        }
        
        /// <summary>
        /// 커스텀 프로파일 등록 (런타임에 오버라이드 가능)
        /// </summary>
        public static void RegisterApproachProfile(ApproachAnimProfile profile)
        {
            if (profile == null) return;
            EnsureApproachProfiles();
            
            int idx = (int)profile.type;
            if (idx >= 0 && idx < _approachProfiles.Length)
                _approachProfiles[idx] = profile;
        }
        
        public static void RegisterAttackProfile(AttackAnimProfile profile)
        {
            if (profile == null) return;
            EnsureAttackProfiles();
            
            int idx = (int)profile.type;
            if (idx >= 0 && idx < _attackProfiles.Length)
                _attackProfiles[idx] = profile;
        }
        
        private static void EnsureApproachProfiles()
        {
            if (_approachProfiles != null) return;
            
            var values = Enum.GetValues(typeof(ApproachAnimType));
            _approachProfiles = new ApproachAnimProfile[values.Length];
            
            foreach (ApproachAnimType t in values)
            {
                int idx = (int)t;
                if (idx >= 0 && idx < _approachProfiles.Length)
                    _approachProfiles[idx] = ApproachAnimProfile.CreateDefault(t);
            }
        }
        
        private static void EnsureAttackProfiles()
        {
            if (_attackProfiles != null) return;
            
            var values = Enum.GetValues(typeof(AttackAnimType));
            _attackProfiles = new AttackAnimProfile[values.Length];
            
            foreach (AttackAnimType t in values)
            {
                int idx = (int)t;
                if (idx >= 0 && idx < _attackProfiles.Length)
                    _attackProfiles[idx] = AttackAnimProfile.CreateDefault(t);
            }
        }
    }
}
