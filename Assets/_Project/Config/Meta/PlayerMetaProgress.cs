using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDeck.Config.Meta
{
    /// <summary>
    /// 업적/가챠/해금 진행도 최소형.
    /// - unlockedPoolIds: 해금된 CardPoolDefinition.poolId 목록
    /// </summary>
    [Serializable]
    public class PlayerMetaProgress
    {
        public int version = 1;
        public List<string> unlockedPoolIds = new();

        private const string SaveKey = "DungeonDeck.MetaProgress.v1";

        public static PlayerMetaProgress LoadOrCreate()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
                return new PlayerMetaProgress();

            var json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrWhiteSpace(json))
                return new PlayerMetaProgress();

            try
            {
                var loaded = JsonUtility.FromJson<PlayerMetaProgress>(json);
                return loaded ?? new PlayerMetaProgress();
            }
            catch
            {
                return new PlayerMetaProgress();
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this);
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaProgress] Save failed: {e}");
            }
        }

        public bool IsUnlocked(string poolId)
        {
            if (string.IsNullOrWhiteSpace(poolId)) return false;
            return unlockedPoolIds != null && unlockedPoolIds.Contains(poolId);
        }

        public void UnlockPool(string poolId)
        {
            if (string.IsNullOrWhiteSpace(poolId)) return;

            unlockedPoolIds ??= new List<string>();
            if (!unlockedPoolIds.Contains(poolId))
                unlockedPoolIds.Add(poolId);
        }
    }
}