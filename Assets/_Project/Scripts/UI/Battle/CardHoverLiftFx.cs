using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace DungeonDeck.UI.Battle
{
    /// <summary>
    /// 마우스 호버 시 카드가 살짝 올라가고 커짐.
    /// (모바일에선 hover가 거의 안 걸려서 영향 없음)
    /// </summary>
    public class CardHoverLiftFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Hover")]
        public float liftY = 22f;
        public float hoverScale = 1.05f;
        public float hoverDuration = 0.12f;

        [Header("Press")]
        public float pressScale = 0.97f;
        public float pressDuration = 0.06f;

        [Header("Tweens")]
        public bool useUnscaledTime = true;

        RectTransform _rt;
        Vector3 _baseLocalPos;
        Vector3 _baseScale;
        bool _hovered;

        void Awake()
        {
            _rt = transform as RectTransform;
            _baseLocalPos = transform.localPosition;
            _baseScale = transform.localScale;
        }

        void OnEnable()
        {
            // 레이아웃이 바뀌었을 수 있으니 enable 시 기준 재설정
            _baseLocalPos = transform.localPosition;
            _baseScale = transform.localScale;
        }

        void OnDisable()
        {
            transform.DOKill(true);
            transform.localPosition = _baseLocalPos;
            transform.localScale = _baseScale;
            _hovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            // 현재 위치를 기준으로(레이아웃 대응)
            _baseLocalPos = transform.localPosition;
            _baseScale = transform.localScale;

            transform.DOKill(true);

            var seq = DOTween.Sequence();
            seq.SetUpdate(useUnscaledTime);
            seq.Join(transform.DOLocalMoveY(_baseLocalPos.y + liftY, hoverDuration).SetEase(Ease.OutQuad));
            seq.Join(transform.DOScale(_baseScale * hoverScale, hoverDuration).SetEase(Ease.OutQuad));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            transform.DOKill(true);

            var seq = DOTween.Sequence();
            seq.SetUpdate(useUnscaledTime);
            seq.Join(transform.DOLocalMoveY(_baseLocalPos.y, hoverDuration).SetEase(Ease.OutQuad));
            seq.Join(transform.DOScale(_baseScale, hoverDuration).SetEase(Ease.OutQuad));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.DOKill(true);
            var targetScale = (_hovered ? _baseScale * hoverScale : _baseScale) * pressScale;
            transform.DOScale(targetScale, pressDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(useUnscaledTime);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.DOKill(true);
            var targetScale = _hovered ? _baseScale * hoverScale : _baseScale;
            transform.DOScale(targetScale, pressDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(useUnscaledTime);
        }
    }
}
