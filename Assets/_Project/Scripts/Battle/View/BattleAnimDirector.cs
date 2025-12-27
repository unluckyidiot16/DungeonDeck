using System.Collections;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Battle.View
{
    public class BattleAnimDirector : MonoBehaviour
    {
        public BattleActorView player;
        public BattleActorView enemy;

        [Header("Timings")]
        public float attackWindup = 0.12f;
        public float hitDelay = 0.06f;
        public float shortPause = 0.08f;

        public bool IsPlaying { get; private set; }

        public void Bind(BattleActorView p, BattleActorView e)
        {
            if (p != null) player = p;
            if (e != null) enemy = e;
        }

        public IEnumerator PlayPlayerCardCo(CardDefinition card)
        {
            if (card == null) yield break;

            IsPlaying = true;

            switch (card.effectKind)
            {
                case CardEffectKind.Attack:
                    if (player) player.PlayAttack();
                    yield return new WaitForSeconds(attackWindup);
                    if (enemy) enemy.PlayHit();
                    yield return new WaitForSeconds(hitDelay);
                    break;

                case CardEffectKind.Block:
                    if (player) player.PlayBlock();
                    yield return new WaitForSeconds(shortPause);
                    break;

                case CardEffectKind.Heal:
                    if (player) player.PlayHeal();
                    yield return new WaitForSeconds(shortPause);
                    break;

                case CardEffectKind.ApplyVulnerable:
                    if (player) player.PlayAttack(); // 임시 “시전” 느낌
                    yield return new WaitForSeconds(shortPause);
                    if (enemy) enemy.PlayDebuff();
                    yield return new WaitForSeconds(shortPause);
                    break;

                default:
                    // 확장 전 기본 연출
                    if (player) player.PlayAttack();
                    yield return new WaitForSeconds(shortPause);
                    break;
            }

            IsPlaying = false;
        }
    }
}
