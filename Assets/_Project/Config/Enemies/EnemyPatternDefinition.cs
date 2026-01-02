using System;
using UnityEngine;

namespace DungeonDeck.Config.Enemies
{
    [CreateAssetMenu(menuName = "DungeonDeck/Enemies/Enemy Pattern Definition", fileName = "EnemyPattern_")]
    public class EnemyPatternDefinition : ScriptableObject
    {
        [Serializable]
        public struct Step
        {
            [Tooltip("인텐드 아이콘 키 (예: atk, heavy, def, buff, debuff 등)")]
            public string intentId;

            [Tooltip("ATK에 곱하는 배율. 1.0 = 기본 공격력, 0 = 비공격")]
            [Min(0f)]
            public float damageMultiplier;
            
            [Header("Debuff to Player")]
            [Tooltip("이번 Step 실행 시 플레이어에게 취약을 부여(턴 수). 0이면 미적용")]
            public int applyVulnerableToPlayerTurns;

            [Tooltip("추가 고정 데미지")]
            public int flatBonus;

            [Tooltip("연타 횟수(표시용/확장용). 0이면 1로 처리")]
            public int hits;
            
            [Header("Block (방어 의도용)")]
            [Tooltip("이 Step이 방어일 때 획득할 블록량")]
            public int blockAmount;

            /// <summary>
            /// damageMultiplier가 0이면 비공격 의도로 0 반환
            /// </summary>
            public int EvalDamage(int atk)
            {
                if (damageMultiplier <= 0f)
                    return 0;
                
                int h = Mathf.Max(1, hits);
                int baseDmg = Mathf.RoundToInt(atk * damageMultiplier) + flatBonus;
                return Mathf.Max(0, baseDmg) * h;
            }
            
            /// <summary>
            /// 이 Step이 공격 의도인지 확인 (damageMultiplier > 0)
            /// </summary>
            public bool IsAttackIntent => damageMultiplier > 0f;
            
            /// <summary>
            /// 이 Step이 방어 의도인지 확인
            /// </summary>
            public bool IsDefendIntent => blockAmount > 0 || 
                (!string.IsNullOrEmpty(intentId) && 
                 (intentId.Contains("def") || intentId.Contains("block") || intentId.Contains("shield")));
            
            /// <summary>
            /// 이 Step이 디버프만 의도인지 확인
            /// </summary>
            public bool IsDebuffOnlyIntent => !IsAttackIntent && applyVulnerableToPlayerTurns > 0;
        }

        public bool loop = true;
        public Step[] steps = Array.Empty<Step>();

        public int StepCount => steps == null ? 0 : steps.Length;

        public Step GetStep(int index)
        {
            if (steps == null || steps.Length == 0) return default;

            if (loop)
            {
                int i = index % steps.Length;
                if (i < 0) i += steps.Length;
                return steps[i];
            }

            int clamped = Mathf.Clamp(index, 0, steps.Length - 1);
            return steps[clamped];
        }
    }
}