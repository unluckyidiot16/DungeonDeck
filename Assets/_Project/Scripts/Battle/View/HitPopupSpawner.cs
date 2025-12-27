using UnityEngine;

namespace DungeonDeck.Battle.View
{
    public class HitPopupSpawner : MonoBehaviour
    {
        public Canvas canvas;
        public RectTransform root; // (기존) 스크린 변환 모드용

        [Header("Popup")]
        public HitPopup popupPrefab;

        [Header("Targets (fallback)")]
        public Transform playerTarget;
        public Transform enemyTarget;

        [Header("Per-Actor Canvas (preferred)")]
        public RectTransform playerCanvasRoot; // ✅ 플레이어 캔버스 루트
        public RectTransform enemyCanvasRoot;  // ✅ 적 캔버스 루트
        public Vector2 localOffset = new Vector2(0, 40);

        [Header("Screen Offset (fallback)")]
        public Vector2 screenOffset = new Vector2(0, 40);

        private Camera _uiCam;

        private void Awake()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();

            if (root == null && canvas != null)
                root = canvas.transform as RectTransform;

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _uiCam = canvas.worldCamera;
        }

        public void SpawnPlayer(int amount) => Spawn(amount, playerCanvasRoot, playerTarget);
        public void SpawnEnemy(int amount) => Spawn(amount, enemyCanvasRoot, enemyTarget);

        private void Spawn(int amount, RectTransform preferredRoot, Transform fallbackTarget)
        {
            if (popupPrefab == null) return;

            // ✅ 1) per-actor canvas가 있으면 그 위에 “로컬”로 띄우기
            if (preferredRoot != null)
            {
                var p = Instantiate(popupPrefab, preferredRoot);
                var rt = p.transform as RectTransform;
                if (rt != null) rt.anchoredPosition = localOffset;
                p.Play(amount);
                return;
            }

            // ✅ 2) 없으면 기존 방식(월드→스크린→UI 로컬)
            if (root == null || fallbackTarget == null) return;

            var popup = Instantiate(popupPrefab, root);
            var prt = popup.transform as RectTransform;
            if (prt == null) return;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_uiCam, fallbackTarget.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, _uiCam, out var local);
            prt.anchoredPosition = local + screenOffset;

            popup.Play(amount);
        }
    }
}
