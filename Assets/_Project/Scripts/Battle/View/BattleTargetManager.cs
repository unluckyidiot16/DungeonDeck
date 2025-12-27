using System.Collections.Generic;
using UnityEngine;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 현재 선택된 적 타겟 관리.
    /// - EnemyTargetView들이 스스로 Register
    /// - Select 시: BattleController.SelectEnemy(index) 호출 + 하이라이트 갱신
    /// - (옵션) HitPopupSpawner의 enemyTarget도 선택된 적 popupTarget으로 교체
    /// </summary>
    public class BattleTargetManager : MonoBehaviour
    {
        [Header("Refs (auto find if null)")]
        public DungeonDeck.Battle.BattleController battle;
        public HitPopupSpawner hitPopups;

        [Header("Runtime")]
        [SerializeField] private int selectedIndex = 0;

        private readonly List<EnemyTargetView> _views = new();

        public int SelectedIndex => selectedIndex;

        private void Awake()
        {
            if (battle == null) battle = FindObjectOfType<DungeonDeck.Battle.BattleController>(true);
            if (hitPopups == null) hitPopups = FindObjectOfType<HitPopupSpawner>(true);
        }

        private void Start()
        {
            // 자동 선택(등록이 먼저 끝난 뒤)
            if (_views.Count > 0)
            {
                Select(Mathf.Clamp(selectedIndex, 0, _views.Count - 1));
            }
        }

        public void Register(EnemyTargetView view)
        {
            if (view == null) return;
            if (_views.Contains(view)) return;

            _views.Add(view);

            // 인덱스 결정: 명시 index 우선, 아니면 등록 순서
            int idx = (view.index >= 0) ? view.index : (_views.Count - 1);
            view.SetIndex(idx);

            // 인덱스 기준 정렬(명시 인덱스 섞여도 안정)
            _views.Sort((a, b) => a.Index.CompareTo(b.Index));

            // 정렬 후 인덱스 다시 매기기(0..N-1로 강제 정규화)
            for (int i = 0; i < _views.Count; i++)
                _views[i].SetIndex(i);

            // BattleController 적 리스트 수를 View 개수에 맞춤
            if (battle != null)
                battle.EnsureEnemyCount(_views.Count);

            // 등록 직후 선택 반영
            RefreshSelectionVisual();
            SyncPopupTargets();
        }

        public void Unregister(EnemyTargetView view)
        {
            if (view == null) return;
            _views.Remove(view);

            // 재정렬/재인덱싱
            for (int i = 0; i < _views.Count; i++)
                _views[i].SetIndex(i);

            if (selectedIndex >= _views.Count)
                selectedIndex = Mathf.Max(0, _views.Count - 1);

            if (battle != null)
                battle.EnsureEnemyCount(_views.Count);

            RefreshSelectionVisual();
            SyncPopupTargets();

        }

        public void Select(int index)
        {
            if (_views.Count == 0) return;

            selectedIndex = Mathf.Clamp(index, 0, _views.Count - 1);

            // BattleController에 선택 타겟 반영
            if (battle != null)
                battle.SelectEnemy(selectedIndex);

            RefreshSelectionVisual();
            SyncPopupTargets();
        }

        private void RefreshSelectionVisual()
        {
            for (int i = 0; i < _views.Count; i++)
                _views[i].SetSelected(i == selectedIndex);
        }

        private void SyncPopupTargets()
        {
            if (hitPopups == null) return;

            // 현재 등록된 뷰 기준으로 enemyTargets[0..]를 재구성
            for (int i = 0; i < _views.Count; i++)
            {
                var v = _views[i];
                if (v == null) continue;

                // popupTarget이 없으면 자기 transform로 fallback (EnemyTargetView Awake에서 기본 세팅됨)
                hitPopups.RegisterEnemyTarget(i, v.popupTarget, null);
            }
        }
    }
}
