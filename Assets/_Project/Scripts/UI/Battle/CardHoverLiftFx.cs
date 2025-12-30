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
            // ✅ 이전 DOTween 잔여 애니메이션 즉시 정리
            transform.DOKill(true);
            
            // ✅ 호버 상태 초기화
            _hovered = false;
        }
        
        /// <summary>
        /// 외부에서 기준 위치/스케일을 재설정할 때 호출.
        /// 레이아웃 변경 후나 카드 Bind 후에 호출하세요.
        /// </summary>
        public void ResetBaseTransform()
        {
            transform.DOKill(true);
            _baseLocalPos = transform.localPosition;
            _baseScale = transform.localScale;
            _hovered = false;
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
            // ✅ 이미 호버 중이면 기준값 업데이트하지 않음 (잔상 방지)
            if (_hovered) return;
            
            _hovered = true;

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