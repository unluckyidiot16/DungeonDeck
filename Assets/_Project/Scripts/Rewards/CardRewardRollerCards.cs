using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Rewards
{
    public static class CardRewardRollerCards
    {
        /// <summary>
        /// 후보 리스트에서 count개를 랜덤으로 뽑음 (기본: 중복 방지)
        /// </summary>
        public static List<CardDefinition> Roll(
            IReadOnlyList<CardDefinition> candidates,
            int count = 3,
            bool unique = true,
            int seed = 0)
        {
            var result = new List<CardDefinition>(Mathf.Max(0, count));
            if (candidates == null || candidates.Count == 0 || count <= 0) return result;

            // 유효 카드만 복사
            var pool = new List<CardDefinition>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i] != null) pool.Add(candidates[i]);

            if (pool.Count == 0) return result;

            System.Random rng = seed != 0 ? new System.Random(seed) : null;

            for (int i = 0; i < count; i++)
            {
                if (pool.Count == 0) break;

                int idx = rng != null
                    ? rng.Next(0, pool.Count)
                    : UnityEngine.Random.Range(0, pool.Count);

                var picked = pool[idx];
                if (picked != null) result.Add(picked);

                if (unique) pool.RemoveAt(idx);
            }

            return result;
        }
    }
}