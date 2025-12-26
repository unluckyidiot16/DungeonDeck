// Assets/_Project/Config/Oaths/OathDefinition.cs
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Config.Oaths
{
    [CreateAssetMenu(menuName = "DungeonDeck/Oaths/Oath", fileName = "OathDefinition")]
    public class OathDefinition : ScriptableObject
    {
        public string id = "oath_a";
        public string displayName = "Oath A";

        [Header("Starting Deck (M1)")]
        public List<CardDefinition> startDeck = new List<CardDefinition>();
        
        [Header("Card Pools (Reward/Shop)")]
        [Tooltip("이 서약이 기본적으로 사용할 카드 풀들.\n예: [CommonPool, OathSpecificPool] 같은 식으로 여러 개를 넣어도 됨")]
        public List<CardPoolDefinition> basePools = new();
            
        [Tooltip("(선택) 이 서약이 '해금'을 통해 추가로 받을 수 있는 풀 ID들(디자인용).\n실제 적용은 PlayerMetaProgress.unlockedPoolIds + RewardPoolResolver.knownPools 기준")]
        public List<string> unlockablePoolIds = new();
        
    }
}