// Assets/_Project/Scripts/Run/RunFactory.cs
using DungeonDeck.Config.Balance;
using DungeonDeck.Config.Oaths;

namespace DungeonDeck.Run
{
    public static class RunFactory
    {
        public static RunState CreateNewRun(OathDefinition oath, RunBalanceDefinition balance)
        {
            var s = new RunState();
            s.oathId = oath != null ? oath.id : "unknown";

            s.maxHP = balance != null ? balance.startMaxHP : 60;
            s.hp = s.maxHP;

            s.gold = balance != null ? balance.startGold : 0;
            s.nodeIndex = 0;

            if (oath != null && oath.startDeck != null)
                s.deck.AddRange(oath.startDeck);

            return s;
        }
    }
}