using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Battle;

namespace DungeonDeck.UI.Battle
{
    public class BattleHandUI : MonoBehaviour
    {
        [Header("Refs")]
        public BattleController battle;
        public Transform handRoot;

        [Header("Prefab (Optional)")]
        public BattleCardButtonView cardPrefab;

        [Header("UI (Optional)")]
        public TMP_Text energyText;
        public TMP_Text playerHpText;
        public TMP_Text enemyHpText;
        public Button endTurnButton;

        [Header("Settings")]
        public int maxHandSlots = 5;

        private readonly List<BattleCardButtonView> _slots = new();

        private void Awake()
        {
            if (battle == null) battle = FindObjectOfType<BattleController>();
            if (handRoot == null) handRoot = transform;

            BuildSlots();

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(() => battle?.EndTurn());
            }
        }

        private void OnEnable()
        {
            if (battle != null) battle.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            if (battle != null) battle.StateChanged -= Refresh;
        }

        private void Start()
        {
            Refresh();
        }

        private void BuildSlots()
        {
            // 이미 만들어져 있다면 유지
            if (_slots.Count > 0) return;

            for (int i = 0; i < maxHandSlots; i++)
            {
                BattleCardButtonView view;

                if (cardPrefab != null)
                {
                    view = Instantiate(cardPrefab, handRoot);
                    view.name = $"CardSlot_{i}";
                }
                else
                {
                    // 프리팹이 없어도 최소 동작하도록 런타임 생성
                    var go = new GameObject($"CardSlot_{i}", typeof(RectTransform));
                    go.transform.SetParent(handRoot, false);

                    // 기본 이미지/버튼 포함은 BattleCardButtonView가 EnsureWired에서 처리
                    view = go.AddComponent<BattleCardButtonView>();

                    // 보기용 최소 크기(레이아웃 그룹 쓰면 무시됨)
                    var rt = (RectTransform)go.transform;
                    rt.sizeDelta = new Vector2(220, 120);
                }

                _slots.Add(view);
            }
        }

        private void Refresh()
        {
            if (battle == null) return;

            if (energyText != null) energyText.text = $"EN {battle.Energy}";
            if (playerHpText != null) playerHpText.text = $"P {battle.PlayerHP}/{battle.PlayerMaxHP} (B {battle.PlayerBlock})";
            if (enemyHpText != null) enemyHpText.text = $"E {battle.EnemyHP}/{battle.EnemyMaxHP}";

            for (int i = 0; i < _slots.Count; i++)
            {
                int idx = i; // closure 안전
                var card = battle.GetHandCard(idx);

                if (card == null)
                {
                    _slots[idx].Bind(null, false, null);
                    continue;
                }

                bool canPlay = battle.Energy >= card.cost;

                _slots[idx].Bind(card, canPlay, () =>
                {
                    // 클릭 시 현재 인덱스 카드 사용
                    battle.TryPlayCardAt(idx);
                });
            }
        }
    }
}
