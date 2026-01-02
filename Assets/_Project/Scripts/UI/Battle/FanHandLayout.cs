// Assets/_Project/Scripts/UI/Battle/FanHandLayout.cs
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace DungeonDeck.UI.Battle
{
    /// <summary>
    /// 하스스톤 스타일의 부채꼴(Fan) 손패 레이아웃.
    /// GridLayoutGroup 대신 사용합니다.
    /// </summary>
    public class FanHandLayout : MonoBehaviour
    {
        [Header("Fan Settings")]
        [Tooltip("부채꼴의 총 각도 (카드가 1장일 때는 0도)")]
        public float maxFanAngle = 30f;
        
        [Tooltip("카드당 최대 각도")]
        public float anglePerCard = 8f;
        
        [Tooltip("부채꼴의 반지름 (중심에서 카드까지 거리)")]
        public float fanRadius = 800f;
        
        [Tooltip("카드 간 최소 간격 (X축)")]
        public float minCardSpacing = 120f;
        
        [Tooltip("카드 간 최대 간격 (X축)")]
        public float maxCardSpacing = 180f;

        [Header("Vertical Arc")]
        [Tooltip("수직 아크 높이 (가운데 카드가 위로 올라감)")]
        public float arcHeight = 25f;

        [Header("Hover")]
        [Tooltip("호버 시 카드가 올라가는 높이")]
        public float hoverLiftY = 60f;
        
        [Tooltip("호버 시 카드 스케일")]
        public float hoverScale = 1.15f;

        [Header("Animation")]
        public float layoutDuration = 0.25f;
        public Ease layoutEase = Ease.OutBack;
        
        [Header("Center Offset")]
        [Tooltip("손패 전체의 Y 오프셋")]
        public float centerOffsetY = -50f;

        // ─────────────────────────────────────────────────
        // Runtime
        // ─────────────────────────────────────────────────
        private readonly List<RectTransform> _cards = new();
        private readonly List<Vector3> _basePositions = new();
        private readonly List<Quaternion> _baseRotations = new();
        private readonly List<Vector3> _baseScales = new();
        private readonly List<int> _baseSiblingIndices = new();
        
        private int _hoveredIndex = -1;
        private RectTransform _rt;

        private void Awake()
        {
            _rt = transform as RectTransform;
        }

        /// <summary>
        /// 카드 목록을 설정하고 레이아웃을 갱신합니다.
        /// </summary>
        public void SetCards(List<RectTransform> cards, bool animate = true)
        {
            _cards.Clear();
            _cards.AddRange(cards);
            
            // 기존 위치 캐시 초기화
            _basePositions.Clear();
            _baseRotations.Clear();
            _baseScales.Clear();
            _baseSiblingIndices.Clear();

            UpdateLayout(animate);
        }

        /// <summary>
        /// 레이아웃만 갱신 (카드 목록은 유지)
        /// </summary>
        public void RefreshLayout(bool animate = true)
        {
            UpdateLayout(animate);
        }

        /// <summary>
        /// 특정 카드에 호버 효과 적용
        /// </summary>
        public void SetHovered(int index)
        {
            if (_hoveredIndex == index) return;
            
            if (_hoveredIndex >= _cards.Count) _hoveredIndex = -1;
            
            // 이전 호버 카드 복구
            if (_hoveredIndex >= 0 && _hoveredIndex < _cards.Count)
            {
                AnimateCardToBase(_hoveredIndex);
            }
            
            _hoveredIndex = index;
            
            // 새 호버 카드 올리기
            if (_hoveredIndex >= 0 && _hoveredIndex < _cards.Count)
            {
                AnimateCardHover(_hoveredIndex);
            }
        }

        /// <summary>
        /// 호버 해제
        /// </summary>
        public void ClearHover()
        {
            SetHovered(-1);
        }

        // ─────────────────────────────────────────────────
        // Layout Calculation
        // ─────────────────────────────────────────────────
        private void UpdateLayout(bool animate)
        {
            int count = _cards.Count;
            if (count == 0) return;

            _basePositions.Clear();
            _baseRotations.Clear();
            _baseScales.Clear();
            _baseSiblingIndices.Clear();

            // 부채꼴 각도 계산
            float totalAngle = Mathf.Min(maxFanAngle, anglePerCard * (count - 1));
            float startAngle = totalAngle / 2f;
            float angleStep = count > 1 ? totalAngle / (count - 1) : 0f;

            // 카드 간격 계산
            float spacing = Mathf.Lerp(maxCardSpacing, minCardSpacing, (count - 1) / 9f);
            float totalWidth = spacing * (count - 1);
            float startX = -totalWidth / 2f;

            for (int i = 0; i < count; i++)
            {
                var card = _cards[i];
                if (card == null) continue;

                // X 위치: 균등 분포
                float x = startX + spacing * i;

                // 각도: 왼쪽이 +, 오른쪽이 -
                float angle = startAngle - angleStep * i;

                // Y 위치: 아크 형태 (가운데가 높음)
                float normalizedPos = count > 1 ? (float)i / (count - 1) : 0.5f;
                float arcFactor = 1f - Mathf.Pow(2f * normalizedPos - 1f, 2f); // 포물선
                float y = centerOffsetY + arcHeight * arcFactor;

                Vector3 pos = new Vector3(x, y, 0f);
                Quaternion rot = Quaternion.Euler(0f, 0f, angle);
                Vector3 scale = Vector3.one;

                _basePositions.Add(pos);
                _baseRotations.Add(rot);
                _baseScales.Add(scale);
                _baseSiblingIndices.Add(i);

                // 애니메이션 적용
                if (animate)
                {
                    AnimateCardTo(card, pos, rot, scale, i);
                }
                else
                {
                    card.DOKill(true);
                    card.anchoredPosition = pos;
                    card.localRotation = rot;
                    card.localScale = scale;
                    card.SetSiblingIndex(i);
                }
            }
        }

        private void AnimateCardTo(RectTransform card, Vector3 pos, Quaternion rot, Vector3 scale, int siblingIndex)
        {
            card.DOKill(true);

            Sequence seq = DOTween.Sequence();
            seq.Join(card.DOAnchorPos(pos, layoutDuration).SetEase(layoutEase));
            seq.Join(card.DOLocalRotateQuaternion(rot, layoutDuration).SetEase(layoutEase));
            seq.Join(card.DOScale(scale, layoutDuration).SetEase(layoutEase));
            
            // 시블링 인덱스는 즉시 설정
            card.SetSiblingIndex(siblingIndex);
        }

        private void AnimateCardToBase(int index)
        {
            if (index < 0 || index >= _cards.Count) return;
            if (index >= _basePositions.Count) return;

            var card = _cards[index];
            if (card == null) return;

            card.DOKill(true);

            Sequence seq = DOTween.Sequence();
            seq.Join(card.DOAnchorPos(_basePositions[index], layoutDuration * 0.7f).SetEase(Ease.OutQuad));
            seq.Join(card.DOLocalRotateQuaternion(_baseRotations[index], layoutDuration * 0.7f).SetEase(Ease.OutQuad));
            seq.Join(card.DOScale(_baseScales[index], layoutDuration * 0.7f).SetEase(Ease.OutQuad));
            
            // 원래 시블링 순서로 복구
            card.SetSiblingIndex(_baseSiblingIndices[index]);
        }

        private void AnimateCardHover(int index)
        {
            if (index < 0 || index >= _cards.Count) return;
            if (index >= _basePositions.Count) return;

            var card = _cards[index];
            if (card == null) return;

            card.DOKill(true);

            // 호버 위치: 원래 위치에서 위로 올림, 회전 해제
            Vector3 hoverPos = _basePositions[index] + new Vector3(0f, hoverLiftY, 0f);
            Quaternion hoverRot = Quaternion.identity; // 회전 해제
            Vector3 hoverScale = Vector3.one * this.hoverScale;

            Sequence seq = DOTween.Sequence();
            seq.Join(card.DOAnchorPos(hoverPos, layoutDuration * 0.5f).SetEase(Ease.OutBack));
            seq.Join(card.DOLocalRotateQuaternion(hoverRot, layoutDuration * 0.5f).SetEase(Ease.OutQuad));
            seq.Join(card.DOScale(hoverScale, layoutDuration * 0.5f).SetEase(Ease.OutBack));
            
            // 맨 앞으로 가져옴
            card.SetAsLastSibling();
        }

        // ─────────────────────────────────────────────────
        // Utility
        // ─────────────────────────────────────────────────
        /// <summary>
        /// 특정 인덱스의 카드 기준 위치 반환
        /// </summary>
        public Vector3 GetBasePosition(int index)
        {
            if (index < 0 || index >= _basePositions.Count)
                return Vector3.zero;
            return _basePositions[index];
        }

        /// <summary>
        /// 특정 인덱스의 카드 기준 회전 반환
        /// </summary>
        public Quaternion GetBaseRotation(int index)
        {
            if (index < 0 || index >= _baseRotations.Count)
                return Quaternion.identity;
            return _baseRotations[index];
        }

        /// <summary>
        /// 카드 수 반환
        /// </summary>
        public int CardCount => _cards.Count;
    }
}
