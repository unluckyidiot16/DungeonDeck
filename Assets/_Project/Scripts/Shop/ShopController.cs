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
        
        [Tooltip("RunSession의 카드 풀(서약/메타 해금)을 후보/가중치에 반영")]
        [SerializeField] private bool useRunCardPools = true;
        
        [Header("Slot-specific Pools (Optional)")]
        [Tooltip("true면 특정 슬롯(기본: 마지막 슬롯)을 Premium 풀로 굴립니다.")]
        [SerializeField] private bool usePremiumSlot = true;
            
        [Tooltip("Premium 슬롯 인덱스 (0-based). 기본 3 = 4칸 중 마지막.")]
        [SerializeField] private int premiumSlotIndex = 3;
            
        [Tooltip("Premium 슬롯에서만 사용할 풀(여기 비어있으면 일반 Shop 풀로 굴림)")]
        [SerializeField] private List<CardPoolDefinition> premiumShopPools = new();

        [Header("Premium Pricing")]
        [Tooltip("Premium 슬롯 가격 배수 (예: 1.25 = +25%)")]
        [Range(1f, 3f)]
        [SerializeField] private float premiumPriceMultiplier = 1.25f;
        
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
        
        // 카드 id -> 현재 상점에서의 실제 가격(프리미엄 슬롯 반영)
        private readonly Dictionary<string, int> _priceCacheById = new();

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
            
            // 슬롯별 풀을 쓰려면, 후보 리스트에 Premium 풀 카드도 포함되어야
            // state에 저장된 id -> 카드 resolve가 안전합니다.
            // (BuildCandidates에서 premiumShopPools도 함께 후보에 합칩니다.)

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
            RebuildPriceCache();
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
            
         // -----------------------------
            // 1) 슬롯 분리: normal vs premium
            // -----------------------------
            int premiumSlot = Mathf.Clamp(premiumSlotIndex, 0, Mathf.Max(0, offerCount - 1));
            var normalSlots = new List<int>(unsoldIndices.Count);
            var premiumSlots = new List<int>(1);

            for (int i = 0; i < unsoldIndices.Count; i++)
            {
                int slot = unsoldIndices[i];
                if (usePremiumSlot && slot == premiumSlot) premiumSlots.Add(slot);
                else normalSlots.Add(slot);
            }

            // ✅ 중요: Shop은 Shop 컨텍스트 풀을 써야 함 (기본 Reward 풀로 굴러가면 UX가 깨짐)
            List<CardPoolDefinition> normalPools = null;
            if (useRunCardPools && _run != null)
            {
                var ro = _run.GetActiveCardPools(RunSession.CardPoolContext.Shop);
                if (ro != null && ro.Count > 0)
                    normalPools = new List<CardPoolDefinition>(ro);
            }

            // premium 풀: 인스펙터 지정이 있으면 그걸 우선, 없으면 normal과 동일
            var premiumPools = ResolvePoolList(premiumShopPools);
            if (premiumPools == null || premiumPools.Count == 0)
                premiumPools = normalPools;

            // -----------------------------
            // 2) normal 먼저 롤 (유니크)
            // -----------------------------
            var usedIds = new HashSet<string>();
            
            // SOLD 슬롯에 이미 박힌 카드 id도 중복 방지에 포함
            for (int i = 0; i < offerCount; i++)
            {
                if (!s.shopOfferSold[i]) continue;
                var id = s.shopOfferIds[i];
                if (!string.IsNullOrWhiteSpace(id)) usedIds.Add(id);
            }

            var rolledNormal = RollFromEither(
                poolsOrNull: normalPools,
                ownedDeck: s.deck,
                count: normalSlots.Count, 
                seed: DeriveSlotSeed(seed, 0),
                cfg: cfg
            ); 
            
            for (int i = 0; i < normalSlots.Count; i++)
            {
                int slot = normalSlots[i];
                var c = (rolledNormal != null && i < rolledNormal.Count) ? rolledNormal[i] : null;

                s.shopOfferIds[slot] = c != null ? c.id : "";
                if (c != null && !string.IsNullOrWhiteSpace(c.id)) usedIds.Add(c.id);
                if (overwriteSold) s.shopOfferSold[slot] = false;
            }

            // -----------------------------
            // 3) premium 롤 (normal과 중복 피하려고 재시도)
            // -----------------------------
            for (int p = 0; p < premiumSlots.Count; p++)
            {
                int slot = premiumSlots[p];

                CardDefinition chosen = null;
                const int maxAttempts = 12;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var one = RollFromEither(
                        poolsOrNull: premiumPools,
                        ownedDeck: s.deck,
                        count: 1,
                        seed: DeriveSlotSeed(seed, 1000 + attempt),
                        cfg: cfg
                    );
                    
                    var cand = (one != null && one.Count > 0) ? one[0] : null;
                    if (cand == null) { chosen = null; break; }
                    if (string.IsNullOrWhiteSpace(cand.id)) { chosen = cand; break; }
                    if (!usedIds.Contains(cand.id))
                    {
                        chosen = cand;
                        break;
                    }
                }

                s.shopOfferIds[slot] = chosen != null ? chosen.id : "";
                if (chosen != null && !string.IsNullOrWhiteSpace(chosen.id)) usedIds.Add(chosen.id);
                if (overwriteSold) s.shopOfferSold[slot] = false;
            }
        }

        private int DeriveSlotSeed(int baseSeed, int salt)
                    {
                        unchecked
                        {
                                int x = baseSeed;
                                x = x * 1103515245 + 12345;
                                x ^= (salt * 10007);
                                if (x == 0) x = 1;
                                return x;
                        }
                    }
        
        private List<CardPoolDefinition> ResolvePoolList(List<CardPoolDefinition> pools)
            {
                if (pools == null || pools.Count == 0) return pools;
        
                var outList = new List<CardPoolDefinition>(pools.Count);
                for (int i = 0; i < pools.Count; i++)
                {
                    var p = pools[i];
                    if (p == null) continue;
                    // AllowsShop가 없다면 컴파일 에러가 날 수 있지만,
                    // RunSession 쪽에서 이미 쓰는 것으로 보이는 필드라 여기서도 필터링해 둠.
                    if (!p.AllowsShop) continue;
                    outList.Add(p);
                }
                return outList;
            }
    
        private List<CardDefinition> RollFromEither(
            List<CardPoolDefinition> poolsOrNull,
            List<CardDefinition> ownedDeck,
            int count,
            int seed, 
            CardRewardRollerCards.RollConfig cfg
            )
        {
                if (count <= 0) return new List<CardDefinition>();
        
                if (poolsOrNull != null && poolsOrNull.Count > 0)
                {
                    return CardRewardRollerCards.RollFromPools(
                        pools: poolsOrNull,
                        ownedDeck: ownedDeck,
                        count: count,
                        unique: true,
                        seed: seed,
                        configOpt: cfg
                        );
                }
        
                return CardRewardRollerCards.RollWeighted(
                    candidates: _candidateList,
                    ownedDeck: ownedDeck,
                    count: count,
                    unique: true,
                    seed: seed,
                    configOpt: cfg
                    );
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
            RebuildPriceCache();
        }

        private bool IsPremiumSlot(int slotIndex)
        {
            int premiumSlot = Mathf.Clamp(premiumSlotIndex, 0, Mathf.Max(0, offerCount - 1));
            return usePremiumSlot && slotIndex == premiumSlot;
        }
    
        private void RebuildPriceCache()
            {
                _priceCacheById.Clear();
                if (_offers == null) return;
        
                for (int i = 0; i < _offers.Count; i++)
                {
                    var c = _offers[i];
                    if (c == null) continue;
                    if (string.IsNullOrWhiteSpace(c.id)) continue;
            
                    int price = GetBasePrice(c);
                    if (IsPremiumSlot(i))
                        price = Mathf.CeilToInt(price * premiumPriceMultiplier);
            
                    _priceCacheById[c.id] = price;
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

            RebuildPriceCache();
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

            RebuildPriceCache();
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
            RebuildPriceCache();
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

            if (!string.IsNullOrWhiteSpace(card.id) && _priceCacheById.TryGetValue(card.id, out int cached))
                return cached;
            
            return GetBasePrice(card);
        }
        
        private int GetBasePrice(CardDefinition card)
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

            // 1) 인스펙터 강제 후보가 있으면 우선
            if (shopCandidates != null && shopCandidates.Count > 0)
            {
                for (int i = 0; i < shopCandidates.Count; i++)
                    if (shopCandidates[i] != null) list.Add(shopCandidates[i]);
                AddCandidatesFromPools(list, premiumShopPools); // premium도 resolve 가능하도록 합침
                return DedupById(list);
            }

            // 2) 카드 풀 기반 후보(서약/메타 해금 포함)
            if (useRunCardPools && _run != null)
            {
                var poolCandidates = _run.GetActiveCardCandidatesUnique(RunSession.CardPoolContext.Shop);
                if (poolCandidates != null && poolCandidates.Count > 0)
                {
                    AddCandidatesFromPools(poolCandidates, premiumShopPools); // premium도 resolve 가능하도록 합침
                    return poolCandidates;
                }
            }
            
            // 3) 없으면 현재 덱 기반(최소 동작)
            if (_run != null && _run.State != null && _run.State.deck != null)
            {
                for (int i = 0; i < _run.State.deck.Count; i++)
                    if (_run.State.deck[i] != null) list.Add(_run.State.deck[i]);
            }

            AddCandidatesFromPools(list, premiumShopPools); // premium도 resolve 가능하도록 합침
            
            return DedupById(list);
        }

        private void AddCandidatesFromPools(List<CardDefinition> list, List<CardPoolDefinition> pools)
        {
            if (list == null) return;
            if (pools == null || pools.Count == 0) return;
            
            for (int i = 0; i < pools.Count; i++)
            {
                var p = pools[i];
                if (p == null || p.entries == null) continue;
                if (!p.AllowsShop) continue;
                
                for (int e = 0; e < p.entries.Count; e++)
                {
                    var entry = p.entries[e];
                    if (entry == null) continue;
                    var c = entry.cardAsset as CardDefinition;
                    if (c != null) list.Add(c);
                }
            }
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
            
            // 후보 리스트에 없더라도(예: 후보 캐시 꼬임) RunSession 쪽에서 한번 더 시도
            if (_run != null && _run.TryResolveCardById(id, out var resolved))
                return resolved;
            
            return null;
        }
    }
}
