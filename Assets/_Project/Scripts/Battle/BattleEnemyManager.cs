// Assets/_Project/Scripts/Battle/BattleEnemyManager.cs
// v2: SlotIndex 기반 접근 메서드 추가
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 객체 참조 기반 적 관리자.
    /// BattleEnemy 인스턴스를 직접 관리하고, 슬롯 인덱스를 지원합니다.
    /// </summary>
    public class BattleEnemyManager
    {
        public event Action SelectionChanged;
        public event Action AllEnemiesDefeated;
        public event Action<BattleEnemy> EnemyDefeated;

        private readonly List<BattleEnemy> _enemies = new List<BattleEnemy>();
        private BattleEnemy _selected;

        // ─────────────────────────────────────────────────
        // Properties
        // ─────────────────────────────────────────────────
        public int Count => _enemies.Count;
        public BattleEnemy Selected => _selected;
        
        /// <summary>
        /// 선택된 적의 슬롯 인덱스 (리스트 인덱스가 아님)
        /// </summary>
        public int SelectedSlotIndex => _selected != null ? _selected.SlotIndex : 0;
        
        /// <summary>
        /// 선택된 적의 리스트 인덱스 (하위 호환용)
        /// </summary>
        public int SelectedIndex => _selected != null ? IndexOf(_selected) : 0;

        /// <summary>
        /// 모든 등록된 적을 순회합니다 (foreach 지원).
        /// </summary>
        public IEnumerable<BattleEnemy> All => _enemies;

        // ─────────────────────────────────────────────────
        // Registration (객체 기반)
        // ─────────────────────────────────────────────────
        public void Register(BattleEnemy enemy)
        {
            if (enemy == null) return;
            if (_enemies.Contains(enemy)) return;

            _enemies.Add(enemy);
            
            Debug.Log($"[BattleEnemyManager] Registered: {enemy.name} (SlotIndex={enemy.SlotIndex}, ListIndex={_enemies.Count - 1})");

            // 첫 번째 적이면 자동 선택
            if (_selected == null)
            {
                _selected = enemy;
                SelectionChanged?.Invoke();
            }
        }

        public void Unregister(BattleEnemy enemy)
        {
            if (enemy == null) return;
            if (!_enemies.Contains(enemy)) return;

            _enemies.Remove(enemy);

            // 선택된 적이 제거되면 다른 살아있는 적으로 전환
            if (_selected == enemy)
            {
                _selected = FindFirstAlive();
                SelectionChanged?.Invoke();
            }
        }

        // ─────────────────────────────────────────────────
        // Selection
        // ─────────────────────────────────────────────────
        /// <summary>
        /// 객체 참조로 적 선택
        /// </summary>
        public bool Select(BattleEnemy enemy)
        {
            if (enemy == null) return false;
            if (!_enemies.Contains(enemy)) return false;
            if (!enemy.IsAlive)
            {
                // 죽은 적 선택 시도 시 살아있는 적으로 보정
                var alt = FindFirstAlive();
                if (alt != null && alt != _selected)
                {
                    _selected = alt;
                    SelectionChanged?.Invoke();
                    return true;
                }
                return false;
            }

            if (_selected == enemy) return false;

            _selected = enemy;
            SelectionChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 슬롯 인덱스로 적 선택
        /// </summary>
        public bool SelectBySlot(int slotIndex)
        {
            var enemy = GetBySlot(slotIndex);
            if (enemy == null) return false;
            return Select(enemy);
        }

        /// <summary>
        /// 리스트 인덱스로 적 선택 (하위 호환용)
        /// </summary>
        public bool Select(int index)
        {
            var enemy = GetAt(index);
            if (enemy == null) return false;
            return Select(enemy);
        }

        // ─────────────────────────────────────────────────
        // Access - 리스트 인덱스 기반 (하위 호환)
        // ─────────────────────────────────────────────────
        /// <summary>
        /// 리스트 인덱스로 적 가져오기
        /// </summary>
        public BattleEnemy GetAt(int index)
        {
            if (index < 0 || index >= _enemies.Count) return null;
            return _enemies[index];
        }

        /// <summary>
        /// 적의 리스트 인덱스 반환 (-1 if not found)
        /// </summary>
        public int IndexOf(BattleEnemy enemy)
        {
            if (enemy == null) return -1;
            return _enemies.IndexOf(enemy);
        }

        // ─────────────────────────────────────────────────
        // Access - 슬롯 인덱스 기반 (신규)
        // ─────────────────────────────────────────────────
        /// <summary>
        /// 슬롯 인덱스로 적 가져오기
        /// </summary>
        public BattleEnemy GetBySlot(int slotIndex)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy != null && enemy.SlotIndex == slotIndex)
                    return enemy;
            }
            return null;
        }

        /// <summary>
        /// 적의 슬롯 인덱스 반환 (-1 if not found or enemy is null)
        /// </summary>
        public int SlotIndexOf(BattleEnemy enemy)
        {
            if (enemy == null) return -1;
            if (!_enemies.Contains(enemy)) return -1;
            return enemy.SlotIndex;
        }
        
        /// <summary>
        /// 슬롯 인덱스로 적 생존 여부 확인
        /// </summary>
        public bool IsAliveAtSlot(int slotIndex)
        {
            var enemy = GetBySlot(slotIndex);
            return enemy != null && enemy.IsAlive;
        }

        // ─────────────────────────────────────────────────
        // Query
        // ─────────────────────────────────────────────────
        public bool AreAllDefeated()
        {
            if (_enemies.Count == 0) return false;

            foreach (var enemy in _enemies)
            {
                if (enemy != null && enemy.IsAlive)
                    return false;
            }
            return true;
        }

        public BattleEnemy FindFirstAlive()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy != null && enemy.IsAlive)
                    return enemy;
            }
            return null;
        }

        public BattleEnemy FindNextAlive(BattleEnemy after)
        {
            if (_enemies.Count == 0) return null;

            int start = after != null ? IndexOf(after) : -1;
            int n = _enemies.Count;

            // after 다음부터 순회
            for (int step = 1; step <= n; step++)
            {
                int i = (start + step) % n;
                var enemy = _enemies[i];
                if (enemy != null && enemy.IsAlive)
                    return enemy;
            }
            return null;
        }
        
        /// <summary>
        /// 첫 번째 살아있는 적의 슬롯 인덱스 반환
        /// </summary>
        public int FindFirstAliveSlotIndex()
        {
            var first = FindFirstAlive();
            return first != null ? first.SlotIndex : 0;
        }

        // ─────────────────────────────────────────────────
        // Combat Events
        // ─────────────────────────────────────────────────
        /// <summary>
        /// 적이 사망했을 때 호출 (BattleEnemy.OnDefeated에서 호출)
        /// </summary>
        public void NotifyEnemyDefeated(BattleEnemy enemy)
        {
            if (enemy == null) return;

            EnemyDefeated?.Invoke(enemy);

            if (AreAllDefeated())
            {
                AllEnemiesDefeated?.Invoke();
            }
            else
            {
                // 선택된 적이 죽었으면 다음 살아있는 적으로 전환
                if (_selected == enemy)
                {
                    _selected = FindNextAlive(enemy) ?? FindFirstAlive();
                    SelectionChanged?.Invoke();
                }
            }
        }
        
        public void RefreshIntentAll(int fallbackDamage = 8)
        {
            foreach (var e in _enemies)
            {
                if (e == null || !e.IsAlive) continue;
                e.RefreshIntentPreview(fallbackDamage);
            }
        }

        // ─────────────────────────────────────────────────
        // Turn Tick
        // ─────────────────────────────────────────────────
        public void TickVulnerableAll()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy != null && enemy.IsAlive)
                    enemy.TickVulnerable();
            }
        }

        // ─────────────────────────────────────────────────
        // Cleanup
        // ─────────────────────────────────────────────────
        public void Clear()
        {
            _enemies.Clear();
            _selected = null;
        }
    }
}
