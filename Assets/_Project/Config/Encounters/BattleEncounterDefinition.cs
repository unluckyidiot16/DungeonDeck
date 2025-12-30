using UnityEngine;
using DungeonDeck.Config.Enemies;

namespace DungeonDeck.Config.Encounters
{
    [CreateAssetMenu(menuName = "DungeonDeck/Encounters/Battle Encounter", fileName = "Encounter_")]
    public class BattleEncounterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "encounter.default";
        public string displayName = "Encounter";

        [Header("Enemies (slots 0~2)")]
        [Tooltip("0~(N-1) 슬롯을 연속으로 채우는 방식 권장. (0,2처럼 구멍은 아직 비권장)")]
        public EnemyDefinition[] enemies = new EnemyDefinition[3];

        [Header("Visual Overrides (optional)")]
        [Tooltip("슬롯별 애니메이션 오버라이드(AnimatorOverrideController). 비워두면 EnemyDefinition.animationOverride → (없으면 기존 AnimatorController) 순으로 사용됩니다.")]
        public AnimatorOverrideController[] slotAnimationOverrides = new AnimatorOverrideController[3];
            
        public AnimatorOverrideController GetAnimationOverrideAt(int slotIndex)
        {
            if (slotAnimationOverrides == null) return null;
            if (slotIndex < 0 || slotIndex >= slotAnimationOverrides.Length) return null;
            return slotAnimationOverrides[slotIndex];
        }
        
        public EnemyDefinition GetEnemyAt(int slotIndex)
        {
            if (enemies == null) return null;
            if (slotIndex < 0 || slotIndex >= enemies.Length) return null;
            return enemies[slotIndex];
        }

        public int GetEnemyCount()
        {
            if (enemies == null) return 0;
            int c = 0;
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null) c++;
            return c;
        }
    }
}