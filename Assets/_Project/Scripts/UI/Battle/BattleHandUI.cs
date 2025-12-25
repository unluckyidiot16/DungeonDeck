using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Battle;

namespace DungeonDeck.UI.Battle
{
    public class BattleHandUI : MonoBehaviour
    {
        public BattleController battle;
        public Transform handRoot;

        public BattleCardButtonView cardPrefab;

        public TMP_Text energyText;
        public TMP_Text playerHpText;
        public TMP_Text enemyHpText;
        public Button endTurnButton;

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
            if (_slots.Count > 0) return;

            for (int i = 0; i < maxHandSlots; i++)
            {
                var view = Instantiate(cardPrefab, handRoot);
                view.name = $"CardSlot_{i}";
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
                int idx = i;
                var card = battle.GetHandCard(idx);

                if (card == null)
                {
                    _slots[idx].gameObject.SetActive(false);
                    continue;
                }

                bool canPlay = battle.Energy >= card.cost;

                _slots[idx].Bind(
                    card,
                    interactable: canPlay,
                    onClick: () => battle.TryPlayCardAt(idx),
                    showCost: true
                );
            }
        }
    }
}
