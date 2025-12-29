using System.Collections;
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
        
        [Header("Enemy Spawn")]
        public float enemySpacing = 2.2f;
        public DungeonDeck.Battle.BattleController battle;

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

        private IEnumerator Start()
        {
            // BattleController.Start()에서 EnemyCount 세팅 끝난 다음 스폰하기 위해 1프레임 대기
            yield return null;
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

            // clear (중복 스폰 방지)
            for (int i = playerAnchor.childCount - 1; i >= 0; i--) Destroy(playerAnchor.GetChild(i).gameObject);
            for (int i = enemyAnchor.childCount - 1; i >= 0; i--) Destroy(enemyAnchor.GetChild(i).gameObject);
            
            if (playerPrefab != null)
            { 
                Player = Instantiate(playerPrefab, playerAnchor);
                Player.transform.localPosition = Vector3.zero;
                Player.transform.localRotation = Quaternion.identity;
            }

            var run = RunSession.I;
            bool isBoss = (run != null && run.PendingBattleType == MapNodeType.Boss);

            var enemyToSpawn = isBoss ? bossPrefab : enemyPrefab;
            if (battle == null) battle = FindObjectOfType<DungeonDeck.Battle.BattleController>(true);
            
            int count = 1;
            if (!isBoss && battle != null && battle.EnemyCount > 0)
                count = Mathf.Clamp(battle.EnemyCount, 1, 3);
            
            _enemies.Clear();
            if (enemyToSpawn != null)
            {
                float center = (count - 1) * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    var e = Instantiate(enemyToSpawn, enemyAnchor);
                    e.transform.localRotation = Quaternion.identity;
                    e.transform.localPosition = new Vector3((i - center) * enemySpacing, 0f, 0f);
                    _enemies.Add(e);
                }
            }
            
            Enemy = (_enemies.Count > 0) ? _enemies[0] : null;
        }

        private void AutoBindViewsAndPopups()
        {
            // stable order: left -> right
            _enemies.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            // legacy
            if (Enemy == null && _enemies.Count > 0) Enemy = _enemies[0];

            // -------- anim director bind --------
            if (animDirector != null)
            {
                if (_enemies.Count > 0) animDirector.Bind(Player, _enemies);
                else animDirector.Bind(Player, (IList<BattleActorView>)null);

                // ✅ 여기서 Apply하면 타이밍 100% 안전 (playerAnimator 확보된 뒤)
                var run = RunSession.I;
                string oathId = run?.State?.oathId;
                if (string.IsNullOrEmpty(oathId)) oathId = run?.Oath?.id;

                animDirector.ApplyOathAnimatorOverride(oathId);
            }
            
            // -------- hit popup bind --------
            if (hitPopups != null)
            {
                // Player target: prefer actor target, fallback to anchor
                Transform pTarget = FindPopupTarget(Player != null ? Player.transform : null)
                                    ?? (Player != null ? Player.transform : playerAnchor);
                
                var pCanvas = FindCanvasRoot(Player != null ? Player.transform : null);
                hitPopups.playerTarget = pTarget;
                hitPopups.playerCanvasRoot = pCanvas;

                // Enemies
                for (int i = 0; i < 3; i++)
                {
                    Transform eTarget = null;
                    RectTransform eCanvas = null;

                    if (i < _enemies.Count && _enemies[i] != null)
                    {
                        eTarget = FindPopupTarget(_enemies[i].transform)
                                 ?? FindPopupTarget(enemyAnchor)
                                 ?? _enemies[i].transform;

                        eCanvas = FindCanvasRoot(_enemies[i].transform);
                    }
                    else
                    {
                        // clear stale bindings
                        eTarget = null;
                        eCanvas = null;
                    }

                    hitPopups.RegisterEnemyTarget(i, eTarget, eCanvas);
                }
            }
        }
        
        private RectTransform FindCanvasRoot(Transform actorRoot)
        {
            if (actorRoot == null) return null;
            var c = actorRoot.GetComponentInChildren<Canvas>(true);
            return c != null ? c.transform as RectTransform : null;
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
