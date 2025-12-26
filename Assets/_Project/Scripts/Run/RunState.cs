// Assets/_Project/Scripts/Run/RunState.cs
using System.Collections.Generic;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Run
{
    [System.Serializable]
    public class RunState
    {
        public string oathId;

        public int maxHP;
        public int hp;
        public int gold;

        public int nodeIndex;

        // M1: Keep direct refs (later you can switch to string ids for save-friendly)
        public List<CardDefinition> deck = new List<CardDefinition>();
        
        public List<bool> shopOfferSold = new();   // 슬롯별 SOLD 상태
        public int shopPurchasesMade = 0;          // 이 상점에서 몇 장 샀는지
        public int shopRerollCount = 0; 
        
        // -------------------------
        // Shop persistent state (M1)
        // -------------------------
        public int shopNodeIndex = -1;                  // 어떤 노드의 상점인지 (nodeIndex 기준)
        public int shopSeed = 0;                        // 해당 상점 방문 시드(offer 고정용)
        public List<string> shopOfferIds = new();       // 슬롯별 카드 id
        public List<int> shopOfferRerolls = new();      // 슬롯별 재롤 횟수
        public bool shopRemoveUsed = false;             // 상점 제거 1회 사용 여부
    }
}