// Assets/_Project/Scripts/UI/Battle/FanCardHoverHandler.cs
using UnityEngine;
using UnityEngine.EventSystems;
using DungeonDeck.Battle;              // ✅ 추가
using DungeonDeck.Config.Cards;        // ✅ 추가

namespace DungeonDeck.UI.Battle
{
    /// <summary>
    /// FanHandLayout과 연동되는 카드 호버 핸들러.
    /// FanHandLayout이 호버 애니메이션을 직접 처리.
    /// + ✅ BattleController에 카드 프리뷰 타겟 UX 연결
    /// </summary>
    public class FanCardHoverHandler : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [HideInInspector] public FanHandLayout layout;
        [HideInInspector] public int cardIndex = -1; // handIndex로 사용한다고 가정

        [Header("Target Preview")]
        public bool enableTargetPreview = true;
        public bool previewOnPointerDown = true;

        private BattleController _battle;
        private CardDefinition _previewedCard; // ✅ “이 핸들러가 마지막으로 프리뷰 걸어둔 카드”

        private void Awake()
        {
            _battle = FindObjectOfType<BattleController>(true);
        }

        private CardDefinition ResolveCard()
        {
            if (_battle == null) _battle = FindObjectOfType<BattleController>(true);
            if (_battle == null) return null;
            if (cardIndex < 0) return null;

            // ✅ BattleController가 가진 런타임 handIndex -> CardDefinition 조회 사용
            return _battle.GetHandCard(cardIndex);
        }

        private void ApplyPreview()
        {
            if (!enableTargetPreview) return;

            var card = ResolveCard();
            if (card == null || _battle == null) return;

            _previewedCard = card;
            _battle.PreviewCardTargeting(card);
        }

        private void ClearPreview()
        {
            if (!enableTargetPreview) return;
            if (_battle == null) return;

            // ✅ “내가 걸어둔 카드”일 때만 해제(카드 간 이동시 깜빡임/오작동 방지)
            if (_previewedCard != null)
                _battle.ClearCardTargetPreview(_previewedCard);

            _previewedCard = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (layout != null && cardIndex >= 0)
                layout.SetHovered(cardIndex);

            ApplyPreview();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (layout != null)
                layout.ClearHover();

            ClearPreview();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!previewOnPointerDown) return;

            // ✅ 모바일/터치: Enter가 안 오는 케이스 대비
            if (layout != null && cardIndex >= 0)
                layout.SetHovered(cardIndex);

            ApplyPreview();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Up에서 바로 지우면 “카드 클릭 → 플레이” 순간 프리뷰가 끊길 수 있어서
            // 기본은 유지. (필요하면 옵션으로 ClearPreview() 넣어도 됨)
        }

        private void OnDisable()
        {
            // ✅ 레이아웃 리빌드/카드 파괴 시 프리뷰 잔존 방지
            ClearPreview();
        }
    }
}
