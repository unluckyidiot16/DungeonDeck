using UnityEngine;

namespace DungeonDeck.Battle.View
{
    public class HitPopupSpawner : MonoBehaviour
    {
        [Header("UI Root")]
        public Canvas canvas;
        public RectTransform root;

        [Header("Popup")]
        public HitPopup popupPrefab;

        [Header("Player Target")]
        public Transform playerTarget;
        public RectTransform playerCanvasRoot;

        [Header("Enemy Targets (by index)")]
        public Transform[] enemyTargets = new Transform[3];
        public RectTransform[] enemyCanvasRoots = new RectTransform[3];

        [Header("Offsets")]
        public Vector2 screenOffset = new Vector2(0, 40);
        public Vector2 localOffset = new Vector2(0, 40);

        Camera _uiCam;

        private void Awake()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (root == null && canvas != null) root = canvas.transform as RectTransform;

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _uiCam = canvas.worldCamera;
        }

        // ---- Registration helpers (optional) ----
        public void RegisterEnemyTarget(int index, Transform t, RectTransform canvasRootOpt = null)
        {
            if (index < 0) return;
            if (enemyTargets == null || enemyTargets.Length <= index) return;

            enemyTargets[index] = t;
            if (enemyCanvasRoots != null && enemyCanvasRoots.Length > index)
                enemyCanvasRoots[index] = canvasRootOpt;
        }

        // ---- Spawn API ----
        public void SpawnPlayer(int amount)
        {
            SpawnInternal(amount, playerCanvasRoot, playerTarget);
        }

        // Backward compatible (old call sites)
        public void SpawnEnemy(int amount)
        {
            SpawnEnemy(amount, 0);
        }

        public void SpawnEnemy(int amount, int enemyIndex)
        {
            Transform t = null;
            RectTransform cr = null;

            if (enemyTargets != null && enemyIndex >= 0 && enemyIndex < enemyTargets.Length)
                t = enemyTargets[enemyIndex];

            if (enemyCanvasRoots != null && enemyIndex >= 0 && enemyIndex < enemyCanvasRoots.Length)
                cr = enemyCanvasRoots[enemyIndex];

            // fallback: 없으면 root+enemyTargets[0] 같은 식으로라도
            SpawnInternal(amount, cr, t);
        }

        private void SpawnInternal(int amount, RectTransform preferredRoot, Transform worldTarget)
        {
            if (popupPrefab == null) return;

            // 1) per-actor canvas root 우선 (월드/카메라 영향 최소)
            if (preferredRoot != null)
            {
                var p = Instantiate(popupPrefab, preferredRoot);
                if (p.transform is RectTransform prt)
                    prt.anchoredPosition = localOffset;

                p.Play(amount);
                return;
            }

            // 2) fallback: 월드→스크린→root 로컬
            if (root == null || worldTarget == null) return;

            var popup = Instantiate(popupPrefab, root);
            if (popup.transform is not RectTransform rt) return;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_uiCam, worldTarget.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, _uiCam, out var local);
            rt.anchoredPosition = local + screenOffset;

            popup.Play(amount);
        }
    }
}
