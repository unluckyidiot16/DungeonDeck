using System.Collections;
using UnityEngine;

namespace DungeonDeck.Battle.View
{
    public class BattleActorView : MonoBehaviour
    {
        public Animator animator;

        [Header("Animator Triggers")]
        public string trigAttack = "Attack";
        public string trigHit = "Hit";
        public string trigBlock = "Block";
        public string trigHeal = "Heal";
        public string trigDebuff = "Debuff";
        public string trigDie = "Die";

        [Header("Fallback Punch")]
        public float punchDistance = 0.15f;
        public float punchDuration = 0.10f;

        private Vector3 _baseLocalPos;
        private Coroutine _punchCo;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _baseLocalPos = transform.localPosition;
        }

        public void PlayAttack() => PlayOrPunch(trigAttack, Vector3.right);
        public void PlayHit() => PlayOrPunch(trigHit, Vector3.left);
        public void PlayBlock() => PlayOrPunch(trigBlock, Vector3.left * 0.5f);
        public void PlayHeal() => PlayOrPunch(trigHeal, Vector3.up * 0.5f);
        public void PlayDebuff() => PlayOrPunch(trigDebuff, Vector3.down * 0.4f);
        public void PlayDie() => Play(trigDie);

        private void Play(string trigger)
        {
            if (animator == null) return;
            animator.ResetTrigger(trigger);
            animator.SetTrigger(trigger);
        }

        private void PlayOrPunch(string trigger, Vector3 dir)
        {
            if (animator != null)
            {
                Play(trigger);
                return;
            }

            if (_punchCo != null) StopCoroutine(_punchCo);
            _punchCo = StartCoroutine(PunchCo(dir.normalized * punchDistance, punchDuration));
        }

        private IEnumerator PunchCo(Vector3 offset, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                transform.localPosition = _baseLocalPos + offset * k;
                yield return null;
            }
            transform.localPosition = _baseLocalPos;
        }
    }
}
