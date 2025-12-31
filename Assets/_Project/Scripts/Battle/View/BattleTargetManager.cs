// Assets/_Project/Scripts/Battle/View/BattleTargetManager.cs
// v2: 슬롯 인덱스 기반 선택 로직 수정
using System;
using UnityEngine;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// EnemyTargetView들을 슬롯(0~2)으로 고정 관리하고,
    /// 선택 변경을 BattleController(상태)와 동기화한다.
    /// 슬롯 인덱스 기반 API 사용.
    /// </summary>
    public class BattleTargetManager : MonoBehaviour
    {
        public const int MaxSlots = 3;
        
        /// <summary>
        /// ✅ 선택 변경 이벤트 (선택 링/UI가 매니저 Refresh 없이 갱신되게)
        /// args: (selectedSlotIndex, selectedEnemy)
        /// </summary>
        public event Action<int, BattleEnemy> OnSelectionChanged;

        [Header("Wiring")]
        public BattleController battle;

        private readonly EnemyTargetView[] _slots = new EnemyTargetView[MaxSlots];
        private readonly BattleEnemy[] _slotEnemies = new BattleEnemy[MaxSlots];
        
        private int _selectedSlotIndex = -1;
        private BattleEnemy _selectedEnemy;

        public int SelectedIndex => _selectedSlotIndex;
        public int SelectedSlotIndex => _selectedSlotIndex; // ✅ alias: EnemyTargetView에서 쓰는 이름

        public BattleEnemy SelectedEnemy => _selectedEnemy;

        private void Awake()
        {
            if (battle == null)
                battle = FindObjectOfType<BattleController>(true);
        }

        // ─────────────────────────────────────────────────
        // Registration
        // ─────────────────────────────────────────────────
        public void Register(EnemyTargetView view)
        {
            if (view == null) return;

            int idx = Mathf.Clamp(view.SlotIndex, 0, MaxSlots - 1);
            _slots[idx] = view;
            HookEnemy(idx, view.Enemy);
            
            Debug.Log($"[BattleTargetManager] Registered: {view.name} at slot {idx}");

            if (_selectedSlotIndex < 0)
                SetSelectedSlotInternal(FindFirstAliveSlotIndex(), notifyBattle: true);
            else
                BroadcastSelectionChanged();
        }

        public void Unregister(EnemyTargetView view)
        {
            if (view == null) return;

            int idx = Mathf.Clamp(view.SlotIndex, 0, MaxSlots - 1);
            if (_slots[idx] == view)
                _slots[idx] = null;
            
            HookEnemy(idx, null);

            // 선택이 빠졌으면 다음 살아있는 슬롯으로
            if (_selectedSlotIndex == idx)
                SetSelectedSlotInternal(FindFirstAliveSlotIndex(), notifyBattle: true);
            else
                BroadcastSelectionChanged();
        }
        
        // ─────────────────────────────────────────────────
        // Selection (객체 기반)
        // ─────────────────────────────────────────────────
        /// <summary>
        /// BattleEnemy 객체로 선택 (클릭 등에서 사용)
        /// </summary>
        public void Select(BattleEnemy enemy)
        {
            if (enemy == null) return;
            if (battle == null) battle = FindObjectOfType<BattleController>(true);

            // 어떤 슬롯의 enemy인지 찾아서 선택
            int slotIdx = SlotIndexOf(enemy);
            if (slotIdx < 0)
            {
                Debug.LogWarning($"[BattleTargetManager] Select failed: enemy {enemy.name} not found in slots");
                return;
            }

            // ✅ 적 객체의 생존 여부로 직접 체크 (EnemyCount 비교 제거)
            if (!enemy.IsAlive)
            {
                Debug.Log($"[BattleTargetManager] Select failed: enemy {enemy.name} is not alive");
                return;
            }

            _selectedEnemy = enemy;
            SetSelectedSlotInternal(slotIdx, notifyBattle: true);
        }

        /// <summary>
        /// BattleController에서 객체 기반으로 선택 설정 (이벤트 루프 방지용)
        /// </summary>
        public void SetSelectedFromBattle(BattleEnemy enemy)
        {
            if (enemy == null) return;

            int slotIdx = SlotIndexOf(enemy);
            if (slotIdx < 0) return;

            _selectedEnemy = enemy;
            SetSelectedSlotInternal(slotIdx, notifyBattle: false);
        }

        /// <summary>
        /// 슬롯 인덱스로 선택 설정 (BattleController에서 호출)
        /// </summary>
        public void SetSelectedIndexFromBattle(int slotIndex)
        {
            SetSelectedSlotInternal(slotIndex, notifyBattle: false);
        }

        /// <summary>
        /// UI 갱신 강제
        /// </summary>
        public void Refresh()
        {
            // ✅ 이제 “링/UI 갱신”은 이벤트 기반
            BroadcastSelectionChanged();
        }

        // ─────────────────────────────────────────────────
        // Internal
        // ─────────────────────────────────────────────────
        private void SetSelectedSlotInternal(int slotIndex, bool notifyBattle)
        {
            int clamped = Mathf.Clamp(slotIndex, 0, MaxSlots - 1);

            // 해당 슬롯에 적이 없거나 죽었으면 첫 alive로
            if (!IsSlotAlive(clamped))
                clamped = FindFirstAliveSlotIndex();

            _selectedSlotIndex = clamped;
            
            // 슬롯에서 enemy 참조 갱신
            if (_selectedSlotIndex >= 0 && _selectedSlotIndex < MaxSlots && _slots[_selectedSlotIndex] != null)
                _selectedEnemy = _slots[_selectedSlotIndex].Enemy;
            else
                _selectedEnemy = null;
            
            // ✅ 선택 변경 브로드캐스트 (EnemyTargetView가 링을 갱신)
            BroadcastSelectionChanged();

            if (notifyBattle && battle != null && _selectedSlotIndex >= 0)
            {
                // ✅ 슬롯 인덱스 기반 선택
                if (_selectedEnemy != null)
                    battle.SelectEnemy(_selectedEnemy);
            }
        }

        /// <summary>
        /// ✅ 선택 변경 이벤트 브로드캐스트
        /// </summary>
        private void BroadcastSelectionChanged()
        {
            OnSelectionChanged?.Invoke(_selectedSlotIndex, _selectedEnemy);
        }
        
        private void HookEnemy(int slotIndex, BattleEnemy enemy)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return;
            
            var prev = _slotEnemies[slotIndex];
            if (prev != null)
                prev.OnDefeated -= HandleEnemyDefeated;
            
            _slotEnemies[slotIndex] = enemy;
            
            if (enemy != null)
                enemy.OnDefeated += HandleEnemyDefeated;
        }
    
        private void HandleEnemyDefeated(BattleEnemy enemy)
        {
            if (enemy == null) return;
        
            // ✅ 선택된 적이 죽으면 자동으로 다음 살아있는 슬롯로 점프
            if (_selectedEnemy == enemy || !IsSlotAlive(_selectedSlotIndex))
            {
                int next = FindFirstAliveSlotIndex();
                SetSelectedSlotInternal(next, notifyBattle: true);
            }
        }

        /// <summary>
        /// 첫 번째 살아있는 슬롯 인덱스 찾기
        /// </summary>
        private int FindFirstAliveSlotIndex()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (IsSlotAlive(i))
                    return i;
            }
            return 0;
        }
        
        /// <summary>
        /// 해당 슬롯의 적이 살아있는지 확인
        /// </summary>
        private bool IsSlotAlive(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return false;
            
            var view = _slots[slotIndex];
            if (view == null) return false;
            
            var enemy = view.Enemy;
            return enemy != null && enemy.IsAlive;
        }

        /// <summary>
        /// 적 객체의 슬롯 인덱스 찾기
        /// </summary>
        private int SlotIndexOf(BattleEnemy enemy)
        {
            if (enemy == null) return -1;
            
            for (int i = 0; i < MaxSlots; i++)
            {
                var v = _slots[i];
                if (v == null) continue;
                if (v.Enemy == enemy) return i;
            }
            return -1;
        }
        
        /// <summary>
        /// 리스트 인덱스가 아닌 슬롯 인덱스로 적 찾기 (하위 호환용 제거)
        /// </summary>
        [System.Obsolete("Use SlotIndexOf instead")]
        private int IndexOf(BattleEnemy enemy)
        {
            return SlotIndexOf(enemy);
        }
    }
}
