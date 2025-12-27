using System.Collections.Generic;
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
        public HitPopupSpawner hitPopups;

        [Header("Auto Bind Popup Target")]
        [Tooltip("Find() path under actor root to locate popup target. Ex) Canvas/Target")]
        public string popupTargetPath = "Canvas/Target";

        [Tooltip("If a RectTransform target is found, spawn popup under that target (local UI). Otherwise use world->screen conversion.")]
        public bool preferLocalCanvasTarget = true;

        public BattleActorView Player { get; private set; }
        public BattleActorView Enemy { get; private set; } // legacy: first enemy

        private readonly List<BattleActorView> _enemies = new List<BattleActorView>(3);

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
            if (hitPopups == null) hitPopups = FindObjectOfType<HitPopupSpawner>(true);
        }

        private void Start()
        {
            Spawn();
            AutoBindViewsAndPopups();
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
        }

        private void AutoBindViewsAndPopups()
        {
            // -------- collect enemies (supports future multi spawn) --------
            _enemies.Clear();
            if (enemyAnchor != null)
            {
                var found = enemyAnchor.GetComponentsInChildren<BattleActorView>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    var e = found[i];
                    if (e == null) continue;
                    _enemies.Add(e);
                }

                // stable order: left -> right
                _enemies.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            }

            // legacy
            if (Enemy == null && _enemies.Count > 0) Enemy = _enemies[0];

            // -------- anim director bind --------
            if (animDirector != null)
            {
                if (_enemies.Count > 0) animDirector.Bind(Player, _enemies);
                else animDirector.Bind(Player, (IList<BattleActorView>)null);
            }

            // -------- hit popup bind --------
            if (hitPopups != null)
            {
                // Player target: prefer actor target, fallback to anchor
                Transform pTarget = FindPopupTarget(Player != null ? Player.transform : null)
                                 ?? FindPopupTarget(playerAnchor)
                                 ?? (Player != null ? Player.transform : playerAnchor);

                var pRt = (preferLocalCanvasTarget && pTarget is RectTransform) ? (RectTransform)pTarget : null;

                hitPopups.playerTarget = pTarget;
                hitPopups.playerCanvasRoot = pRt;

                // Enemies
                for (int i = 0; i < 3; i++)
                {
                    Transform eTarget = null;
                    RectTransform eRt = null;

                    if (i < _enemies.Count && _enemies[i] != null)
                    {
                        eTarget = FindPopupTarget(_enemies[i].transform)
                                 ?? FindPopupTarget(enemyAnchor)
                                 ?? _enemies[i].transform;

                        eRt = (preferLocalCanvasTarget && eTarget is RectTransform) ? (RectTransform)eTarget : null;
                    }
                    else
                    {
                        // clear stale bindings
                        eTarget = null;
                        eRt = null;
                    }

                    hitPopups.RegisterEnemyTarget(i, eTarget, eRt);
                }
            }
        }

        private Transform FindPopupTarget(Transform root)
        {
            if (root == null) return null;

            // 1) path
            if (!string.IsNullOrEmpty(popupTargetPath))
            {
                var t = root.Find(popupTargetPath);
                if (t != null) return t;
            }

            // 2) common names
            var direct = root.Find("Target");
            if (direct != null) return direct;

            // 3) deep search by name (one-time, small)
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t != null && t.name == "Target") return t;
            }

            return null;
        }
    }
}
