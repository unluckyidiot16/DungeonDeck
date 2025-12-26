// Assets/_Project/Scripts/Run/RunState.cs
using System.Collections.Generic;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Run
{
    [System.Serializable]
    public class RunState
    {
        public string oathId;

        public int seed = 0;
        
        public int maxHP;
        public int hp;
        public int gold;

        public int nodeIndex;

        public int runClearedBattles = 0;
        
        // 전투 승리 보상 롤 횟수(세이브/재진입 시 같은 보상 반복 방지용 salt)
        public int rewardRollCount = 0;
        
        public RunEndOutcome lastOutcome = RunEndOutcome.None;
        
        // M1: Keep direct refs (later you can switch to string ids for save-friendly)
        public List<CardDefinition> deck = new List<CardDefinition>();
        
        public List<bool> shopOfferSold = new();   // 슬롯별 SOLD 상태
        public int shopRerollCount = 0; 
        
        // -------------------------
        // Shop persistent state (M1)
        // -------------------------
        public int shopNodeIndex = -1;                  // 어떤 노드의 상점인지 (nodeIndex 기준)
        public int shopSeed = 0;                        // 해당 상점 방문 시드(offer 고정용)
        public List<string> shopOfferIds = new();       // 슬롯별 카드 id
        public bool shopRemoveUsed = false;             // 상점 제거 1회 사용 여부
    }
}