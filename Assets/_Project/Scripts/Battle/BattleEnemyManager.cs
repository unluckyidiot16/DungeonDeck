// Assets/_Project/Scripts/Battle/BattleEnemyManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Run;
using DungeonDeck.Config.Map;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 적 상태 관리 전담.
    /// BattleController에서 분리된 단일 책임 클래스.
    /// </summary>
    [Serializable]
    public class EnemyState
    {
        public int hp;
        public int maxHp;
        public int block;
        public int vulnerableTurns;

        public bool IsAlive => hp > 0;

        public EnemyState(int hp, int maxHp)
        {
            this.hp = hp;
            this.maxHp = maxHp;
            this.block = 0;
            this.vulnerableTurns = 0;
        }
    }

    public class BattleEnemyManager
    {
        private readonly List<EnemyState> _enemies = new();
        private int _selectedIndex = 0;

        public event Action SelectionChanged;
        public event Action<int> EnemyDefeated;
        public event Action AllEnemiesDefeated;

        // ─────────────────────────────────────────
        // Properties
        // ─────────────────────────────────────────
        public int Count => _enemies.Count;
        public int SelectedIndex => _selectedIndex;
        public EnemyState Selected => GetAt(_selectedIndex);

        public EnemyState GetAt(int index)
        {
            if (index < 0 || index >= _enemies.Count) return null;
            return _enemies[index];
        }

        public IReadOnlyList<EnemyState> All => _enemies;

        // ─────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────
        public void Initialize(int count, RunSession run)
        {
            _enemies.Clear();
            count = Mathf.Clamp(count, 1, 3);

            bool isBoss = run != null && run.PendingBattleType == MapNodeType.Boss;
            if (isBoss) count = 1;

            for (int i = 0; i < count; i++)
            {
                _enemies.Add(CreateEnemy(run, i, isBoss));
            }

            _selectedIndex = 0;
        }

        public void EnsureCount(int count, RunSession run)
        {
            count = Mathf.Clamp(count, 1, 3);

            bool isBoss = run != null && run.PendingBattleType == MapNodeType.Boss;
            if (isBoss) count = 1;

            if (_enemies.Count == count) return;

            while (_enemies.Count < count)
                _enemies.Add(CreateEnemy(run, _enemies.Count, isBoss));

            while (_enemies.Count > count)
                _enemies.RemoveAt(_enemies.Count - 1);

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _enemies.Count - 1);
        }

        private EnemyState CreateEnemy(RunSession run, int index, bool isBoss)
        {
            int baseHp = isBoss ? 60 : 30;
            int hp = isBoss ? baseHp : Mathf.Max(10, baseHp - index * 5);
            return new EnemyState(hp, hp);
        }

        // ─────────────────────────────────────────
        // Selection
        // ─────────────────────────────────────────
        public bool Select(int index)
        {
            if (_enemies.Count == 0) return false;

            int newIndex = Mathf.Clamp(index, 0, _enemies.Count - 1);
            if (newIndex == _selectedIndex) return false;

            _selectedIndex = newIndex;
            SelectionChanged?.Invoke();
            return true;
        }

        public void AutoSelectNextAlive()
        {
            var current = GetAt(_selectedIndex);
            if (current != null && current.IsAlive) return;

            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null && _enemies[i].IsAlive)
                {
                    _selectedIndex = i;
                    SelectionChanged?.Invoke();
                    return;
                }
            }

            _selectedIndex = 0;
        }

        // ─────────────────────────────────────────
        // Combat Actions
        // ─────────────────────────────────────────
        public int DealDamage(int enemyIndex, int rawAmount)
        {
            var enemy = GetAt(enemyIndex);
            if (enemy == null || !enemy.IsAlive) return 0;

            int amount = Mathf.Max(0, rawAmount);

            // 취약 적용
            if (enemy.vulnerableTurns > 0 && amount > 0)
                amount = Mathf.CeilToInt(amount * 1.5f);

            int hpBefore = enemy.hp;

            // 블록 먼저 소모
            int remain = amount;
            if (enemy.block > 0)
            {
                int used = Mathf.Min(enemy.block, remain);
                enemy.block -= used;
                remain -= used;
            }

            // 남은 데미지 HP에 적용
            if (remain > 0) enemy.hp -= remain;
            if (enemy.hp < 0) enemy.hp = 0;

            int hpLoss = Mathf.Max(0, hpBefore - enemy.hp);

            // 사망 체크
            if (!enemy.IsAlive)
            {
                EnemyDefeated?.Invoke(enemyIndex);

                if (enemyIndex == _selectedIndex)
                    AutoSelectNextAlive();

                if (AreAllDefeated())
                    AllEnemiesDefeated?.Invoke();
            }

            return hpLoss;
        }

        public void ApplyVulnerable(int enemyIndex, int turns)
        {
            var enemy = GetAt(enemyIndex);
            if (enemy == null) return;

            enemy.vulnerableTurns = Mathf.Clamp(enemy.vulnerableTurns + turns, 0, 99);
        }

        public void TickVulnerableAll()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e != null && e.vulnerableTurns > 0)
                    e.vulnerableTurns -= 1;
            }
        }

        public void ClearBlockAll()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null)
                    _enemies[i].block = 0;
            }
        }

        // ─────────────────────────────────────────
        // Queries
        // ─────────────────────────────────────────
        public bool AreAllDefeated()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null && _enemies[i].IsAlive)
                    return false;
            }
            return true;
        }

        public int CountAlive()
        {
            int n = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null && _enemies[i].IsAlive)
                    n++;
            }
            return n;
        }

        public int GetHP(int index) => GetAt(index)?.hp ?? 0;
        public int GetMaxHP(int index) => GetAt(index)?.maxHp ?? 0;
        public int GetBlock(int index) => GetAt(index)?.block ?? 0;
        public int GetVulnerableTurns(int index) => GetAt(index)?.vulnerableTurns ?? 0;
    }
}
