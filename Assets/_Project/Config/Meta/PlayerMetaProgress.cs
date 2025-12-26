using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDeck.Config.Meta
{
    /// <summary>
    /// 업적/가챠/해금 진행도 최소형.
    /// - unlockedPoolIds: 해금된 CardPoolDefinition.poolId 목록
    /// - startGoldBonus/startMaxHpBonus/startBonusCardIds: 런 시작 보너스(영구 업그레이드)
    /// </summary>
    [Serializable]
    public class PlayerMetaProgress
    {
        public int version = 2;
        public List<string> unlockedPoolIds = new();
        
        [Header("Run Start Bonuses (Persistent)")]
        public int startGoldBonus = 0;
        public int startMaxHpBonus = 0;
        public List<string> startBonusCardIds = new(); // CardDefinition.id

        private const string SaveKey = "DungeonDeck.MetaProgress.v1";

        public static PlayerMetaProgress LoadOrCreate()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
                return new PlayerMetaProgress().Normalize();

            var json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrWhiteSpace(json))
                return new PlayerMetaProgress().Normalize();

            try
            {
                var loaded = JsonUtility.FromJson<PlayerMetaProgress>(json);
                if (loaded == null) return new PlayerMetaProgress().Normalize();
                
                // 구버전 마이그레이션 (필드 추가만 했으니 normalize로 충분)
                if (loaded.version < 2) loaded.version = 2;
                return loaded.Normalize();
            }
            catch
            {
                return new PlayerMetaProgress().Normalize();
            }
        }

        public void Save()
        {
            try
            {
                Normalize();
                var json = JsonUtility.ToJson(this);
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaProgress] Save failed: {e}");
            }
        }

        private PlayerMetaProgress Normalize()
        {
            unlockedPoolIds ??= new List<string>();
            startBonusCardIds ??= new List<string>();
            return this;
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