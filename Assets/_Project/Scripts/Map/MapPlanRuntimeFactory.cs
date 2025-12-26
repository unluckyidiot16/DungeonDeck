using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Map;

namespace DungeonDeck.Map
{
    public static class MapPlanRuntimeFactory
    {
        public static MapPlanDefinition CreateRuntimePlan(MapPlanDefinition template, int seed)
        {
            int len = (template != null && template.nodes != null && template.nodes.Count > 0)
                ? template.nodes.Count
                : 6;

            var plan = ScriptableObject.CreateInstance<MapPlanDefinition>();
            plan.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

            var rng = new System.Random(Math.Max(1, seed));
            plan.nodes = new List<MapNodeType>(len);

            bool hasShop = false;
            bool hasRest = false;

            for (int i = 0; i < len; i++)
            {
                if (i == 0)
                {
                    plan.nodes.Add(MapNodeType.Battle);
                    continue;
                }
                if (i == len - 1)
                {
                    plan.nodes.Add(MapNodeType.Boss);
                    continue;
                }

                // 가중치: Battle 70 / Shop 20 / Rest 10
                int roll = rng.Next(0, 100);
                MapNodeType t =
                    (roll < 70) ? MapNodeType.Battle :
                    (roll < 90) ? MapNodeType.Shop :
                                  MapNodeType.Rest;

                if (t == MapNodeType.Shop) hasShop = true;
                if (t == MapNodeType.Rest) hasRest = true;

                plan.nodes.Add(t);
            }

            // ✅ 최소 1회 Shop/Rest 보장(길이 충분할 때)
            if (len >= 5)
            {
                if (!hasShop) ForceOne(plan.nodes, rng, MapNodeType.Shop);
                if (!hasRest) ForceOne(plan.nodes, rng, MapNodeType.Rest);
            }

            return plan;
        }

        private static void ForceOne(List<MapNodeType> nodes, System.Random rng, MapNodeType type)
        {
            if (nodes == null || nodes.Count < 3) return;

            // 1..Count-2 범위에서 바꿔치기
            int tries = 20;
            while (tries-- > 0)
            {
                int idx = rng.Next(1, nodes.Count - 1);
                if (nodes[idx] == MapNodeType.Boss) continue;
                if (nodes[idx] == MapNodeType.Battle || nodes[idx] == MapNodeType.Shop || nodes[idx] == MapNodeType.Rest)
                {
                    nodes[idx] = type;
                    return;
                }
            }
        }
    }
}