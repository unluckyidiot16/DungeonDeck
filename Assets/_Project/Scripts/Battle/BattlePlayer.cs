using System;
using UnityEngine;
using DungeonDeck.Battle.Combat;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 플레이어 전투 유닛(최소 스켈레톤)
    /// - Enemy처럼 별도 컴포넌트로 분리
    /// - ICombatant로 통일 처리 가능
    /// - 값이 변할 때만 이벤트 발행
    /// </summary>
    public class BattlePlayer : MonoBehaviour, ICombatant
    {
        [Header("Stats")]
        [SerializeField] private int maxHP = 60;
        [SerializeField] private int hp = 60;
        [SerializeField] private int block = 0;
        [SerializeField] private int vulnerableTurns = 0;

        [Header("Popup Anchor (optional)")]
        [SerializeField] private Transform popupTarget;

        private bool _defeatedRaised;

        public event Action<ICombatant> OnDefeated;
        public event Action<ICombatant> OnStatsChanged;

        public int HP => hp;
        public int MaxHP => maxHP;
        public int Block => block;
        public int VulnerableTurns => vulnerableTurns;
        public bool IsAlive => hp > 0;
        public Transform PopupTarget => popupTarget != null ? popupTarget : transform;
        
        // Player는 적 슬롯 개념이 없으므로 -1
        public int SlotIndex => -1;
        public int PopupSlotIndex => -1;

        public void ResetWithValues(int maxHp, int startHp, int startBlock = 0, int startVulnerableTurns = -1)
        {
            bool changed = false;

            maxHp = Mathf.Max(1, maxHp);
            startHp = Mathf.Clamp(startHp, 0, maxHp);
            startBlock = Mathf.Max(0, startBlock);

            if (maxHP != maxHp) { maxHP = maxHp; changed = true; }
            if (hp != startHp) { hp = startHp; changed = true; }
            if (block != startBlock) { block = startBlock; changed = true; }

            // ✅ 선택: startVulnerableTurns >= 0이면 그 값으로 동기화
            if (startVulnerableTurns >= 0)
            {
                startVulnerableTurns = Mathf.Max(0, startVulnerableTurns);
                if (vulnerableTurns != startVulnerableTurns) { vulnerableTurns = startVulnerableTurns; changed = true; }
            }

            _defeatedRaised = false;

            if (changed) RaiseStatsChanged();
        }

        public int TakeDamage(int rawAmount)
        {
            if (!IsAlive) return 0;
            if (rawAmount == 0) return 0;

            int beforeHp = hp;
            int beforeBlock = block;

            int incoming = Mathf.Max(0, rawAmount);

            // Block 먼저 소모
            int blockUsed = Mathf.Min(block, incoming);
            block -= blockUsed;
            incoming -= blockUsed;

            // HP 감소
            if (incoming > 0)
                hp = Mathf.Max(0, hp - incoming);

            bool diedNow = (beforeHp > 0 && hp <= 0);
            bool changed = (beforeHp != hp || beforeBlock != block);

            if (changed) RaiseStatsChanged();

            if (diedNow && !_defeatedRaised)
            {
                _defeatedRaised = true;
                OnDefeated?.Invoke(this);
            }

            // 실제 HP에 들어간 피해량 반환(블록 제외)
            return Mathf.Max(0, beforeHp - hp);
        }

        public void Heal(int amount)
        {
            if (!IsAlive) return;
            if (amount <= 0) return;

            int before = hp;
            hp = Mathf.Clamp(hp + amount, 0, maxHP);
            if (hp != before) RaiseStatsChanged();
        }

        public void AddBlock(int amount)
        {
            if (!IsAlive) return;
            if (amount == 0) return;

            int before = block;
            block = Mathf.Max(0, block + amount);
            if (block != before) RaiseStatsChanged();
        }

        public void ApplyVulnerable(int turns)
        {
            if (!IsAlive) return;
            if (turns <= 0) return;

            int before = vulnerableTurns;
            vulnerableTurns = Mathf.Max(0, vulnerableTurns + turns);
            if (vulnerableTurns != before) RaiseStatsChanged();
        }

        public void TickVulnerable()
        {
            int before = vulnerableTurns;
            if (vulnerableTurns > 0)
                vulnerableTurns -= 1;
            if (vulnerableTurns != before) RaiseStatsChanged();
        }

        private void RaiseStatsChanged()
        {
            OnStatsChanged?.Invoke(this);
        }
    }
}