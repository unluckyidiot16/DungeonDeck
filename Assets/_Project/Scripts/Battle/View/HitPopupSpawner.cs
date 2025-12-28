using UnityEngine;

namespace DungeonDeck.Battle.View
{
    public class HitPopupSpawner : MonoBehaviour
    {
        [Header("Popup")]
        public HitPopup popupPrefab;

        [Header("Player")]
        public Transform playerTarget;                 // 월드 기준(보통 PlayerAnchor 혹은 Canvas/Target)
        public RectTransform playerCanvasRoot;         // PlayerAnchor의 Canvas (RectTransform)

        [Header("Enemies (by index)")]
        public Transform[] enemyTargets = new Transform[3];          // 각 적의 Target(월드)
        public RectTransform[] enemyCanvasRoots = new RectTransform[3]; // 각 적의 Canvas (RectTransform)

        [Header("Offsets")]
        public Vector2 localOffset = new Vector2(0, 40); // 캔버스 로컬 좌표 오프셋

        // -------------------------
        // Public API
        // -------------------------

        public void RegisterEnemyTarget(int index, Transform target, RectTransform canvasRoot)
        {
            if (index < 0 || index >= enemyTargets.Length) return;

            enemyTargets[index] = target;
            // ✅ null이면 기존 canvasRoot 유지 (TargetManager가 null로 덮어쓰는 문제 방지)
            if (canvasRoot != null && enemyCanvasRoots != null && index < enemyCanvasRoots.Length) 
                enemyCanvasRoots[index] = canvasRoot;
        }

        public void SpawnPlayer(int amount)
        {
            SpawnOnCanvas(amount, playerCanvasRoot, playerTarget);
        }

        public void SpawnEnemy(int amount)
        {
            SpawnEnemy(amount, 0);
        }

        public void SpawnEnemy(int amount, int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= enemyTargets.Length) return;

            var canvasRoot = (enemyCanvasRoots != null && enemyIndex < enemyCanvasRoots.Length)
                ? enemyCanvasRoots[enemyIndex]
                : null;

            var target = enemyTargets[enemyIndex];

            SpawnOnCanvas(amount, canvasRoot, target);
        }

        // -------------------------
        // Core
        // -------------------------

        private void SpawnOnCanvas(int amount, RectTransform canvasRoot, Transform worldTarget)
        {
            if (popupPrefab == null) return;
            if (canvasRoot == null) return; // ✅ 이번 요구사항: 각자 캔버스에 스폰 (없으면 안 띄움)

            var popup = Instantiate(popupPrefab, canvasRoot);
            if (popup.transform is not RectTransform rt) return;

            // target이 없으면 그냥 localOffset 위치
            if (worldTarget == null)
            {
                rt.anchoredPosition = localOffset;
                popup.Play(amount);
                return;
            }

            // target 월드 위치 → 해당 canvasRoot의 로컬 좌표로 변환
            var cam = GetCamFor(canvasRoot);

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldTarget.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screen, cam, out var local);

            rt.anchoredPosition = local + localOffset;
            popup.Play(amount);
        }

        private Camera GetCamFor(RectTransform canvasRoot)
        {
            if (canvasRoot == null) return null;

            var c = canvasRoot.GetComponentInParent<Canvas>();
            if (c == null) return null;

            // ScreenSpaceOverlay면 camera null
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return null;

            // ScreenSpaceCamera/WorldSpace면 worldCamera 사용, 없으면 Main
            if (c.worldCamera != null) return c.worldCamera;
            return Camera.main;
        }
    }
}
