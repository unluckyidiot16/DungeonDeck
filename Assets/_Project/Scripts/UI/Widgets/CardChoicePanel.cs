using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Rewards;

namespace DungeonDeck.UI.Widgets
{
    public class CardChoicePanel : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button skipButton;

        [Header("Option Buttons (3)")]
        [SerializeField] private Button optionButton0;
        [SerializeField] private Button optionButton1;
        [SerializeField] private Button optionButton2;

        [SerializeField] private TMP_Text optionText0;
        [SerializeField] private TMP_Text optionText1;
        [SerializeField] private TMP_Text optionText2;

        private Action<CardRewardRoller.RewardOption> _onChosen;
        private Action _onSkipped;
        private List<CardRewardRoller.RewardOption> _current;

        private void Awake()
        {
            if (root == null) root = gameObject;

            Bind(optionButton0, 0);
            Bind(optionButton1, 1);
            Bind(optionButton2, 2);

            if (skipButton != null)
                skipButton.onClick.AddListener(HandleSkip);

            Hide();
        }

        private void Bind(Button btn, int index)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => HandlePick(index));
        }

        public void Show(
            IReadOnlyList<CardRewardRoller.RewardOption> options,
            Action<CardRewardRoller.RewardOption> onChosen,
            Action onSkipped = null,
            string title = "Choose 1 Card")
        {
            _onChosen = onChosen;
            _onSkipped = onSkipped;

            _current = options != null ? new List<CardRewardRoller.RewardOption>(options) : new List<CardRewardRoller.RewardOption>();

            if (titleText != null) titleText.text = title;

            ApplyOption(0, optionButton0, optionText0);
            ApplyOption(1, optionButton1, optionText1);
            ApplyOption(2, optionButton2, optionText2);

            if (skipButton != null) skipButton.gameObject.SetActive(onSkipped != null);

            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void ApplyOption(int idx, Button btn, TMP_Text txt)
        {
            bool has = _current != null && idx >= 0 && idx < _current.Count && _current[idx].cardAsset != null;

            if (btn != null) btn.gameObject.SetActive(has);
            if (txt != null)
            {
                txt.text = has ? _current[idx].displayName : "";
            }
        }

        private void HandlePick(int index)
        {
            if (_current == null || index < 0 || index >= _current.Count)
                return;

            var opt = _current[index];
            if (opt.cardAsset == null)
                return;

            Hide();
            _onChosen?.Invoke(opt);
        }

        private void HandleSkip()
        {
            Hide();
            _onSkipped?.Invoke();
        }
    }
}
