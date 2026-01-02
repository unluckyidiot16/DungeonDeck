// Assets/_Project/Scripts/Battle/BattlePlayerState.cs
using System;
using UnityEngine;
using DungeonDeck.Run;
using DungeonDeck.Config.Balance;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 플레이어 전투 상태 관리.
    /// HP, 블록, 에너지, 드로우 등.
    /// </summary>
    public class BattlePlayerState
    {
        public int HP { get; private set; }
        public int MaxHP { get; private set; }
        public int Block { get; private set; }
        public int Energy { get; private set; }
        public int DrawPerTurn { get; private set; }
        public int VulnerableTurns { get; private set; }

        public event Action StateChanged;
        public event Action Died;

        public bool IsAlive => HP > 0;
        
        private const float VULNERABLE_DAMAGE_MULT = 1.5f;
        // NOTE: 소수 처리 정책은 ceil(올림). (1 피해도 취약이면 체감되게)

        // ─────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────
        public void Initialize(RunSession run)
        {
            if (run == null || run.State == null)
            {
                MaxHP = 60;
                HP = MaxHP;
                Energy = 3;
                DrawPerTurn = 5;
                Block = 0;
                VulnerableTurns = 0;
                return;
            }

            MaxHP = run.State.maxHP;
            HP = run.State.hp;
            Block = 0;
            VulnerableTurns = 0;

            var balance = run.Balance;
            Energy = balance != null ? balance.startEnergyPerTurn : 3;
            DrawPerTurn = balance != null ? balance.startDrawPerTurn : 5;
        }

        // ─────────────────────────────────────────
        // Turn Management
        // ─────────────────────────────────────────
        public void BeginTurn(RunSession run)
        {
            Block = 0;

            var balance = run?.Balance;
            Energy = balance != null ? balance.startEnergyPerTurn : 3;

            StateChanged?.Invoke();
        }

        // ─────────────────────────────────────────
        // Energy
        // ─────────────────────────────────────────
        public bool TrySpendEnergy(int amount)
        {
            if (amount < 0) amount = 0;
            if (Energy < amount) return false;

            Energy -= amount;
            StateChanged?.Invoke();
            return true;
        }

        public void GainEnergy(int amount)
        {
            if (amount <= 0) return;
            Energy += amount;
            StateChanged?.Invoke();
        }

        public bool HasEnergy(int amount) => Energy >= amount;

        // ─────────────────────────────────────────
        // Block
        // ─────────────────────────────────────────
        public void GainBlock(int amount)
        {
            if (amount <= 0) return;
            Block += amount;
            StateChanged?.Invoke();
        }

        public void ClearBlock()
        {
            if (Block == 0) return;
            Block = 0;
            StateChanged?.Invoke();
        }
        
        // ─────────────────────────────────────────
        // Debuff: Vulnerable
        // ─────────────────────────────────────────
        /// <summary>
        /// 취약: 받는 피해 증가. (턴 단위)
        /// </summary>
        public void ApplyVulnerable(int turns)
        {
            if (turns <= 0) return;
            int before = VulnerableTurns;
            VulnerableTurns = Mathf.Max(0, VulnerableTurns + turns);
            if (VulnerableTurns != before)
                StateChanged?.Invoke();
        }
    
        /// <summary>
        /// 취약 턴 감소(0 아래로 내려가지 않음). 보통 플레이어 턴 시작에 호출.
        /// </summary>
        public void TickVulnerable()
        {
            int before = VulnerableTurns;
            if (VulnerableTurns > 0) VulnerableTurns -= 1;
            if (VulnerableTurns != before)
                StateChanged?.Invoke();
        }

        // ─────────────────────────────────────────
        // Damage
        // ─────────────────────────────────────────
        public int TakeDamage(int rawAmount)
        {
            int amount = Mathf.Max(0, rawAmount);
            
            // ✅ 취약 보정: 들어오는 피해를 증가시킨 뒤 Block을 적용한다.
            // (STS류 규칙과 동일: Block이 더 빨리 깎이게 됨)
            if (amount > 0 && VulnerableTurns > 0)
                amount = Mathf.CeilToInt(amount * VULNERABLE_DAMAGE_MULT);
            int hpBefore = HP;
            int blockBefore = Block;
            
            int remain = amount;
            if (Block > 0)
            {
                int used = Mathf.Min(Block, remain);
                Block -= used;
                remain -= used;
            }

            if (remain > 0) HP -= remain;
            if (HP < 0) HP = 0;

            int hpLoss = Mathf.Max(0, hpBefore - HP);

            if (hpLoss != 0 || Block != blockBefore)
                StateChanged?.Invoke();

            if (!IsAlive)
                Died?.Invoke();

            return hpLoss;
        }

        // ─────────────────────────────────────────
        // Healing
        // ─────────────────────────────────────────
        public void Heal(int amount)
        {
            int before = HP;
            HP = Mathf.Min(HP + amount, MaxHP);
            if (HP != before) StateChanged?.Invoke();
        }

        // ─────────────────────────────────────────
        // Sync Back
        // ─────────────────────────────────────────
        public void SyncToRun(RunSession run)
        {
            if (run == null || run.State == null) return;
            run.State.hp = HP;
        }
    }
}
