// Assets/_Project/Scripts/Map/MapController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;
using DungeonDeck.Run;
using DungeonDeck.Config.Map;

namespace DungeonDeck.Map
{
    public class MapController : MonoBehaviour
    {
        [Header("UI")]
        public Transform nodeListRoot;
        public MapNodeView nodePrefab;

        private readonly List<MapNodeView> _spawned = new();

        private void Start()
        {
            BuildOrRebuild();
        }

        public void BuildOrRebuild()
        {
            if (RunSession.I == null || RunSession.I.Plan == null)
            {
                Debug.LogError("[Map] RunSession or Plan missing. Start from Boot scene.");
                return;
            }

            // Clear existing
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();

            var plan = RunSession.I.Plan;
            for (int i = 0; i < plan.nodes.Count; i++)
            {
                var view = Instantiate(nodePrefab, nodeListRoot);
                _spawned.Add(view);

                bool cleared = RunSession.I.IsNodeCleared(i);
                bool isCurrent = (RunSession.I.State.nodeIndex == i);

                int idx = i;
                view.Bind(idx, plan.nodes[idx], cleared, isCurrent, () => OnNodeClicked(idx));
            }
        }

        private void OnNodeClicked(int index)
        {
            RunSession.I.EnterNode(index);

            var type = RunSession.I.GetNodeType(index);
            switch (type)
            {
                case MapNodeType.Battle:
                case MapNodeType.Boss:
                    SceneManager.LoadScene(SceneRoutes.Battle);
                    break;

                case MapNodeType.Shop:
                    SceneManager.LoadScene(SceneRoutes.Shop); // Shop 씬 라우트 필요
                    break;

                case MapNodeType.Rest:
                    Debug.Log("[Map] Rest node - M1 stub: heal and advance.");
                    if (RunSession.I.State != null)
                        RunSession.I.State.hp = Mathf.Min(RunSession.I.State.maxHP, RunSession.I.State.hp + 10);

                    RunSession.I.MarkNodeClearedAndAdvance();
                    BuildOrRebuild();
                    break;
            }
        }
    }
}
