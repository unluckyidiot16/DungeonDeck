using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Config.Cards;
using DungeonDeck.UI.Battle; // BattleCardButtonView 재사용

namespace DungeonDeck.UI.Shop
{
    public class ShopPanel : MonoBehaviour
    {
        [Header("Top Bar")]
        public TMP_Text goldText;
        public Button leaveButton;

        [Header("Offers")]
        public Transform offersRoot;
        public BattleCardButtonView cardPrefab;
        public int maxOfferSlots = 4;

        [Header("Remove Mode")]
        public GameObject removeRoot;          // 제거 모드 패널 루트(없으면 그냥 비활성 무시)
        public Transform removeListRoot;       // 덱 카드 리스트 루트
        public Button enterRemoveButton;
        public Button exitRemoveButton;
        public TMP_Text removeHintText;
        
        [Header("Reroll")]
        public Button rerollButton;
        public TMP_Text rerollText; // optional

        private readonly List<BattleCardButtonView> _offerViews = new();
        private readonly List<BattleCardButtonView> _removeViews = new();

        private Action _onLeave;
        private Action _onEnterRemove;
        private Action _onExitRemove;

        public void Hook(Action onLeave, Action onEnterRemove, Action onExitRemove)
        {
            _onLeave = onLeave;
            _onEnterRemove = onEnterRemove;
            _onExitRemove = onExitRemove;

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveAllListeners();
                leaveButton.onClick.AddListener(() => _onLeave?.Invoke());
            }

            if (enterRemoveButton != null)
            {
                enterRemoveButton.onClick.RemoveAllListeners();
                enterRemoveButton.onClick.AddListener(() => _onEnterRemove?.Invoke());
            }

            if (exitRemoveButton != null)
            {
                exitRemoveButton.onClick.RemoveAllListeners();
                exitRemoveButton.onClick.AddListener(() => _onExitRemove?.Invoke());
            }

            if (removeRoot != null) removeRoot.SetActive(false);
        }

        public void SetGold(int gold)
        {
            if (goldText != null) goldText.text = $"{gold}G";
        }
        
        // ShopPanel.cs 안에 있는 ShowOffersSoldAware를 이 버전으로 추천
        public void ShowOffersSoldAware(
            List<CardDefinition> offers,
            List<bool> soldSlots,
            Func<CardDefinition, int> getPrice,
            Func<int, bool> canBuyAt,
            Action<int> onBuyAt)
        {
            EnsureOfferSlots();

            for (int i = 0; i < _offerViews.Count; i++)
            {
                int idx = i;
                var view = _offerViews[idx];

                bool sold = (soldSlots != null && idx < soldSlots.Count && soldSlots[idx]);

                if (offers == null || idx >= offers.Count || offers[idx] == null)
                {
                    view.gameObject.SetActive(false);
                    continue;
                }

                var card = offers[idx];
                view.gameObject.SetActive(true);

                if (sold)
                {
                    view.Bind(card: card, interactable: false, onClick: null, showCost: true);
                    if (view.rarityText != null) view.rarityText.text = "SOLD";
                    if (view.button != null) view.button.interactable = false;
                    continue;
                }

                bool interactable = canBuyAt != null ? canBuyAt(idx) : true;

                view.Bind(card: card, interactable: interactable, onClick: () => onBuyAt?.Invoke(idx), showCost: true);

                if (view.rarityText != null && getPrice != null)
                    view.rarityText.text = $"{card.rarity} • {getPrice(card)}G";
            }
        }


        // BattleCardButtonView에 텍스트 필드가 있다고 가정(너가 카드답게 개선하던 그 구조)
        private void ApplySoldVisual(BattleCardButtonView v, string soldText)
        {
            if (v == null) return;

            if (v.nameText != null) v.nameText.text = soldText;
            if (v.effectText != null) v.effectText.text = "";
            if (v.costText != null) v.costText.text = "";
            if (v.rarityText != null) v.rarityText.text = soldText;

            if (v.button != null) v.button.interactable = false;
        }

        
        // Hook 시그니처 확장
        private Action _onReroll;

        public void Hook(Action onLeave, Action onEnterRemove, Action onExitRemove, Action onReroll)
        {
            _onLeave = onLeave;
            _onEnterRemove = onEnterRemove;
            _onExitRemove = onExitRemove;
            _onReroll = onReroll;

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveAllListeners();
                leaveButton.onClick.AddListener(() => _onLeave?.Invoke());
            }

            if (enterRemoveButton != null)
            {
                enterRemoveButton.onClick.RemoveAllListeners();
                enterRemoveButton.onClick.AddListener(() => _onEnterRemove?.Invoke());
            }

            if (exitRemoveButton != null)
            {
                exitRemoveButton.onClick.RemoveAllListeners();
                exitRemoveButton.onClick.AddListener(() => _onExitRemove?.Invoke());
            }

            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveAllListeners();
                rerollButton.onClick.AddListener(() => _onReroll?.Invoke());
            }

            if (removeRoot != null) removeRoot.SetActive(false);
        }

        // 리롤 버튼 UI 상태 갱신용(추가)
        public void SetRerollState(bool interactable, int cost)
        {
            if (rerollButton != null) rerollButton.interactable = interactable;

            if (rerollText != null)
            {
                rerollText.text = cost > 0 ? $"REROLL ({cost}G)" : "REROLL";
            }
        }

        // ---------- Offers ----------

        public void ShowOffers(
            List<CardDefinition> offers,
            Func<CardDefinition, int> getPrice,
            Func<CardDefinition, bool> canBuy,
            Action<int> onBuy)
        {
            EnsureOfferSlots();

            for (int i = 0; i < _offerViews.Count; i++)
            {
                int idx = i;

                if (offers == null || idx >= offers.Count || offers[idx] == null)
                {
                    _offerViews[idx].gameObject.SetActive(false);
                    continue;
                }

                var card = offers[idx];
                bool interactable = canBuy != null ? canBuy(card) : true;

                _offerViews[idx].gameObject.SetActive(true);
                _offerViews[idx].Bind(
                    card: card,
                    interactable: interactable,
                    onClick: () => onBuy?.Invoke(idx),
                    showCost: true // 에너지 코스트도 같이 보여주는 편이 상점에서 편함
                );

                // 가격 표시: rarityText를 재활용해서 "Rare • 80G" 식으로
                if (_offerViews[idx].rarityText != null && getPrice != null)
                    _offerViews[idx].rarityText.text = $"{card.rarity} • {getPrice(card)}G";
            }
        }

        public void RefreshOffersInteractable(
            List<CardDefinition> offers,
            Func<CardDefinition, bool> canBuy,
            Func<CardDefinition, int> getPrice)
        {
            if (offers == null) return;

            for (int i = 0; i < _offerViews.Count; i++)
            {
                if (i >= offers.Count || offers[i] == null)
                {
                    _offerViews[i].gameObject.SetActive(false);
                    continue;
                }

                var card = offers[i];
                _offerViews[i].gameObject.SetActive(true);

                if (_offerViews[i].button != null && canBuy != null)
                    _offerViews[i].button.interactable = canBuy(card);

                if (_offerViews[i].rarityText != null && getPrice != null)
                    _offerViews[i].rarityText.text = $"{card.rarity} • {getPrice(card)}G";
            }
        }

        private void EnsureOfferSlots()
        {
            if (cardPrefab == null || offersRoot == null) return;
            if (_offerViews.Count > 0) return;

            int n = Mathf.Max(1, maxOfferSlots);
            for (int i = 0; i < n; i++)
            {
                var v = Instantiate(cardPrefab, offersRoot);
                v.name = $"ShopOffer_{i}";
                _offerViews.Add(v);
            }
        }

        // ---------- Remove Mode ----------

        public void ShowRemoveList(
            List<CardDefinition> deck,
            int removeCost,
            bool canRemove,
            Action<CardDefinition> onRemoveCard)
        {
            if (removeRoot != null) removeRoot.SetActive(true);

            if (removeHintText != null)
                removeHintText.text = canRemove ? $"Remove a card (-{removeCost}G)" : $"Need {removeCost}G to remove";

            // 기존 리스트 제거
            ClearRemoveViews();

            if (cardPrefab == null || removeListRoot == null) return;
            if (deck == null) return;

            // 덱 전체를 나열(최소)
            for (int i = 0; i < deck.Count; i++)
            {
                var card = deck[i];
                if (card == null) continue;

                var v = Instantiate(cardPrefab, removeListRoot);
                v.name = $"Remove_{i}_{card.id}";
                _removeViews.Add(v);

                v.Bind(
                    card: card,
                    interactable: canRemove,
                    onClick: () => onRemoveCard?.Invoke(card),
                    showCost: true
                );

                if (v.rarityText != null)
                    v.rarityText.text = $"{card.rarity} • Remove {removeCost}G";
            }
        }

        public void HideRemoveList()
        {
            if (removeRoot != null) removeRoot.SetActive(false);
            ClearRemoveViews();
        }

        private void ClearRemoveViews()
        {
            for (int i = 0; i < _removeViews.Count; i++)
            {
                if (_removeViews[i] != null)
                    Destroy(_removeViews[i].gameObject);
            }
            _removeViews.Clear();
        }
    }
}
