// Assets/_Project/Config/Cards/CardDefinition.cs
using UnityEngine;

namespace DungeonDeck.Config.Cards
{
    public enum CardEffectKind
    {
        Attack,
        Block,
        Draw,
        GainEnergy
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
                default:                        return "";
            }
        }
    }
}