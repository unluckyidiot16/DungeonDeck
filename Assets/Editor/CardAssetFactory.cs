// Assets/_Project/Editor/CardAssetFactory.cs
using System.IO;
using UnityEditor;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.EditorTools
{
    public static class CardAssetFactory
    {
        private const string DefaultFolder = "Assets/_Project/Config/Cards/Defs";

        [MenuItem("DungeonDeck/M1/Create Starter Cards")]
        public static void CreateStarterCards()
        {
            EnsureFolder(DefaultFolder);

            // M1 최소 4장 (원하면 더 추가 가능)
            CreateOrUpdateCard(DefaultFolder, "Strike",    "strike",     1, CardEffectKind.Attack,     6);
            CreateOrUpdateCard(DefaultFolder, "Defend",    "defend",     1, CardEffectKind.Block,      5);
            CreateOrUpdateCard(DefaultFolder, "QuickDraw", "quick_draw", 1, CardEffectKind.Draw,       2);
            CreateOrUpdateCard(DefaultFolder, "Charge",    "charge",     0, CardEffectKind.GainEnergy, 1);

            // 옵션: 초반 재미용 2장 (원치 않으면 지워도 됨)
            CreateOrUpdateCard(DefaultFolder, "HeavyStrike", "heavy_strike", 2, CardEffectKind.Attack, 12);
            CreateOrUpdateCard(DefaultFolder, "Fortify",     "fortify",      1, CardEffectKind.Block,   8);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CardAssetFactory] Starter cards created/updated in: {DefaultFolder}");
        }

        private static void CreateOrUpdateCard(string folder, string assetName, string id, int cost, CardEffectKind kind, int value)
        {
            string path = $"{folder}/{assetName}.asset";

            CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            bool isNew = false;

            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardDefinition>();
                isNew = true;
            }

            card.id = id;
            card.cost = cost;
            card.effectKind = kind;
            card.value = value;

            EditorUtility.SetDirty(card);

            if (isNew)
            {
                AssetDatabase.CreateAsset(card, path);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            // "Assets/..." 형태의 폴더를 단계적으로 생성
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
