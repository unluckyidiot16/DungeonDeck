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

    [CreateAssetMenu(menuName = "DungeonDeck/Cards/Card", fileName = "CardDefinition")]
    public class CardDefinition : ScriptableObject
    {
        public string id = "strike";
        public int cost = 1;

        public CardEffectKind effectKind = CardEffectKind.Attack;
        public int value = 6;
    }
}