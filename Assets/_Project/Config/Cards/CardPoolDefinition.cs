using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDeck.Config.Cards
{
    [CreateAssetMenu(menuName = "DungeonDeck/Cards/Card Pool Definition", fileName = "CardPool_")]
    public class CardPoolDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("메타 저장/해금에서 참조하는 고유 ID")]
        public string poolId = "pool_common";

        [Tooltip("디버그/표시용 이름")]
        public string displayName = "Common Pool";

        [Header("Entries")]
        public List<Entry> entries = new();

        [Serializable]
        public class Entry
        {
            [Tooltip("카드 에셋 (CardDefinition SO든 Prefab이든 상관 없음)")]
            public UnityEngine.Object cardAsset;

            [Min(0)]
            public int weight = 1;

            [Tooltip("비워두면 cardAsset.name 사용")]
            public string overrideName;
        }

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(poolId))
            {
                reason = $"{name}: poolId is empty";
                return false;
            }

            if (entries == null || entries.Count == 0)
            {
                reason = $"{name}: entries is empty";
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.cardAsset == null)
                {
                    reason = $"{name}: entry[{i}] cardAsset is null";
                    return false;
                }
                if (e.weight <= 0)
                {
                    reason = $"{name}: entry[{i}] weight must be > 0";
                    return false;
                }
            }

            reason = "";
            return true;
        }

        public static string GetEntryDisplayName(Entry e)
        {
            if (e == null) return "(null)";
            if (!string.IsNullOrWhiteSpace(e.overrideName)) return e.overrideName;
            return e.cardAsset != null ? e.cardAsset.name : "(null)";
        }
    }
}
