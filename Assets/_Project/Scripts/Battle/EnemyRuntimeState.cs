using System;
using UnityEngine;
using DungeonDeck.Config.Enemies;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 런타임에서만 변하는 적 상태.
    /// - SO(정의)는 절대 수정하지 않음
    /// - 전투 중 변화는 전부 여기에 누적
    /// </summary>
    [Serializable]
    public class EnemyRuntimeState
    {
        [SerializeField] private int hp;
        [SerializeField] private int maxHp;
        [SerializeField] private int block;
        [SerializeField] private int vulnerableTurns;

        public int HP => hp;
        public int MaxHP => maxHp;
        public int Block => block;
        public int VulnerableTurns => vulnerableTurns;
        public bool IsAlive => hp > 0;

        /// <summary>
        /// EnemyDefinition SO 기반 초기화
        /// </summary>
        public void ResetFromDefinition(EnemyDefinition def, int orderIndex = 0)
        {
            if (def == null)
            {
                // 안전 기본값
                maxHp = 30;
                hp = 30;
                block = 0;
                vulnerableTurns = 0;
                return;
            }

            maxHp = def.ComputeMaxHp(orderIndex);
            hp = maxHp;
            block = 0;
            vulnerableTurns = 0;
        }

        /// <summary>
        /// 직접 값으로 초기화 (RunSession 기반 등)
        /// </summary>
        public void ResetWithValues(int maxHealth, int attack = 0)
        {
            maxHp = Mathf.Max(1, maxHealth);
            hp = maxHp;
            block = 0;
            vulnerableTurns = 0;
            // attack은 현재 사용되지 않지만 확장용으로 파라미터 유지
        }

        public int TakeDamage(int rawAmount)
        {
            if (!IsAlive) return 0;

            int amount = Mathf.Max(0, rawAmount);

            // 취약 1.5배
            if (vulnerableTurns > 0 && amount > 0)
                amount = Mathf.CeilToInt(amount * 1.5f);

            int hpBefore = hp;

            // 블록 먼저 소모
            int remain = amount;
            if (block > 0)
            {
                int used = Mathf.Min(block, remain);
                block -= used;
                remain -= used;
            }

            // 남은 데미지 HP에 적용
            if (remain > 0)
                hp -= remain;

            if (hp < 0) hp = 0;

            return Mathf.Max(0, hpBefore - hp);
        }

        public void Heal(int amount)
        {
            if (!IsAlive) return;
            hp = Mathf.Min(hp + Mathf.Max(0, amount), maxHp);
        }

        public void AddBlock(int amount)
        {
            if (!IsAlive) return;
            block += Mathf.Max(0, amount);
        }

        public void ApplyVulnerable(int turns)
        {
            if (!IsAlive) return;
            vulnerableTurns += Mathf.Max(0, turns);
        }

        public void TickVulnerable()
        {
            if (vulnerableTurns > 0) vulnerableTurns -= 1;
        }
    }
}
