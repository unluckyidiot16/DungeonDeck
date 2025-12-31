// Assets/_Project/Scripts/Config/Enemies/EnemyDefinition.cs
using UnityEngine;

namespace DungeonDeck.Config.Enemies
{
    /// <summary>
    /// 종족값 느낌의 아주 단순한 3분할
    /// - Tank:    HP > ATK
    /// - Balanced:HP = ATK
    /// - Striker: HP < ATK
    /// </summary>
    public enum EnemyStatProfile
    {
        Tank = 0,
        Balanced = 1,
        Striker = 2,
    }

    [CreateAssetMenu(menuName = "DungeonDeck/Enemies/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "enemy.unknown";
        public string displayName = "Enemy";

        [Header("Core Power (MVP)")]
        [Min(0)] public int basePower = 5; // 기존 에셋이 0으로 들어오면 아래 fallback 로직으로 커버
        public EnemyStatProfile defaultProfile = EnemyStatProfile.Balanced;

        [Header("Optional Visual Override")]
        public AnimatorOverrideController animationOverride;

        [Header("Legacy HP (kept for compatibility)")]
        [Min(0)] public int baseMaxHp = 30;
        [Min(0)] public int hpPenaltyPerOrder = 0;
        
        [Tooltip("HP 최소값(페널티로 너무 낮아지는 것 방지)")]
        public int minHp = 10;
        
        [Tooltip("기본 공격력(패턴이 없을 때 사용)")]
        [Min(0)] public int baseAtk = 8;
        
        [Header("Pattern (MVP)")]
        [Tooltip("적의 행동 패턴. 비워두면 BattleController가 단순 기본 공격으로 처리합니다.")]
        public EnemyPatternDefinition defaultPattern;
        
        public int GetBasePowerSafe()
        {
            // 기존 에셋이 basePower=0이면 baseMaxHp에서 대충 환산해서 “안 죽게”만
            if (basePower > 0) return basePower;
            return Mathf.Max(1, Mathf.CeilToInt(baseMaxHp / 10f));
        }

        public void ComputeBaseStats(int orderIndex, int power, EnemyStatProfile profile, out int maxHp, out int atk)
        {
            power = Mathf.Max(1, power);

            // ✅ 너무 복잡해지지 않게 “정수 곱”만 사용
            // (원하면 여기 숫자만 바꿔도 밸런스가 전체적으로 움직임)
            int hp;
            int a;

            switch (profile)
            {
                case EnemyStatProfile.Tank:
                    hp = power * 12;
                    a  = power * 6;
                    break;

                case EnemyStatProfile.Striker:
                    hp = power * 7;
                    a  = power * 11;
                    break;

                default: // Balanced
                    hp = power * 9;
                    a  = power * 9;
                    break;
            }

            if (hpPenaltyPerOrder > 0 && orderIndex > 0)
                hp = Mathf.Max(minHp, hp - hpPenaltyPerOrder * orderIndex);

            maxHp = Mathf.Max(minHp, hp);
            atk   = Mathf.Max(0, a);
        }

        // (기존 코드가 ComputeMaxHp를 쓰고 있으면 계속 동작하도록 유지)
        public int ComputeMaxHp(int orderIndex)
        {
            ComputeBaseStats(orderIndex, GetBasePowerSafe(), defaultProfile, out int hp, out _);
            return hp;
        }
    }
}
