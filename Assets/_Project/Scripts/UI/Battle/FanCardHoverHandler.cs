// Assets/_Project/Scripts/UI/Battle/FanCardHoverHandler.cs
using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonDeck.UI.Battle
{
    /// <summary>
    /// FanHandLayout과 연동되는 카드 호버 핸들러.
    /// CardHoverLiftFx 대신 사용합니다 (FanHandLayout이 호버 애니메이션을 직접 처리).
    /// </summary>
    public class FanCardHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [HideInInspector]
        public FanHandLayout layout;
        
        [HideInInspector]
        public int cardIndex = -1;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (layout == null) return;
            if (cardIndex < 0) return;
            
            layout.SetHovered(cardIndex);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (layout == null) return;
            
            layout.ClearHover();
        }
    }
}
