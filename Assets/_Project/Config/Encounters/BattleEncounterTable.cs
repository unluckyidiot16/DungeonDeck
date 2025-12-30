using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDeck.Config.Encounters
{
    [CreateAssetMenu(menuName = "DungeonDeck/Encounters/Encounter Table", fileName = "EncounterTable_")]
    public class BattleEncounterTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public BattleEncounterDefinition encounter;
            [Min(0)] public int weight = 1;
        }

        public List<Entry> entries = new();

        public BattleEncounterDefinition Roll(int seed)
        {
            var rng = new System.Random(seed != 0 ? seed : 1);
            return Roll(rng);
        }

        public BattleEncounterDefinition Roll(System.Random rng)
        {
            if (entries == null || entries.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.encounter == null) continue;
                if (e.weight <= 0) continue;
                total += e.weight;
            }

            if (total <= 0) return null;

            int roll = rng.Next(0, total);
            int acc = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.encounter == null) continue;
                if (e.weight <= 0) continue;

                acc += e.weight;
                if (roll < acc)
                    return e.encounter;
            }

            // fallback
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e != null && e.encounter != null && e.weight > 0)
                    return e.encounter;
            }
            return null;
        }
    }
}