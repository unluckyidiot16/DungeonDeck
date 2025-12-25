using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Config.UI
{
    [CreateAssetMenu(menuName = "DungeonDeck/UI/Card Visual Theme", fileName = "CardVisualTheme")]
    public class CardVisualTheme : ScriptableObject
    {
        [Serializable]
        public class RarityStyle
        {
            public CardRarity rarity;

            [Header("Tint")]
            public Color backgroundTint = Color.white;
            public Color frameTint = Color.white;
            public Color rarityTextTint = Color.white;

            [Header("Optional Sprites")]
            public Sprite frameSprite;
        }

        public List<RarityStyle> styles = new();

        public bool TryGet(CardRarity rarity, out RarityStyle style)
        {
            if (styles != null)
            {
                for (int i = 0; i < styles.Count; i++)
                {
                    var s = styles[i];
                    if (s != null && s.rarity == rarity)
                    {
                        style = s;
                        return true;
                    }
                }
            }
            style = null;
            return false;
        }
    }
}