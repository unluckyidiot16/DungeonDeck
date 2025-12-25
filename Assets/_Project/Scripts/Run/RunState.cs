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
    }
}