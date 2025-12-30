// Assets/_Project/Scripts/Battle/View/EnemyTargetView.cs
using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 적 오브젝트에 붙이는 클릭/선택 표시 컴포넌트.
    /// BattleEnemy 컴포넌트와 함께 사용됩니다.
    /// 클릭 시 자신의 BattleEnemy를 타겟으로 지정합니다.
    /// </summary>
    [RequireComponent(typeof(BattleEnemy))]
    public class EnemyTargetView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Slot Index (fixed mapping)")]
        [Tooltip("0~2 고정 슬롯 인덱스. -1이면 Awake에서 siblingIndex로 자동 세팅.")]
        public int slotIndex = -1;

        [Header("Selection Visual")]
        [Tooltip("선택 링/하이라이트 오브젝트")]
        public GameObject selectedMarker;

        [Header("Manager (auto find if null)")]
        public BattleTargetManager targetManager;

        [Header("Auto Collider (click support)")]
        public bool autoAddCollider2D = true;

        private BattleEnemy _enemy;

        public int SlotIndex => slotIndex;
        public BattleEnemy Enemy => _enemy;
        public Transform PopupTarget => _enemy != null ? _enemy.PopupTarget : transform;

        private void Awake()
        {
            _enemy = GetComponent<BattleEnemy>();

            if (slotIndex < 0)
                slotIndex = transform.GetSiblingIndex();

            if (selectedMarker != null)
                selectedMarker.SetActive(false);

            EnsureCollider();
        }

        private void EnsureCollider()
        {
            if (GetComponentInChildren<Collider2D>() != null || GetComponentInChildren<Collider>() != null)
                return;

            if (!autoAddCollider2D) return;

            var sr = GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null) return;

            var host = sr.gameObject;
            if (host.GetComponent<Collider2D>() != null) return;

            var bc = host.AddComponent<BoxCollider2D>();
            if (sr.sprite != null)
            {
                bc.size = sr.sprite.bounds.size;
                bc.offset = sr.sprite.bounds.center;
            }
        }

        private void OnEnable()
        {
            if (targetManager == null)
                targetManager = FindObjectOfType<BattleTargetManager>(true);

            if (targetManager != null)
                targetManager.Register(this);
        }

        private void OnDisable()
        {
            if (targetManager != null)
                targetManager.Unregister(this);
        }

        public void SetSelected(bool on)
        {
            if (selectedMarker != null)
                selectedMarker.SetActive(on);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (targetManager == null) return;
            if (_enemy == null) return;

            // “BattleEnemy.IsAlive”가 상태 기반 컨트롤러와 불일치할 수 있으니
            // 최종 검증은 TargetManager에서 한 번 더 해준다.
            targetManager.Select(_enemy);
        }
    }
}
