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
        [Tooltip("선택 링/하이라이트 오브젝트 (🎯 단일 대상)")]
        public GameObject selectedMarker;

        [Tooltip("광역 표시 오브젝트 (🌐 전체 대상 / 🎯+🌐 혼합)")]
        public GameObject aoeMarker;
        
        [Header("Intent Preview (optional)")]
        [Tooltip("인텐드 아이콘 이미지 (없으면 무시됩니다)")]
        [SerializeField] private Image intentIcon;
            
        [Tooltip("인텐드 아이콘 매핑 (intentId → Sprite)")]
        [SerializeField] private IntentSpriteEntry[] intentSprites;
            
        [Tooltip("데미지/횟수 등 프리뷰 텍스트 (없으면 무시됩니다)")]
        [SerializeField] private TMP_Text intentValueText;
        
        [Header("Intent Theme (optional)")]
        [Tooltip("인텐트 타입별 색상(아이콘/숫자 틴트). 공격=빨강, 방어=파랑, 디버프=보라 기본값")]
        [SerializeField] private bool tintIntentUI = true;
            
        [SerializeField] private Color attackColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color defendColor = new Color(0.35f, 0.65f, 1f, 1f);
        [SerializeField] private Color debuffColor = new Color(0.85f, 0.45f, 1f, 1f);
        [SerializeField] private Color neutralColor = Color.white;
            
        [Tooltip("value가 0일 때 빈칸으로 레이아웃이 흔들리면 공백 한 칸을 유지합니다.")]
        [SerializeField] private bool keepValueSpaceWhenZero = true;
        
        
            
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
        
        public int SlotIndex => _enemy != null ? _enemy.SlotIndex : slotIndex;
        public BattleEnemy Enemy => _enemy;
        public Transform PopupTarget => _enemy != null ? _enemy.PopupTarget : transform;

        private void Awake()
        {
            _enemy = GetComponent<BattleEnemy>();

            if (slotIndex < 0)
                slotIndex = transform.GetSiblingIndex();

            if (selectedMarker != null)
                selectedMarker.SetActive(false);
            
            if (aoeMarker != null)
                aoeMarker.SetActive(false);

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
            {
                if (value > 0) intentValueText.text = value.ToString();
                else intentValueText.text = keepValueSpaceWhenZero ? " " : string.Empty;
            }
        }
   
        private void ApplyIntentTheme(string intentId)
        {
            if (!tintIntentUI) return;
            
            var kind = ResolveIntentKind(intentId);
            var c = kind switch
            {
                IntentKind.Attack => attackColor,
                IntentKind.Defend => defendColor,
                IntentKind.Debuff => debuffColor,
                _ => neutralColor
            };
            
            if (intentIcon != null) intentIcon.color = c;
            if (intentValueText != null) intentValueText.color = c;
        }
    
        private enum IntentKind { None, Attack, Defend, Debuff, Other }
    
        private static IntentKind ResolveIntentKind(string intentId)
        {
            if (string.IsNullOrWhiteSpace(intentId))
                return IntentKind.None;
        
            // 소문자/공백 제거
            var id = intentId.Trim().ToLowerInvariant();
        
            // ✅ MVP 기준 키워드: atk / def / debuff
            // 확장 대비: attack / block / shield / buff / weaken 등도 흡수
            if (id == "atk" || id == "attack" || id.Contains("atk") || id.Contains("attack"))
                return IntentKind.Attack;
            if (id == "def" || id.Contains("def") || id.Contains("block") || id.Contains("shield"))
                return IntentKind.Defend;
            if (id == "debuff" || id.Contains("debuff") || id.Contains("vuln") || id.Contains("weak"))
                return IntentKind.Debuff;
        
            return IntentKind.Other;
        }
        
        public void SetAoe(bool on)
        {
            if (aoeMarker != null)
                aoeMarker.SetActive(on);
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
            SetIntentPreview(_enemy.PlannedIntentId, _enemy.PlannedIntentValue);
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
            
            // ✅ 즉시 마커 정리 (매니저 브로드캐스트 전 한 프레임 잔상 방지)
            SetSelected(false);
            SetAoe(false);
        }
    
        private void HandleSelectionChanged(int selectedSlotIndex, BattleEnemy selectedEnemy)
        {
            var mode = (targetManager != null) ? targetManager.TargetingMode
                : BattleTargetManager.CardTargetingMode.None;

            bool alive = _enemy != null && _enemy.IsAlive;
            bool isSelectedSlot = (selectedSlotIndex == SlotIndex) && alive;

            // 기본값
            SetAoe(false);
            SetSelected(false);

            switch (mode)
            {
                // 🌐 전체 대상: 살아있는 적 전원 강조(aoeMarker), 단일 선택 링은 숨김
                case BattleTargetManager.CardTargetingMode.AllEnemiesOnly:
                {
                    SetAoe(alive);
                    SetSelected(false);
                    break;
                }

                // 🎯+🌐 혼합: 전체 강조 + 선택된 적만 추가 강조
                case BattleTargetManager.CardTargetingMode.Mixed:
                {
                    SetAoe(alive);
                    SetSelected(isSelectedSlot);
                    break;
                }

                // 🎯 단일 대상 요구 / None: 기존 단일 선택 링만
                case BattleTargetManager.CardTargetingMode.RequireSingleEnemy:
                case BattleTargetManager.CardTargetingMode.None:
                default:
                {
                    SetAoe(false);
                    SetSelected(isSelectedSlot);
                    break;
                }
            }
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
            Debug.Log($"[ETV.Click] view={name}, _enemy={_enemy?.name}, _enemy.SlotIndex={_enemy?.SlotIndex}, this.slotIndex={slotIndex}");
            
            if (targetManager != null && targetManager.IsSelectionLocked)
                return;
            if (_enemy == null) return;

            // “BattleEnemy.IsAlive”가 상태 기반 컨트롤러와 불일치할 수 있으니
            // 최종 검증은 TargetManager에서 한 번 더 해준다.
            targetManager.Select(_enemy);
        }
    }
}
