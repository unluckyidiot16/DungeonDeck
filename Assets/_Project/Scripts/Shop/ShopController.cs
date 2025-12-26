using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Run;
using DungeonDeck.Config.Cards;
using DungeonDeck.Config.Map;
using DungeonDeck.Core;
using DungeonDeck.Rewards;
using DungeonDeck.UI.Shop;

namespace DungeonDeck.Shop
{
    public class ShopController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string mapSceneName = "Map";

        [Header("UI")]
        [SerializeField] private ShopPanel panel;

        [Header("Offer Settings")]
        [SerializeField] private int offerCount = 4;

        [Tooltip("비어있으면 '현재 덱'을 후보로 사용(최소 동작용). 실제론 여기에 전체 카드풀 넣기 추천.")]
        [SerializeField] private List<CardDefinition> shopCandidates = new();

        [Header("Reroll")]
        [Tooltip("0이면 무료 리롤")]
        [SerializeField] private int rerollCost = 25;

        [Header("Prices (Gold)")]
        public int commonPrice = 25;
        public int uncommonPrice = 45;
        public int rarePrice = 80;
        public int epicPrice = 130;
        public int legendaryPrice = 220;

        [Header("Remove Card")]
        public int removeCost = 75;

        [Header("Roll Tuning")]
        [Range(0.05f, 1f)] public float duplicateWeightMultiplier = 0.35f;
        public bool scaleByCopies = true;
        public int maxCopyExponent = 3;

        private RunSession _run;
        private List<CardDefinition> _offers = new();
        private List<CardDefinition> _candidateList = new();
        private bool _leaving = false;

        private void Awake()
        {
            if (panel == null) panel = FindObjectOfType<ShopPanel>(true);
        }

        private void Start()
        {
            _run = RunSession.I;
            if (_run == null || _run.State == null)
            {
                Debug.LogError("[Shop] RunSession missing.");
                return;
            }

            if (panel == null)
            {
                Debug.LogError("[Shop] ShopPanel missing in scene.");
                return;
            }

            panel.Hook(
                onLeave: OnLeaveShop,
                onEnterRemove: OpenRemoveMode,
                onExitRemove: CloseRemoveMode,
                onReroll: RerollAllOffers
            );

            _candidateList = BuildCandidates();

            EnsureShopState();
            RefreshShopView();
        }

        // -------------------------
        // State: fixed offers + SOLD per shop node
        // -------------------------
        private void EnsureShopState()
        {
            var s = _run.State;

            bool isNewShopNode = (s.shopNodeIndex != s.nodeIndex);
            bool invalidList =
                (s.shopOfferIds == null || s.shopOfferIds.Count != offerCount) ||
                (s.shopOfferSold == null || s.shopOfferSold.Count != offerCount);

            if (isNewShopNode || invalidList)
            {
                s.shopNodeIndex = s.nodeIndex;
                s.shopSeed = UnityEngine.Random.Range(1, int.MaxValue);

                s.shopRemoveUsed = false;
                s.shopRerollCount = 0;

                EnsureListSize(s.shopOfferIds, offerCount, "");
                EnsureListSize(s.shopOfferSold, offerCount, false);

                GenerateOffersIntoState(seed: s.shopSeed, overwriteSold: true);
            }

            LoadOffersFromState();
        }

        private void GenerateOffersIntoState(int seed, bool overwriteSold)
        {
            var s = _run.State;

            var cfg = CardRewardRollerCards.RollConfig.Default;
            cfg.duplicateWeightMultiplier = Mathf.Clamp(duplicateWeightMultiplier, 0.0001f, 1f);
            cfg.scaleByCopies = scaleByCopies;
            cfg.maxCopyExponent = Mathf.Max(1, maxCopyExponent);

            // 어떤 슬롯이 unsold인지 모아서 그 개수만큼만 뽑기(유니크)
            var unsoldIndices = new List<int>();
            for (int i = 0; i < offerCount; i++)
            {
                bool sold = s.shopOfferSold[i];
                if (!sold) unsoldIndices.Add(i);
            }

            if (unsoldIndices.Count == 0)
                return;

            var rolled = CardRewardRollerCards.RollWeighted(
                candidates: _candidateList,
                ownedDeck: s.deck,
                count: unsoldIndices.Count,
                unique: true,
                seed: seed,
                configOpt: cfg
            );

            for (int j = 0; j < unsoldIndices.Count; j++)
            {
                int slot = unsoldIndices[j];
                var c = (rolled != null && j < rolled.Count) ? rolled[j] : null;

                s.shopOfferIds[slot] = c != null ? c.id : "";
                if (overwriteSold) s.shopOfferSold[slot] = false;
            }
        }

        private void LoadOffersFromState()
        {
            var s = _run.State;

            _offers = new List<CardDefinition>(offerCount);
            for (int i = 0; i < offerCount; i++)
            {
                string id = s.shopOfferIds[i];
                _offers.Add(ResolveCandidateById(id));
            }
        }

        private void EnsureListSize<T>(List<T> list, int size, T fill)
        {
            if (list == null) return;
            while (list.Count < size) list.Add(fill);
            if (list.Count > size) list.RemoveRange(size, list.Count - size);
        }

        // -------------------------
        // Buy -> SOLD (slot stays SOLD forever in this shop)
        // -------------------------
        private void BuyOfferAt(int index)
        {
            var s = _run.State;

            if (index < 0 || index >= _offers.Count) return;
            if (s.shopOfferSold[index]) return;

            var card = _offers[index];
            if (card == null) return;

            int price = GetPrice(card);
            if (s.gold < price)
            {
                RefreshShopView();
                return;
            }

            s.gold -= price;
            s.deck.Add(card);

            // ✅ 슬롯 SOLD 고정 (리롤로도 안 바뀜)
            s.shopOfferSold[index] = true;

            RefreshShopView();
        }

        private bool CanBuyAt(int index)
        {
            var s = _run.State;
            if (index < 0 || index >= _offers.Count) return false;
            if (s.shopOfferSold[index]) return false;

            var c = _offers[index];
            if (c == null) return false;

            return s.gold >= GetPrice(c);
        }

        // -------------------------
        // Reroll: reroll all UNSOLD offers only
        // -------------------------
        private void RerollAllOffers()
        {
            var s = _run.State;

            int unsoldCount = 0;
            for (int i = 0; i < offerCount; i++)
                if (!s.shopOfferSold[i]) unsoldCount++;

            if (unsoldCount <= 0)
                return;

            if (rerollCost > 0 && s.gold < rerollCost)
                return;

            if (rerollCost > 0)
                s.gold -= rerollCost;

            s.shopRerollCount += 1;

            int seed = DeriveRerollSeed(s.shopSeed, s.shopRerollCount);

            // ✅ unsold만 새로 뽑아서 state에 반영
            GenerateOffersIntoState(seed: seed, overwriteSold: false);
            LoadOffersFromState();

            RefreshShopView();
        }

        private int DeriveRerollSeed(int shopSeed, int rerollCount)
        {
            unchecked
            {
                int x = shopSeed;
                x = x * 486187739 + (rerollCount + 1) * 10007;
                if (x == 0) x = 1;
                return x;
            }
        }

        private bool CanReroll()
        {
            var s = _run.State;

            bool hasUnsold = false;
            for (int i = 0; i < offerCount; i++)
            {
                if (!s.shopOfferSold[i]) { hasUnsold = true; break; }
            }
            if (!hasUnsold) return false;

            if (rerollCost > 0 && s.gold < rerollCost) return false;
            return true;
        }

        // -------------------------
        // Remove: only once per shop (그대로 유지)
        // -------------------------
        private void OpenRemoveMode()
        {
            SyncRemoveButtonInteractable();

            bool canRemoveNow =
                !_run.State.shopRemoveUsed &&
                _run.State.gold >= removeCost &&
                _run.State.deck != null &&
                _run.State.deck.Count > 0;

            panel.SetGold(_run.State.gold);

            panel.ShowRemoveList(
                deck: _run.State.deck,
                removeCost: removeCost,
                canRemove: canRemoveNow,
                onRemoveCard: RemoveCard
            );
        }

        private void RemoveCard(CardDefinition card)
        {
            if (card == null) return;

            if (_run.State.shopRemoveUsed) { OpenRemoveMode(); return; }
            if (_run.State.gold < removeCost) { OpenRemoveMode(); return; }

            int idx = _run.State.deck.IndexOf(card);
            if (idx < 0)
            {
                for (int i = 0; i < _run.State.deck.Count; i++)
                {
                    if (_run.State.deck[i] != null && _run.State.deck[i].id == card.id)
                    {
                        idx = i;
                        break;
                    }
                }
            }
            if (idx < 0) return;

            _run.State.gold -= removeCost;
            _run.State.deck.RemoveAt(idx);
            _run.State.shopRemoveUsed = true;

            OpenRemoveMode();
        }

        private void CloseRemoveMode()
        {
            panel.HideRemoveList();
            RefreshShopView();
        }

        private void SyncRemoveButtonInteractable()
        {
            if (panel == null || panel.enterRemoveButton == null) return;
            panel.enterRemoveButton.interactable = !_run.State.shopRemoveUsed;
        }

        // -------------------------
        // UI
        // -------------------------
        private void RefreshShopView()
        {
            panel.SetGold(_run.State.gold);

            panel.ShowOffersSoldAware(
                offers: _offers,
                soldSlots: _run.State.shopOfferSold,
                getPrice: GetPrice,
                canBuyAt: CanBuyAt,
                onBuyAt: BuyOfferAt
            );

            panel.SetRerollState(interactable: CanReroll(), cost: rerollCost);

            panel.HideRemoveList();
            SyncRemoveButtonInteractable();
        }

        private int GetPrice(CardDefinition card)
        {
            if (card == null) return 0;

            switch (card.rarity)
            {
                case CardRarity.Common: return commonPrice;
                case CardRarity.Uncommon: return uncommonPrice;
                case CardRarity.Rare: return rarePrice;
                case CardRarity.Epic: return epicPrice;
                case CardRarity.Legendary: return legendaryPrice;
                default: return commonPrice;
            }
        }

        private void OnLeaveShop()
        {
            if (_leaving) return;
            _leaving = true;
            
            // 안전: 현재 노드가 Shop일 때만 advance
            if (_run != null && _run.State != null)
            {
                var t = _run.GetNodeType(_run.State.nodeIndex);
                if (t == MapNodeType.Shop)
                    _run.MarkNodeClearedAndAdvance();
            }
            
            SceneManager.LoadScene(SceneRoutes.Map);
        }

        // -------------------------
        // Candidates
        // -------------------------
        private List<CardDefinition> BuildCandidates()
        {
            var list = new List<CardDefinition>();

            if (shopCandidates != null && shopCandidates.Count > 0)
            {
                for (int i = 0; i < shopCandidates.Count; i++)
                    if (shopCandidates[i] != null) list.Add(shopCandidates[i]);
                return DedupById(list);
            }

            if (_run != null && _run.State != null && _run.State.deck != null)
            {
                for (int i = 0; i < _run.State.deck.Count; i++)
                    if (_run.State.deck[i] != null) list.Add(_run.State.deck[i]);
            }

            return DedupById(list);
        }

        private List<CardDefinition> DedupById(List<CardDefinition> list)
        {
            var map = new Dictionary<string, CardDefinition>();
            var outList = new List<CardDefinition>();

            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null) continue;
                if (string.IsNullOrWhiteSpace(c.id)) continue;

                if (map.ContainsKey(c.id)) continue;
                map[c.id] = c;
                outList.Add(c);
            }

            return outList;
        }

        private CardDefinition ResolveCandidateById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < _candidateList.Count; i++)
            {
                var c = _candidateList[i];
                if (c != null && c.id == id) return c;
            }
            return null;
        }
    }
}
