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

        public event Action StateChanged;
        public event Action Died;

        public bool IsAlive => HP > 0;

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
                return;
            }

            MaxHP = run.State.maxHP;
            HP = run.State.hp;
            Block = 0;

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
            Block = 0;
            StateChanged?.Invoke();
        }

        // ─────────────────────────────────────────
        // Damage
        // ─────────────────────────────────────────
        public int TakeDamage(int rawAmount)
        {
            int amount = Mathf.Max(0, rawAmount);
            int hpBefore = HP;

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
            if (amount <= 0) return;
            HP = Mathf.Min(HP + amount, MaxHP);
            StateChanged?.Invoke();
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
