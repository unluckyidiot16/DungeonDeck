// Assets/_Project/Config/Map/MapPlanDefinition.cs
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDeck.Config.Map
{
    public enum MapNodeType
    {
        Battle,
        Shop,
        Rest,
        Boss
    }

    [CreateAssetMenu(menuName = "DungeonDeck/Map/Map Plan", fileName = "MapPlanDefinition")]
    public class MapPlanDefinition : ScriptableObject
    {
        [Tooltip("M1: fixed nodes, e.g. Battle,Battle,Shop,Battle,Rest,Boss")]
        public List<MapNodeType> nodes = new List<MapNodeType>()
        {
            MapNodeType.Battle,
            MapNodeType.Battle,
            MapNodeType.Shop,
            MapNodeType.Battle,
            MapNodeType.Rest,
            MapNodeType.Boss
        };
    }
}