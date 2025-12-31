// Assets/_Project/Scripts/Battle/View/EnemyTargetView.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System;

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
        
        [Header("Intent Preview (optional)")]
        [Tooltip("인텐드 아이콘 이미지 (없으면 무시됩니다)")]
        [SerializeField] private Image intentIcon;
            
        [Tooltip("인텐드 아이콘 매핑 (intentId → Sprite)")]
        [SerializeField] private IntentSpriteEntry[] intentSprites;
            
        [Tooltip("데미지/횟수 등 프리뷰 텍스트 (없으면 무시됩니다)")]
        [SerializeField] private TMP_Text intentValueText;
            
        [Serializable]
        private struct IntentSpriteEntry
        {
            public string id;
            public Sprite sprite;
        }

        [Header("Manager (auto find if null)")]
        public BattleTargetManager targetManager;

        [Header("Auto Collider (click support)")]
        public bool autoAddCollider2D = true;

        private BattleEnemy _enemy;
        
        private bool _boundEnemyEvents;
        private bool _boundManagerEvents;
        
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

            BindManagerEvents(); // ✅ selection 링 이벤트 구독
            BindEnemyEvents();   // ✅ intent 프리뷰 이벤트 구독
            
            if (targetManager != null)
                targetManager.Register(this);
            
            RefreshIntentPreviewFromEnemy();
            
            // ✅ 현재 선택 상태 즉시 반영 (등록 타이밍에 따라 이벤트를 놓쳐도 안전)
            if (targetManager != null) 
                HandleSelectionChanged(targetManager.SelectedSlotIndex, targetManager.SelectedEnemy);
        }

        private void OnDisable()
        {
            UnbindEnemyEvents();
            UnbindManagerEvents();
            
            if (targetManager != null)
                targetManager.Unregister(this);
        }

        private void BindManagerEvents()
        {
            if (_boundManagerEvents) return;
            if (targetManager == null) return;
            targetManager.OnSelectionChanged += HandleSelectionChanged;
            _boundManagerEvents = true;
        }
    
        private void UnbindManagerEvents()
        {
            if (!_boundManagerEvents) return;
            if (targetManager != null)
                targetManager.OnSelectionChanged -= HandleSelectionChanged;
            _boundManagerEvents = false;
        }
        
        public void SetSelected(bool on)
        {
            if (selectedMarker != null)
                selectedMarker.SetActive(on);
        }

        /// <summary>
        /// 인텐드 프리뷰를 직접 세팅합니다. (id, value)
        /// </summary>
        public void SetIntentPreview(string intentId, int value)
        {
            if (intentIcon != null)
            {
                var sprite = ResolveIntentSprite(intentId);
                intentIcon.enabled = sprite != null;
                intentIcon.sprite = sprite;
            }
            
            if (intentValueText != null)
                intentValueText.text = value > 0 ? value.ToString() : string.Empty;
        }
    
        /// <summary>
        /// 연결된 BattleEnemy의 PlannedIntentId/PlannedDamage로 프리뷰를 갱신합니다.
        /// </summary>
        public void RefreshIntentPreviewFromEnemy()
        {
            if (_enemy == null)
            {
                SetIntentPreview(null, 0);
                return;
            }
            SetIntentPreview(_enemy.PlannedIntentId, _enemy.PlannedDamage);
        }
        
        private void BindEnemyEvents()
        {
            if (_boundEnemyEvents) return;
            if (_enemy == null) return;
            
            _enemy.OnStatsChanged += HandleEnemyStatsChanged;
            _enemy.OnDefeated += HandleEnemyDefeated;
            _boundEnemyEvents = true;
        }
    
        private void UnbindEnemyEvents()
        {
            if (!_boundEnemyEvents) return;
        
            if (_enemy != null)
            {
                _enemy.OnStatsChanged -= HandleEnemyStatsChanged;
                _enemy.OnDefeated -= HandleEnemyDefeated;
            }
        
            _boundEnemyEvents = false;
        }
    
        private void HandleEnemyStatsChanged(BattleEnemy enemy)
        {
            if (!isActiveAndEnabled) return;
            if (enemy != _enemy) return;
            RefreshIntentPreviewFromEnemy();
        }
    
        private void HandleEnemyDefeated(BattleEnemy enemy)
        {
            if (!isActiveAndEnabled) return;
            if (enemy != _enemy) return;
        
            // 죽은 적은 인텐드 프리뷰 비움 (선택 링 점프는 매니저가 처리)
            SetIntentPreview(null, 0);
        }
    
        private void HandleSelectionChanged(int selectedSlotIndex, BattleEnemy selectedEnemy)
        {
            // ✅ “선택 링도 매니저 Refresh 없이”: 이벤트만 받고 자기 링만 갱신
            SetSelected(selectedSlotIndex == SlotIndex);
        }
        
    
        private Sprite ResolveIntentSprite(string intentId)
        {
            if (string.IsNullOrWhiteSpace(intentId) || intentSprites == null) return null;
        
            for (int i = 0; i < intentSprites.Length; i++)
            {
                var e = intentSprites[i];
                if (!string.IsNullOrWhiteSpace(e.id) && e.id == intentId)
                    return e.sprite;
            }
            return null;
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
