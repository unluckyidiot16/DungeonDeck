// Assets/_Project/Scripts/Battle/BattleEnemy.cs
using System;
using UnityEngine;
using DungeonDeck.Config.Enemies;
using DungeonDeck.Run;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 각 적 오브젝트에 붙는 컴포넌트.
    /// - EnemyDefinition(SO) + EnemyRuntimeState(런타임) 보유
    /// - Init(def) 또는 Initialize(run, orderIndex)에서 state 생성/리셋
    /// </summary>
    public class BattleEnemy : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private EnemyDefinition definition;

        [Header("Runtime State (readonly in inspector)")]
        [SerializeField] private EnemyRuntimeState state;

        [Header("View References")]
        [Tooltip("팝업/FX가 뜰 위치. 비워두면 자기 transform 사용")]
        public Transform popupTarget;

        [Header("Slot")]
        [SerializeField] private int slotIndex = -1;
        public int SlotIndex => slotIndex >= 0 ? slotIndex : Mathf.Clamp(transform.GetSiblingIndex(), 0, 2);
        
        
        [Header("Auto Registration")]
        [Tooltip("활성화 시 BattleController에 자동 등록")]
        [SerializeField] private bool autoRegister = true;

        private BattleController _battleController;

        // ─────────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────────
        public event Action<BattleEnemy> OnDefeated;
        public event Action<BattleEnemy> OnStatsChanged;

        // ─────────────────────────────────────────────────
        // Properties
        // ─────────────────────────────────────────────────
        public EnemyDefinition Definition => definition;
        public EnemyRuntimeState State => state;

        public int HP => state != null ? state.HP : 0;
        public int MaxHP => state != null ? state.MaxHP : 0;
        public int Block => state != null ? state.Block : 0;
        public int VulnerableTurns => state != null ? state.VulnerableTurns : 0;
        public bool IsAlive => state != null && state.IsAlive;

        public Transform PopupTarget => popupTarget != null ? popupTarget : transform;

        // ─────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────
        private void Awake()
        {
            if (popupTarget == null) popupTarget = transform;

            // state는 전투 시작 시 Init에서 생성해도 되지만,
            // 씬에서 미리 세팅된 오브젝트라면 null 방어용으로 한 번 만들어둠.
            if (state == null) state = new EnemyRuntimeState();
        }

        private void OnEnable()
        {
            if (autoRegister)
            {
                if (_battleController == null)
                    _battleController = FindObjectOfType<BattleController>(true);

                _battleController?.RegisterEnemy(this);
            }
        }

        private void OnDisable()
        {
            if (autoRegister && _battleController != null)
            {
                _battleController.UnregisterEnemy(this);
            }
        }

        // ─────────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────────
        /// <summary>
        /// SO 정의를 주입하고 런타임 상태를 리셋합니다.
        /// </summary>
        public void Init(EnemyDefinition def, int orderIndex = 0)
        {
            definition = def;
            slotIndex = orderIndex;
            
            if (state == null) state = new EnemyRuntimeState();
            state.ResetFromDefinition(definition, orderIndex);

            OnStatsChanged?.Invoke(this);
        }

        /// <summary>
        /// RunSession 기반 초기화 (BattleController에서 호출).
        /// definition이 있으면 SO 기반, 없으면 기본값 사용.
        /// </summary>
        public void Initialize(RunSession run, int orderIndex = 0)
        {
            if (state == null) state = new EnemyRuntimeState();

            // 슬롯 인덱스는 "등록 순서" 기준으로 고정 (StageSpawner가 0→1→2 순서로 활성화)
            slotIndex = orderIndex;
            
            if (definition != null)
            {
                // SO 기반 (정석)
                state.ResetFromDefinition(definition, orderIndex);
            }
            else
            {
                // 폴백: SO 없이 씬에 배치된 적용 기본값
                // TODO: 모든 적에 EnemyDefinition 할당 후 이 분기 제거
                Debug.LogWarning($"[BattleEnemy] {name} has no EnemyDefinition. Using fallback stats.");
                state.ResetWithValues(maxHealth: 30, attack: 0);
            }

            OnStatsChanged?.Invoke(this);
        }

        // ─────────────────────────────────────────────────
        // Combat Actions (state 래핑)
        // ─────────────────────────────────────────────────
        public int TakeDamage(int rawAmount)
        {
            if (state == null || !state.IsAlive) return 0;

            int before = state.HP;
            int dealt = state.TakeDamage(rawAmount);

            if (dealt > 0 || before != state.HP)
                OnStatsChanged?.Invoke(this);

            if (!state.IsAlive)
            {
                OnDefeated?.Invoke(this);

                // BattleController에도 알림
                if (_battleController == null)
                    _battleController = FindObjectOfType<BattleController>(true);
                // Note: BattleController가 EnemyDefeated 이벤트를 직접 구독하므로
                // 별도 알림 불필요
            }

            return dealt;
        }

        public void Heal(int amount)
        {
            if (state == null || !state.IsAlive) return;
            state.Heal(amount);
            OnStatsChanged?.Invoke(this);
        }

        public void AddBlock(int amount)
        {
            if (state == null || !state.IsAlive) return;
            state.AddBlock(amount);
            OnStatsChanged?.Invoke(this);
        }

        public void ApplyVulnerable(int turns)
        {
            if (state == null || !state.IsAlive) return;
            state.ApplyVulnerable(turns);
            OnStatsChanged?.Invoke(this);
        }

        public void TickVulnerable()
        {
            if (state == null) return;
            state.TickVulnerable();
            OnStatsChanged?.Invoke(this);
        }

        // ─────────────────────────────────────────────────
        // Wiring Helper
        // ─────────────────────────────────────────────────
        public void SetBattleController(BattleController controller)
        {
            _battleController = controller;
        }
        
        /// <summary>
        /// 스폰 시 고정 슬롯 인덱스 강제 세팅용
        /// </summary>
        public void SetSlotIndex(int idx)
        { 
            slotIndex = Mathf.Clamp(idx, 0, 2);
        }
        
        
    }
}
