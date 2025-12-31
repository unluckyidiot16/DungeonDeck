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
        // NOTE
        // - BattleEnemy/UI에서 참조하는 최소 스탯만 보관합니다.
        // - def/slotIndex는 디버그/문맥용(세이브 목적 아님)입니다.
                    
        [Header("Debug / Context")]
        [SerializeField] public EnemyDefinition def;
        [SerializeField] public int slotIndex = -1;
                    
        [Header("Core Stats")]
            
        [SerializeField] private int hp;
        [SerializeField] private int maxHp;
        [SerializeField] private int atk;
        
        [Header("Temporary States")]
        [SerializeField] private int block;
        [SerializeField] private int vulnerableTurns;

        public int HP => hp;
        public int MaxHP => maxHp;
        public int ATK => atk;
        public int Block => block;
        public int VulnerableTurns => vulnerableTurns;
        public bool IsAlive => hp > 0;

        /// <summary>
        /// EnemyDefinition SO 기반 초기화
        /// </summary>
        public void ResetFromDefinition(EnemyDefinition def, int orderIndex = 0)
        {
            this.def = def;
            
            if (def == null)
            {
                // 안전 기본값
                ResetWithValues(hp: 30, maxHp: 30, atk: 0);
                return;
            }

            int power = def.GetBasePowerSafe();
            def.ComputeBaseStats(orderIndex, power, def.defaultProfile, out int computedMaxHp, out int computedAtk);
            ResetWithValues(hp: computedMaxHp, maxHp: computedMaxHp, atk: computedAtk);
        }

        /// <summary>
        /// - 기존 호출부 호환을 위해 (maxHealth, attack) 시그니처 유지
        /// </summary>
        public void ResetWithValues(int maxHealth, int attack = 0)
        {
            ResetWithValues(hp: maxHealth, maxHp: maxHealth, atk: attack);
        }

        /// <summary>
        /// HP/MaxHP/ATK를 명시적으로 세팅하는 버전.
        /// BattleEnemy.Init()에서 사용합니다.
        /// </summary>
        public void ResetWithValues(int hp, int maxHp, int atk)
        {
            this.maxHp = Mathf.Max(1, maxHp);
            this.hp = Mathf.Clamp(hp, 0, this.maxHp);
            this.atk = Mathf.Max(0, atk);
            
            block = 0;
            vulnerableTurns = 0;
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
