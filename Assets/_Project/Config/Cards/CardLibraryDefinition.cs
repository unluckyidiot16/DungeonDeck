using System.Collections.Generic;
using UnityEngine;

namespace DungeonDeck.Config.Cards
{
    [CreateAssetMenu(menuName = "DungeonDeck/Cards/Card Library", fileName = "CardLibraryDefinition")]
    public class CardLibraryDefinition : ScriptableObject
    {
        public List<CardDefinition> cards = new List<CardDefinition>();

        private Dictionary<string, CardDefinition> _map;

        public void Rebuild()
        {
            _map = new Dictionary<string, CardDefinition>();
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c == null) continue; 
                if (string.IsNullOrWhiteSpace(c.id)) continue;
                if (_map.ContainsKey(c.id)) continue;
                _map.Add(c.id, c);
            }
        }
        
        public bool TryGet(string id, out CardDefinition card)
        {
            if (_map == null) Rebuild();
            if (string.IsNullOrWhiteSpace(id))
            {
                card = null;
                return false;
            }
            return _map.TryGetValue(id, out card);
        }
    }
}