using UnityEngine;
using DungeonDeck.Run;
using DungeonDeck.Config.Map;

namespace DungeonDeck.Battle.View
{
    public class BattleStageSpawner : MonoBehaviour
    {
        [Header("Anchors")]
        public Transform playerAnchor;
        public Transform enemyAnchor;

        [Header("Prefabs")]
        public BattleActorView playerPrefab;
        public BattleActorView enemyPrefab;
        public BattleActorView bossPrefab;

        [Header("Optional")]
        public BattleAnimDirector animDirector;

        public BattleActorView Player { get; private set; }
        public BattleActorView Enemy { get; private set; }

        private void Awake()
        {
            if (playerAnchor == null)
            {
                var t = transform.Find("PlayerAnchor");
                if (t) playerAnchor = t;
            }
            if (enemyAnchor == null)
            {
                var t = transform.Find("EnemyAnchor");
                if (t) enemyAnchor = t;
            }
            if (animDirector == null) animDirector = FindObjectOfType<BattleAnimDirector>(true);
        }

        private void Start()
        {
            Spawn();
        }

        private void Spawn()
        {
            if (playerAnchor == null || enemyAnchor == null)
            {
                Debug.LogError("[BattleStageSpawner] Missing anchors. Create PlayerAnchor/EnemyAnchor.");
                return;
            }

            if (playerPrefab != null)
                Player = Instantiate(playerPrefab, playerAnchor.position, Quaternion.identity, playerAnchor);

            var run = RunSession.I;
            bool isBoss = (run != null && run.PendingBattleType == MapNodeType.Boss);

            var enemyToSpawn = isBoss ? bossPrefab : enemyPrefab;
            if (enemyToSpawn != null)
                Enemy = Instantiate(enemyToSpawn, enemyAnchor.position, Quaternion.identity, enemyAnchor);

            if (animDirector != null)
                animDirector.Bind(Player, Enemy);
        }
    }
}
