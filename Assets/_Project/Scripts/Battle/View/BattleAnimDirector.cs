using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace DungeonDeck.Battle.View
{
    public class BattleAnimDirector : MonoBehaviour
    {
        [Header("Player")]
        public Transform playerView;
        public Animator playerAnimator;
        public string playerHitTrigger = "Hit";
        public float playerHitShakeDuration = 0.12f;
        public float playerHitShakeStrength = 0.10f;

        [Header("Enemies (by index)")]
        public Transform[] enemyViews = new Transform[3];
        public Animator[] enemyAnimators = new Animator[3];
        public string enemyAttackTrigger = "Attack";

        [Header("Enemy Lunge")]
        public float lungeDistance = 0.25f;
        public float lungeOutTime = 0.10f;
        public float lungeBackTime = 0.12f;

        // -------------------------
        // Auto binding helpers
        // -------------------------

        // Legacy (single enemy) compatibility
        public void Bind(BattleActorView player, BattleActorView enemy)
        {
            if (enemy != null)
                Bind(player, new List<BattleActorView> { enemy });
            else
                Bind(player, (IList<BattleActorView>)null);
        }

        // Multi enemy bind
        public void Bind(BattleActorView player, IList<BattleActorView> enemies)
        {
            if (player != null)
            {
                playerView = player.transform;
                if (playerAnimator == null)
                    playerAnimator = player.GetComponentInChildren<Animator>(true);
            }

            // clear arrays
            for (int i = 0; i < enemyViews.Length; i++)
            {
                enemyViews[i] = null;
                if (enemyAnimators != null && i < enemyAnimators.Length)
                    enemyAnimators[i] = null;
            }

            if (enemies == null) return;

            int n = Mathf.Min(enemies.Count, enemyViews.Length);
            for (int i = 0; i < n; i++)
            {
                var e = enemies[i];
                if (e == null) continue;

                enemyViews[i] = e.transform;

                if (enemyAnimators != null && i < enemyAnimators.Length)
                    enemyAnimators[i] = e.GetComponentInChildren<Animator>(true);
            }
        }

        // Compatibility overload (some older call sites)
        public IEnumerator PlayEnemyAttackCo()
        {
            yield return PlayEnemyAttackCo(0);
        }

        public IEnumerator PlayEnemyAttackCo(int enemyIndex)
        {
            if (enemyIndex < 0) yield break;
            if (enemyViews == null || enemyIndex >= enemyViews.Length) yield break;

            var view = enemyViews[enemyIndex];
            var anim = (enemyAnimators != null && enemyIndex < enemyAnimators.Length) ? enemyAnimators[enemyIndex] : null;

            if (anim != null && !string.IsNullOrEmpty(enemyAttackTrigger))
                anim.SetTrigger(enemyAttackTrigger);

            if (view == null)
            {
                yield return new WaitForSeconds(lungeOutTime + lungeBackTime);
                yield break;
            }

            Vector3 start = view.localPosition;

            // move slightly toward player (left/right only)
            float dir = 1f;
            if (playerView != null)
                dir = Mathf.Sign((playerView.position - view.position).x);

            Vector3 outPos = start + new Vector3(dir * lungeDistance, 0f, 0f);

            view.DOKill(true);
            var seq = DOTween.Sequence();
            seq.Join(view.DOLocalMove(outPos, lungeOutTime).SetEase(Ease.OutQuad));
            seq.Append(view.DOLocalMove(start, lungeBackTime).SetEase(Ease.InQuad));

            yield return seq.WaitForCompletion();
        }

        public void PlayPlayerHitFx()
        {
            if (playerAnimator != null && !string.IsNullOrEmpty(playerHitTrigger))
                playerAnimator.SetTrigger(playerHitTrigger);

            if (playerView != null)
            {
                playerView.DOKill(true);
                playerView.DOShakePosition(playerHitShakeDuration, playerHitShakeStrength, 12, 90f, false, true);
            }
        }
    }
}
