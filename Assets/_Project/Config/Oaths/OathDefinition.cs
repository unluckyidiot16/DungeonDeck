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
    }
}