using UnityEngine;

namespace DungeonDeck.Config.Enemies
{
    [CreateAssetMenu(menuName = "DungeonDeck/Enemies/Enemy Definition", fileName = "EnemyDef_")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "enemy.default";
        public string displayName = "Enemy";

        [Header("Tags")]
        public bool isBoss = false;

        [Header("Base Stats")]
        [Min(1)] public int baseMaxHp = 30;

        [Tooltip("같은 전투 내 N번째 적(0,1,2...)에 따라 HP를 깎고 싶을 때 사용")]
        public int hpPenaltyPerOrder = 5;

        [Tooltip("HP 최소값(페널티로 너무 낮아지는 것 방지)")]
        public int minHp = 10;
        
        [Header("Visual / Animation")]
        [Tooltip("비워두면 프리팹(씬)에 설정된 AnimatorController를 그대로 사용합니다.\n전투 시작 시 BattleStageSpawner가 runtimeAnimatorController로 적용합니다.")]
        public AnimatorOverrideController animationOverride;
            

        // 필요해지면 확장:
        // public int baseAtk;
        // public int rewardGold;
        // public RuntimeAnimatorController animatorBase;
        // public Sprite portrait;

        public int ComputeMaxHp(int orderIndex)
        {
            int hp = Mathf.Max(minHp, baseMaxHp - Mathf.Max(0, orderIndex) * Mathf.Max(0, hpPenaltyPerOrder));
            return Mathf.Max(1, hp);
        }
    }
}