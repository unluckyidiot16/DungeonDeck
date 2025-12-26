using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Rewards
{
    public static class CardRewardRollerCards
    {
        [Serializable]
        public struct RarityWeights
        {
            public float common;
            public float uncommon;
            public float rare;
            public float epic;
            public float legendary;

            public static RarityWeights Default => new RarityWeights
            {
                common = 1.0f,
                uncommon = 0.55f,
                rare = 0.25f,
                epic = 0.12f,
                legendary = 0.06f
            };

            public float Get(CardRarity r)
            {
                switch (r)
                {
                    case CardRarity.Common: return common;
                    case CardRarity.Uncommon: return uncommon;
                    case CardRarity.Rare: return rare;
                    case CardRarity.Epic: return epic;
                    case CardRarity.Legendary: return legendary;
                    default: return common;
                }
            }
        }

        public struct RollConfig
        {
            public RarityWeights rarityWeights;

            /// <summary>
            /// 덱에 이미 있는 카드일 때 가중치에 곱해지는 값.
            /// 예) 0.35면 "이미 가진 카드 확률 35%"
            /// </summary>
            public float duplicateWeightMultiplier;

            /// <summary>
            /// true면 "보유 장수"에 따라 더 눌림: multiplier^(copies)
            /// false면 1장이라도 있으면 multiplier 1회만 적용
            /// </summary>
            public bool scaleByCopies;

            /// <summary>
            /// scaleByCopies일 때 copies가 너무 커지면 0에 수렴하니 상한.
            /// </summary>
            public int maxCopyExponent;

            public static RollConfig Default => new RollConfig
            {
                rarityWeights = RarityWeights.Default,
                duplicateWeightMultiplier = 0.35f,
                scaleByCopies = true,
                maxCopyExponent = 3
            };
        }

        private class Item
        {
            public CardDefinition card;
            public double w;
        }

        /// <summary>
        /// 후보(candidates)에서 count장 제시.
        /// - 희귀도 가중치 적용
        /// - 덱 중복은 duplicateWeightMultiplier로 "부드럽게" 감소
        /// - unique=true면 제시 3장 내 중복은 없음(같은 카드 SO가 2번 뜨지 않음)
        /// </summary>
        public static List<CardDefinition> RollWeighted(
            IReadOnlyList<CardDefinition> candidates,
            IReadOnlyList<CardDefinition> ownedDeck,
            int count = 3,
            bool unique = true,
            int seed = 0,
            RollConfig? configOpt = null)
        {
            var cfg = configOpt ?? RollConfig.Default;

            var result = new List<CardDefinition>(Mathf.Max(0, count));
            if (candidates == null || candidates.Count == 0 || count <= 0) return result;

            // 덱 보유 장수 카운트(id 기준)
            var ownedCounts = BuildOwnedCounts(ownedDeck);

            // 아이템 리스트 생성 (null 제거 + 가중치 계산)
            var items = new List<Item>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c == null) continue;

                double w = Math.Max(0.0, cfg.rarityWeights.Get(c.rarity));

                if (ownedCounts.TryGetValue(c.id, out int copies) && copies > 0)
                {
                    float m = Mathf.Clamp(cfg.duplicateWeightMultiplier, 0.0001f, 1f);

                    if (cfg.scaleByCopies)
                    {
                        int exp = Mathf.Clamp(copies, 1, Mathf.Max(1, cfg.maxCopyExponent));
                        m = Mathf.Pow(m, exp);
                    }

                    w *= m;
                }

                // 너무 작은 값은 0 취급(총합 0 방지용)
                if (w <= 0.0) continue;

                items.Add(new Item { card = c, w = w });
            }

            if (items.Count == 0)
            {
                // 전부 weight=0으로 날아간 경우: 그냥 균등 랜덤으로라도 뽑게 처리
                var fallback = new List<CardDefinition>();
                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i] != null) fallback.Add(candidates[i]);

                return RollUniformFallback(fallback, count, unique, seed);
            }

            System.Random rng = seed != 0 ? new System.Random(seed) : null;

            for (int k = 0; k < count; k++)
            {
                if (items.Count == 0) break;

                int pickedIndex = PickWeightedIndex(items, rng);
                if (pickedIndex < 0 || pickedIndex >= items.Count) break;

                var picked = items[pickedIndex].card;
                if (picked != null) result.Add(picked);

                if (unique)
                    items.RemoveAt(pickedIndex);
            }

            return result;
        }

        private static Dictionary<string, int> BuildOwnedCounts(IReadOnlyList<CardDefinition> deck)
        {
            var map = new Dictionary<string, int>();
            if (deck == null) return map;

            for (int i = 0; i < deck.Count; i++)
            {
                var c = deck[i];
                if (c == null) continue;
                if (string.IsNullOrWhiteSpace(c.id)) continue;

                map.TryGetValue(c.id, out int n);
                map[c.id] = n + 1;
            }

            return map;
        }

        private static int PickWeightedIndex(List<Item> items, System.Random rng)
        {
            double total = 0.0;
            for (int i = 0; i < items.Count; i++)
                total += Math.Max(0.0, items[i].w);

            if (total <= 0.0) return -1;

            double roll = (rng != null ? rng.NextDouble() : UnityEngine.Random.value) * total;

            double acc = 0.0;
            for (int i = 0; i < items.Count; i++)
            {
                acc += Math.Max(0.0, items[i].w);
                if (roll < acc) return i;
            }

            return items.Count - 1;
        }

        private static List<CardDefinition> RollUniformFallback(
            List<CardDefinition> pool,
            int count,
            bool unique,
            int seed)
        {
            var result = new List<CardDefinition>(Mathf.Max(0, count));
            if (pool == null || pool.Count == 0 || count <= 0) return result;

            System.Random rng = seed != 0 ? new System.Random(seed) : null;

            for (int i = 0; i < count; i++)
            {
                if (pool.Count == 0) break;

                int idx = rng != null ? rng.Next(0, pool.Count) : UnityEngine.Random.Range(0, pool.Count);
                var picked = pool[idx];
                if (picked != null) result.Add(picked);

                if (unique) pool.RemoveAt(idx);
            }

            return result;
        }
    }
}
