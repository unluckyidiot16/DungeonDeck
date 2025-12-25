using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Rewards
{
    public static class CardRewardRoller
    {
        public struct RewardOption
        {
            public UnityEngine.Object cardAsset;
            public string displayName;
        }

        private class WeightedItem
        {
            public UnityEngine.Object asset;
            public string name;
            public int weight;
        }

        /// <summary>
        /// 여러 풀을 합성해서 count개를 가중치 랜덤으로 뽑음 (기본: 중복 방지).
        /// </summary>
        public static List<RewardOption> RollOptions(
            IReadOnlyList<CardPoolDefinition> pools,
            int count = 3,
            bool unique = true,
            int seed = 0)
        {
            var rng = seed != 0 ? new System.Random(seed) : null;

            var combined = CombinePools(pools);
            var picked = new List<RewardOption>(Mathf.Max(0, count));

            if (combined.Count == 0 || count <= 0)
                return picked;

            // unique면 뽑을 때 제거
            for (int i = 0; i < count; i++)
            {
                if (combined.Count == 0) break;

                int idx = PickWeightedIndex(combined, rng);
                if (idx < 0 || idx >= combined.Count) break;

                var w = combined[idx];
                if (w.asset == null)
                {
                    combined.RemoveAt(idx);
                    i--;
                    continue;
                }

                picked.Add(new RewardOption
                {
                    cardAsset = w.asset,
                    displayName = w.name
                });

                if (unique)
                    combined.RemoveAt(idx);
            }

            return picked;
        }

        private static List<WeightedItem> CombinePools(IReadOnlyList<CardPoolDefinition> pools)
        {
            // asset instance id 기준으로 weight 합산
            var map = new Dictionary<int, WeightedItem>();
            var list = new List<WeightedItem>();

            if (pools == null) return list;

            for (int p = 0; p < pools.Count; p++)
            {
                var pool = pools[p];
                if (pool == null || pool.entries == null) continue;

                for (int i = 0; i < pool.entries.Count; i++)
                {
                    var e = pool.entries[i];
                    if (e == null || e.cardAsset == null) continue;
                    if (e.weight <= 0) continue;

                    int id = e.cardAsset.GetInstanceID();
                    string n = CardPoolDefinition.GetEntryDisplayName(e);

                    if (map.TryGetValue(id, out var existing))
                    {
                        existing.weight += e.weight;
                        // 이름은 처음 들어온 것을 유지
                    }
                    else
                    {
                        var w = new WeightedItem
                        {
                            asset = e.cardAsset,
                            name = n,
                            weight = e.weight
                        };
                        map[id] = w;
                        list.Add(w);
                    }
                }
            }

            // weight 0 제거
            for (int k = list.Count - 1; k >= 0; k--)
            {
                if (list[k].asset == null || list[k].weight <= 0)
                    list.RemoveAt(k);
            }

            return list;
        }

        private static int PickWeightedIndex(List<WeightedItem> items, System.Random rng)
        {
            long total = 0;
            for (int i = 0; i < items.Count; i++)
                total += Math.Max(0, items[i].weight);

            if (total <= 0) return -1;

            long roll = rng != null ? NextLong(rng, total) : UnityRoll(total);

            long acc = 0;
            for (int i = 0; i < items.Count; i++)
            {
                acc += Math.Max(0, items[i].weight);
                if (roll < acc) return i;
            }

            return items.Count - 1;
        }

        private static long UnityRoll(long totalExclusive)
        {
            // UnityEngine.Random은 int 기반이라 안전하게 분해
            // totalExclusive가 int 범위를 넘는 경우에도 대충 안전하게 동작하도록 2회 합성
            if (totalExclusive <= int.MaxValue)
                return UnityEngine.Random.Range(0, (int)totalExclusive);

            int hi = UnityEngine.Random.Range(0, int.MaxValue);
            int lo = UnityEngine.Random.Range(0, int.MaxValue);
            ulong combined = ((ulong)(uint)hi << 32) | (uint)lo;
            return (long)(combined % (ulong)totalExclusive);
        }
        
        private static long NextLong(System.Random rng, long maxExclusive)
        {
            // [0, maxExclusive) 범위
            if (maxExclusive <= 0) return 0;

            // .NET Random.Next()는 [0, int.MaxValue)
            // 두 번 뽑아서 62bit 정도 확보 후 mod
            long a = (long)rng.Next(); // 31 bits
            long b = (long)rng.Next(); // 31 bits
            ulong u = ((ulong)a << 31) | (ulong)b; // 62 bits

            return (long)(u % (ulong)maxExclusive);
        }

        
    }
}
