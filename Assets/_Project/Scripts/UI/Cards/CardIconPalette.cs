// Assets/_Project/Scripts/UI/Cards/CardIconPalette.cs
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.UI.Cards
{
    [CreateAssetMenu(menuName = "DungeonDeck/UI/Card Icon Palette", fileName = "CardIconPalette")]
    public class CardIconPalette : ScriptableObject
    {
        [Header("Targets")]
        public Sprite targetSelf;
        public Sprite targetEnemy;
        public Sprite targetAllEnemies;

        [Header("Effects")]
        public Sprite effectAttack;
        public Sprite effectBlock;
        public Sprite effectDraw;
        public Sprite effectEnergy;
        public Sprite effectVulnerable;
        public Sprite effectHeal;

        public Sprite GetTarget(CardEffectTarget t)
        {
            return t switch
            {
                CardEffectTarget.Self => targetSelf,
                CardEffectTarget.Enemy => targetEnemy,
                CardEffectTarget.AllEnemies => targetAllEnemies,
                _ => null
            };
        }

        public Sprite GetEffect(CardEffectKind k)
        {
            return k switch
            {
                CardEffectKind.Attack => effectAttack,
                CardEffectKind.Block => effectBlock,
                CardEffectKind.Draw => effectDraw,
                CardEffectKind.GainEnergy => effectEnergy,
                CardEffectKind.ApplyVulnerable => effectVulnerable,
                CardEffectKind.Heal => effectHeal,
                _ => null
            };
        }
    }
}