using System;
using UnityEngine;

namespace DungeonDeck.Battle.Combat
{
    /// <summary>
    /// 전투 유닛 공통 계약: Enemy/Player를 같은 코드로 다루기 위한 최소 인터페이스
    /// </summary>
    public interface ICombatant
    {
        event Action<ICombatant> OnStatsChanged;
        event Action<ICombatant> OnDefeated;
        
        /// <summary>
        /// UI/팝업용 슬롯 인덱스.
        /// Enemy: 0~2, Player: -1 권장
        /// </summary>
        int SlotIndex { get; }

        int HP { get; }
        int MaxHP { get; }
        int Block { get; }
        int VulnerableTurns { get; }
        bool IsAlive { get; }
        
        /// <summary>팝업/이펙트 앵커 (없으면 transform 반환 추천)</summary>
        Transform PopupTarget { get; }
        
        /// <summary>
        /// UI/팝업 라우팅용 슬롯 인덱스.
        /// Enemy는 0~2, Player는 -1 권장.
        /// </summary>
        int PopupSlotIndex { get; }

        int TakeDamage(int rawAmount);
        void Heal(int amount);
        void AddBlock(int amount);
        void ApplyVulnerable(int turns);
        void TickVulnerable();
    }
}