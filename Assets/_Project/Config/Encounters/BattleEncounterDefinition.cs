// Assets/_Project/Scripts/Config/Encounters/BattleEncounterDefinition.cs
using System;
using UnityEngine;
using DungeonDeck.Config.Enemies;

namespace DungeonDeck.Config.Encounters
{
    [CreateAssetMenu(menuName = "DungeonDeck/Encounters/Battle Encounter Definition", fileName = "Encounter_")]
    public class BattleEncounterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "encounter.default";
        public string title = "Encounter";

        // ✅ Legacy (기존 배열 에셋이 있으면 당장 안 깨지게 유지)
        [Header("Legacy Enemies (backward compatible)")]
        public EnemyDefinition[] enemies = new EnemyDefinition[3];

        [Serializable]
        public struct Slot
        {
            [Header("Enemy")]
            public EnemyDefinition enemy;

            [Header("Overrides (optional)")]
            [Min(0)] public int powerOverride; // 0이면 enemy.basePower 사용
            public bool overrideProfile;
            public EnemyStatProfile profileOverride;

            public EnemyPatternDefinition patternOverride;
            public AnimatorOverrideController animationOverride;
        }

        [Header("Slots (new)")]
        public Slot[] slots = new Slot[3];

        public Slot GetResolvedSlotAt(int slotIndex)
        {
            if (slotIndex < 0) return default;

            Slot s = default;
            if (slots != null && slotIndex < slots.Length) s = slots[slotIndex];

            // 슬롯에 enemy가 없으면 legacy enemies를 fallback
            if (s.enemy == null && enemies != null && slotIndex < enemies.Length)
                s.enemy = enemies[slotIndex];

            return s;
        }

        public EnemyDefinition GetEnemyAt(int slotIndex)
        {
            return GetResolvedSlotAt(slotIndex).enemy;
        }
        
        /// <summary>
        /// 슬롯별 애니메이션 오버라이드 헬퍼 (없으면 null)
        /// </summary>
        public AnimatorOverrideController GetAnimationOverrideAt(int slotIndex)
        {
            return GetResolvedSlotAt(slotIndex).animationOverride;
        }
    
        /// <summary>
        /// 슬롯별 패턴 오버라이드 헬퍼 (없으면 null)
        /// </summary>
        public EnemyPatternDefinition GetPatternOverrideAt(int slotIndex)
        {
            return GetResolvedSlotAt(slotIndex).patternOverride;
        }

        public int GetEnemyCount()
        {
            int count = 0;
            for (int i = 0; i < 3; i++)
                if (GetEnemyAt(i) != null) count++;
            return count;
        }
    }
}