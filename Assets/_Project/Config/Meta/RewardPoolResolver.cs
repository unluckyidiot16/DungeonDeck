using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Config.Meta
{
    /// <summary>
    /// "서약 기본 풀 + 메타 해금 풀"을 합쳐서 이번 Run에서 사용할 풀 리스트를 만드는 리졸버.
    /// - knownPools에 프로젝트의 모든 "추가 풀"을 등록해두면, meta.unlockedPoolIds로 찾아 붙임.
    /// </summary>
    [CreateAssetMenu(menuName = "DungeonDeck/Meta/Reward Pool Resolver", fileName = "RewardPoolResolver")]
    public class RewardPoolResolver : ScriptableObject
    {
        [Header("Known (Unlockable) Pools")]
        public List<CardPoolDefinition> knownPools = new();

        /// <summary>
        /// oathBasePool: 서약 기본 풀(필수)
        /// meta: 해금 진행도(없으면 null 가능)
        /// </summary>
        public List<CardPoolDefinition> ResolvePools(CardPoolDefinition oathBasePool, PlayerMetaProgress meta)
        {
            var result = new List<CardPoolDefinition>();

            if (oathBasePool != null)
                result.Add(oathBasePool);

            if (meta == null || meta.unlockedPoolIds == null || meta.unlockedPoolIds.Count == 0)
                return result;

            // 빠른 조회용
            Dictionary<string, CardPoolDefinition> map = null;
            if (knownPools != null && knownPools.Count > 0)
            {
                map = new Dictionary<string, CardPoolDefinition>(knownPools.Count);
                foreach (var p in knownPools)
                {
                    if (p == null) continue;
                    if (string.IsNullOrWhiteSpace(p.poolId)) continue;
                    map[p.poolId] = p;
                }
            }

            foreach (var id in meta.unlockedPoolIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (map == null) break;

                if (map.TryGetValue(id, out var pool) && pool != null)
                {
                    if (!result.Contains(pool))
                        result.Add(pool);
                }
            }

            return result;
        }

        public CardPoolDefinition FindKnownPool(string poolId)
        {
            if (string.IsNullOrWhiteSpace(poolId)) return null;
            if (knownPools == null) return null;

            for (int i = 0; i < knownPools.Count; i++)
            {
                var p = knownPools[i];
                if (p == null) continue;
                if (p.poolId == poolId) return p;
            }
            return null;
        }
    }
}
