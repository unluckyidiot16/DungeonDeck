using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Config.Cards;
using DungeonDeck.UI.Battle; // BattleCardButtonView 재사용

namespace DungeonDeck.UI.Widgets
{
    public class CardChoicePanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;
        public TMP_Text titleText;
        public Button skipButton;

        [Header("Cards (Reuse same prefab)")]
        public Transform cardRoot;
        public BattleCardButtonView cardPrefab;

        [Header("Settings")]
        public int optionCount = 3;

        private readonly List<BattleCardButtonView> _views = new();
        private Action<CardDefinition> _onChosen;
        private Action _onSkipped;
        private List<CardDefinition> _options;

        private void Awake()
        {
            if (root == null) root = gameObject;
            if (cardRoot == null) cardRoot = transform;

            BuildViews();

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() =>
                {
                    Hide();
                    _onSkipped?.Invoke();
                });
            }

            Hide();
        }

        private void BuildViews()
        {
            if (cardPrefab == null) return;
            if (_views.Count > 0) return;

            for (int i = 0; i < optionCount; i++)
            {
                var v = Instantiate(cardPrefab, cardRoot);
                v.name = $"RewardCard_{i}";
                _views.Add(v);
            }
        }

        public void Show(
            IReadOnlyList<CardDefinition> options,
            Action<CardDefinition> onChosen,
            Action onSkipped = null,
            string title = "Choose 1 Card")
        {
            _onChosen = onChosen;
            _onSkipped = onSkipped;

            _options = options != null ? new List<CardDefinition>(options) : new List<CardDefinition>();

            if (titleText != null) titleText.text = title;
            if (skipButton != null) skipButton.gameObject.SetActive(onSkipped != null);

            for (int i = 0; i < _views.Count; i++)
            {
                int idx = i;

                if (idx >= _options.Count || _options[idx] == null)
                {
                    _views[idx].gameObject.SetActive(false);
                    continue;
                }

                var card = _options[idx];
                _views[idx].Bind(
                    card,
                    interactable: true,
                    onClick: () =>
                    {
                        Hide();
                        _onChosen?.Invoke(card);
                    },
                    showCost: false // 보상 선택 화면에선 cost 숨기는 게 보통 더 보기 좋음
                );
            }

            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
