using TMPro;
using UnityEngine;
using DG.Tweening;

namespace DungeonDeck.Battle.View
{
    public class HitPopup : MonoBehaviour
    {
        public TMP_Text text;
        public CanvasGroup group;

        public float rise = 40f;
        public float duration = 0.55f;

        private void Awake()
        {
            if (text == null) text = GetComponentInChildren<TMP_Text>(true);
            if (group == null) group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        public void Play(int amount)
        {
            if (text != null) text.text = amount.ToString();

            group.alpha = 1f;
            transform.localScale = Vector3.one;

            var rt = transform as RectTransform;
            Vector2 start = rt != null ? rt.anchoredPosition : Vector2.zero;

            Sequence s = DOTween.Sequence();
            if (rt != null)
                s.Join(rt.DOAnchorPos(start + Vector2.up * rise, duration).SetEase(Ease.OutQuad));

            s.Join(group.DOFade(0f, duration).SetEase(Ease.InQuad));
            s.Join(transform.DOPunchScale(Vector3.one * 0.18f, 0.18f, 10, 1f));

            s.OnComplete(() => Destroy(gameObject));
        }
    }
}