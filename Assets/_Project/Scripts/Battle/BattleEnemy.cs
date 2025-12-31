// Assets/_Project/Scripts/Battle/BattleEnemy.cs
using System;
using DungeonDeck.Config.Encounters;
using UnityEngine;
using DungeonDeck.Config.Encounters;
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

        public int ATK => state != null ? state.ATK : 0;
        public EnemyPatternDefinition Pattern => pattern;
        
        // ─────────────────────────────────────────────────
        // Pattern Runtime (Intent Preview)
        // ─────────────────────────────────────────────────
        [Header("Pattern Runtime")]
        [SerializeField] private EnemyPatternDefinition pattern;
        [SerializeField] private int patternStepIndex = 0;
        [SerializeField] private string plannedIntentId;
        [SerializeField] private int plannedDamage;
            
        private bool SetPlannedIntent(string intentId, int damage)
        {
            // ✅ 규칙 통일: "값이 변할 때만" 변경 처리 (그리고 필요 시에만 이벤트 발행)
            if (plannedIntentId == intentId && plannedDamage == damage) return false;
            plannedIntentId = intentId;
            plannedDamage = damage;
            return true;
        }
        
        public string PlannedIntentId => plannedIntentId;
        public int PlannedDamage => plannedDamage;
        
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

        /// <summary>
        /// ✅ StageSpawner에서 호출하는 “정식 초기화”
        /// </summary>
        public void Init(EnemyDefinition def, int slotIndex, BattleEncounterDefinition encounter)
        {
            definition = def;
            if (state == null) state = new EnemyRuntimeState();
            
            state.ResetFromDefinition(definition, slotIndex);
            
            // 패턴 연결 (정의에 있으면 우선 적용)
            if (definition != null && definition.defaultPattern != null)
                pattern = definition.defaultPattern;
            
            patternStepIndex = 0;
            
            // 슬롯 인덱스는 바인딩/타겟의 기준이므로 반드시 고정
            this.slotIndex = slotIndex;

            state.def = definition;
            state.slotIndex = slotIndex;

            if (definition == null)
            {
                // 빈 슬롯이면 그냥 비활성 상태로 두는 게 안전
                state.ResetWithValues(1, 1, 0);
                pattern = null;
                patternStepIndex = 0;
                SetPlannedIntent(null, 0);
                OnStatsChanged?.Invoke(this);
                return;
            }

            var slot = (encounter != null) ? encounter.GetResolvedSlotAt(slotIndex) : default;

            int power = (slot.powerOverride > 0) ? slot.powerOverride : definition.GetBasePowerSafe();
            var profile = (slot.overrideProfile) ? slot.profileOverride : definition.defaultProfile;

            definition.ComputeBaseStats(slotIndex, power, profile, out int maxHp, out int atk);

            // ✅ HP/ATK 반영
            state.ResetWithValues(maxHp, maxHp, atk);

            // ✅ 패턴 주입
            pattern = (slot.patternOverride != null) ? slot.patternOverride : definition.defaultPattern;
            patternStepIndex = 0;

            // (선택) 애니메이션 override도 여기서 같이 주입 가능
            var aoc = (slot.animationOverride != null) ? slot.animationOverride : definition.animationOverride;
            ApplyAnimatorOverride(aoc);
            
            // ✅ 초기 Intent 값도 최신 스탯/패턴 기준으로 맞춰둔다 (Init 마지막 OnStatsChanged에서 UI 갱신)
            RefreshIntentPreview(fireEvent: false);

            OnStatsChanged?.Invoke(this);
        }

        private void ApplyAnimatorOverride(AnimatorOverrideController aoc)
        {
            if (aoc == null) return;
            var anim = GetComponentInChildren<Animator>(true);
            if (anim == null) return;

            if (anim.runtimeAnimatorController != aoc)
                anim.runtimeAnimatorController = aoc;
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
                
                // 패턴 연결 (정의에 있으면 우선 적용)
                if (definition != null && definition.defaultPattern != null)
                    pattern = definition.defaultPattern;
                patternStepIndex = 0;
                RefreshIntentPreview(fireEvent: false);
                
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
            
            int beforeHp = state.HP;
            int beforeBlock = state.Block;
            int dealt = state.TakeDamage(rawAmount);

            bool diedNow = !state.IsAlive;
            bool statsChanged = (dealt != 0 || beforeHp != state.HP || beforeBlock != state.Block);


            if (diedNow)
            {
                
                // ✅ 사망 시 intent 프리뷰 정리 (규칙 통일: SetPlannedIntent로 변경 여부 판단)
                if (SetPlannedIntent(null, 0))
                    statsChanged = true;
            }
            
            // ✅ 스탯 변경 이벤트는 1회만 발행
            if (statsChanged)
                OnStatsChanged?.Invoke(this);
            
            if (diedNow)
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
            if (amount <= 0) return;
            
            int beforeHp = state.HP;
            state.Heal(amount);
            
            // ✅ 실제로 HP가 변했을 때만 이벤트 발행
            if (state.HP != beforeHp)
                OnStatsChanged?.Invoke(this);
        }

        public void AddBlock(int amount)
        {
            if (state == null || !state.IsAlive) return;
            if (amount == 0) return;
            
            int beforeBlock = state.Block;
            state.AddBlock(amount);
            
            // ✅ 실제로 Block이 변했을 때만 이벤트 발행
            if (state.Block != beforeBlock)
                OnStatsChanged?.Invoke(this);
        }

        public void ApplyVulnerable(int turns)
        {
            if (state == null || !state.IsAlive) return;
            if (turns <= 0) return;
            
            int before = state.VulnerableTurns;
            state.ApplyVulnerable(turns);
            
            // ✅ 실제로 VulnerableTurns가 변했을 때만 이벤트 발행
            if (state.VulnerableTurns != before)
                OnStatsChanged?.Invoke(this);
        }

        public void TickVulnerable()
        {
            if (state == null) return;
            
            int before = state.VulnerableTurns;
            
            state.TickVulnerable();
            // ✅ tick 결과로 값이 바뀐 경우에만 이벤트 발행
            if (state.VulnerableTurns != before)
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
        
        
        // <summary>
        /// 현재 패턴 스텝 기준으로 Intent(의도) 프리뷰 값을 갱신합니다.
        /// BattleEnemyManager.RefreshIntentAll()에서 턴 시작 시 호출됩니다.
        /// </summary>
        public void RefreshIntentPreview(int fallbackDamage = 8, bool fireEvent = true)
        {
            
            if (!IsAlive)
            {
                bool changedDead = SetPlannedIntent(null, 0);
                if (fireEvent && changedDead)
                    OnStatsChanged?.Invoke(this);
                return;
            }
            
            // ✅ 기본 데미지 기준: 내 ATK 우선, 없으면 fallback
            int baseDmg = ATK > 0 ? ATK : Mathf.Max(0, fallbackDamage);

            string nextId;
            int nextDmg;
            
            if (pattern != null && pattern.StepCount > 0)
            {
                // EnemyPatternDefinition.Step 는 struct일 가능성이 높아서 null 비교 불가
                int idx = Mathf.Clamp(patternStepIndex, 0, pattern.StepCount - 1);
                var step = pattern.GetStep(idx);
                
                nextId = string.IsNullOrWhiteSpace(step.intentId) ? "atk" : step.intentId;
                int dmg = step.EvalDamage(baseDmg);
                nextDmg = dmg > 0 ? dmg : baseDmg;
            }
            else
            {
                nextId = "atk";
                nextDmg = baseDmg;
            }
            
            // ✅ 규칙 통일: "값이 변할 때만" 이벤트 발행
            bool changed = SetPlannedIntent(nextId, nextDmg);
            if (fireEvent && changed)
                OnStatsChanged?.Invoke(this);
        }
    
        /// <summary>
        /// 적 행동(공격) 후 패턴 스텝을 1칸 진행시키고, 다음 프리뷰로 갱신합니다.
        /// BattleController.EnemyAttackPhaseCo()에서 호출됩니다.
        /// </summary>
        public void AdvancePatternStep(int fallbackDamage = 8)
        {
            if (pattern != null && pattern.StepCount > 0)
            {
                if (pattern.loop)
                    patternStepIndex = (patternStepIndex + 1) % pattern.StepCount;
                else
                    patternStepIndex = Mathf.Min(patternStepIndex + 1, pattern.StepCount - 1);
            }
        
            // 다음 턴(또는 즉시 UI)에 보일 값 갱신
            RefreshIntentPreview(fallbackDamage);
        }
        
    }
}
