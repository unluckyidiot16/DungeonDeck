// Assets/_Project/Config/Cards/AttackAnimType.cs
namespace DungeonDeck.Config.Cards
{
    /// <summary>
    /// 플레이어가 공격할 때 사용할 애니메이션 타입.
    /// Animator의 Trigger 파라미터와 매핑됨.
    /// </summary>
    public enum AttackAnimType
    {
        /// <summary>
        /// 기본 베기 공격
        /// Trigger: "Attack"
        /// </summary>
        Slash = 0,
        
        /// <summary>
        /// 찌르기 공격 (창, 레이피어 등)
        /// Trigger: "Thrust"
        /// </summary>
        Thrust = 1,
        
        /// <summary>
        /// 내려치기 공격 (해머, 도끼 등)
        /// Trigger: "Smash"
        /// </summary>
        Smash = 2,
        
        /// <summary>
        /// 연속 공격 (콤보)
        /// Trigger: "Combo"
        /// </summary>
        Combo = 3,
        
        /// <summary>
        /// 원거리 공격 (화살, 투척 등)
        /// Trigger: "RangedAttack"
        /// </summary>
        Ranged = 4,
        
        /// <summary>
        /// 마법 공격 시전
        /// Trigger: "CastAttack"
        /// </summary>
        CastAttack = 5,
        
        /// <summary>
        /// 회전 공격 (범위 공격)
        /// Trigger: "Spin"
        /// </summary>
        Spin = 6,
        
        /// <summary>
        /// 상단 베기
        /// Trigger: "SlashUp"
        /// </summary>
        SlashUp = 7,
        
        /// <summary>
        /// 하단 베기
        /// Trigger: "SlashDown"
        /// </summary>
        SlashDown = 8,
        
        /// <summary>
        /// 강공격 (차지 공격)
        /// Trigger: "HeavyAttack"
        /// </summary>
        Heavy = 9,
    }
}
