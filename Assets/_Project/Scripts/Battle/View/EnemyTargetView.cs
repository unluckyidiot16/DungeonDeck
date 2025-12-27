using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 적 오브젝트에 붙이는 클릭/선택 표시 컴포넌트.
    /// - SpriteRenderer든 UI든 상관없이 "클릭 이벤트"만 들어오면 됨.
    /// - SpriteRenderer인 경우: Collider2D + Camera에 Physics2DRaycaster 필요.
    /// </summary>
    public class EnemyTargetView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Index (optional)")]
        [Tooltip("비워두면(=-1) 등록 순서대로 자동 인덱스가 배정됩니다.")]
        public int index = -1;

        [Header("Selection Visual")]
        [Tooltip("선택 링/하이라이트 오브젝트")]
        public GameObject selectedMarker;

        [Header("Popup/FX Target")]
        [Tooltip("HitPopup/FX가 뜰 위치. 비워두면 자기 transform 사용")]
        public Transform popupTarget;

        [Header("Manager (auto find if null)")]
        public BattleTargetManager manager;

        public int Index { get; private set; } = -1;

        private void Awake()
        {
            if (popupTarget == null) popupTarget = transform;
            if (selectedMarker != null) selectedMarker.SetActive(false);
        }

        private void OnEnable()
        {
            if (manager == null) manager = FindObjectOfType<BattleTargetManager>(true);
            if (manager != null) manager.Register(this);
        }

        private void OnDisable()
        {
            if (manager != null) manager.Unregister(this);
        }

        public void SetIndex(int i) => Index = i;

        public void SetSelected(bool on)
        {
            if (selectedMarker != null) selectedMarker.SetActive(on);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (manager == null) return;
            manager.Select(Index);
        }
    }
}