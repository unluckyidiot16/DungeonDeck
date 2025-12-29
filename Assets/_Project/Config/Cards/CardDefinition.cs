// Assets/_Project/Config/Cards/CardDefinition.cs
using UnityEngine;

namespace DungeonDeck.Config.Cards
{
    public enum CardEffectKind
    {
        Attack,
        Block,
        Draw,
        GainEnergy,
        ApplyVulnerable,
        Heal,
    }

    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    [CreateAssetMenu(menuName = "DungeonDeck/Cards/Card", fileName = "CardDefinition")]
    public class CardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "strike";

        [Header("Display")]
        public string displayName = "Strike";
        public Sprite icon;
        public CardRarity rarity = CardRarity.Common;

        [TextArea(2, 4)]
        public string effectTextOverride;

        [Header("Cost/Effect")]
        public int cost = 1;
        public CardEffectKind effectKind = CardEffectKind.Attack;
        public int value = 6;
        
        [Header("Keywords")]
        [Tooltip("사용 후 전투에서 제거(Exhaust)")]
        public bool exhaustOnPlay = false;
        
        [Header("Animation - Approach")]
        [Tooltip("적에게 접근할 때 사용할 애니메이션")]
        public ApproachAnimType approachAnimType = ApproachAnimType.Run;
        
        [Header("Animation - Attack")]
        [Tooltip("공격 시 사용할 애니메이션 (Attack 타입 카드에만 적용)")]
        public AttackAnimType attackAnimType = AttackAnimType.Slash;


        public string GetDisplayName()
            => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

        public string GetEffectText()
        {
            if (!string.IsNullOrWhiteSpace(effectTextOverride))
                return effectTextOverride;

            // 기본 자동 문구 (M1)
            switch (effectKind)
            {
                case CardEffectKind.Attack:     return $"Deal {value} damage.";
                case CardEffectKind.Block:      return $"Gain {value} block.";
                case CardEffectKind.Draw:       return $"Draw {value} card(s).";
                case CardEffectKind.GainEnergy: return $"Gain {value} energy.";
                case CardEffectKind.ApplyVulnerable: return $"Apply Vulnerable {value} turn(s).";
                case CardEffectKind.Heal: return $"Heal {value}.";
                default:                        return "";
            }
        }
    }
}
