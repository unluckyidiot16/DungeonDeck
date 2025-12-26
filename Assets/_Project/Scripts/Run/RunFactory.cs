// Assets/_Project/Scripts/Run/RunFactory.cs
using DungeonDeck.Config.Balance;
using DungeonDeck.Config.Oaths;
using UnityEngine;

namespace DungeonDeck.Run
{
    public static class RunFactory
    {
        public static RunState CreateNewRun(OathDefinition oath, RunBalanceDefinition balance)
        {
            var s = new RunState();
            s.lastOutcome = RunEndOutcome.None;
            s.oathId = oath != null ? oath.id : "unknown";

            // ✅ 런 고정 시드(보상/이벤트/전투 RNG 등에 사용)
            s.seed = Random.Range(1, int.MaxValue);
            if (s.seed == 0) s.seed = 1;
            
            s.maxHP = balance != null ? balance.startMaxHP : 60;
            s.hp = s.maxHP;

            s.gold = balance != null ? balance.startGold : 0;
            s.nodeIndex = 0;
            s.runClearedBattles = 0;

            if (oath != null && oath.startDeck != null)
                s.deck.AddRange(oath.startDeck);

            return s;
        }
    }
}