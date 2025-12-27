using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Battle;
using DG.Tweening;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.UI.Battle
{
    public class BattleHandUI : MonoBehaviour
    {
        public BattleController battle;
        public Transform handRoot;

        public BattleCardButtonView cardPrefab;

        public TMP_Text energyText;
        public TMP_Text playerHpText;
        public TMP_Text enemyHpText;
        public Button endTurnButton;

        [Header("FX Anchors")]
        public RectTransform flyRoot;          // 카드가 날아다닐 레이어(캔버스 안). 비우면 자동 탐색
        public RectTransform discardAnchor;    // Discard 더미 위치(캔버스 안)
        public float discardFlyDuration = 0.28f;

        public int maxHandSlots = 5;

        private readonly List<BattleCardButtonView> _slots = new();

        public DungeonDeck.Battle.View.BattleAnimDirector animDirector;

        private bool _busy = false;
        private Canvas _canvas;
        private Camera _uiCam;

        private void Awake()
        {
            if (battle == null) battle = FindObjectOfType<BattleController>();
            if (handRoot == null) handRoot = transform;

            if (animDirector == null)
                animDirector = FindObjectOfType<DungeonDeck.Battle.View.BattleAnimDirector>(true);

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _uiCam = _canvas.worldCamera;

            if (flyRoot == null && _canvas != null)
                flyRoot = _canvas.transform as RectTransform;

            BuildSlots();

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(OnClickEndTurn);
            }
        }

        private void OnEnable()
        {
            if (battle != null) battle.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            if (battle != null) battle.StateChanged -= Refresh;
        }

        private void Start() => Refresh();

        private void BuildSlots()
        {
            if (_slots.Count > 0) return;

            for (int i = 0; i < maxHandSlots; i++)
            {
                var view = Instantiate(cardPrefab, handRoot);
                view.name = $"CardSlot_{i}";
                _slots.Add(view);
            }
        }

        private void Refresh()
        {
            if (battle == null) return;

            if (energyText != null) energyText.text = $"EN {battle.Energy}";
            if (playerHpText != null) playerHpText.text = $"P {battle.PlayerHP}/{battle.PlayerMaxHP} (B {battle.PlayerBlock})";
            if (enemyHpText != null) enemyHpText.text = $"E {battle.EnemyHP}/{battle.EnemyMaxHP}";

            bool allowInput = !_busy && battle.IsPlayerTurn && !battle.IsResolving;
            if (endTurnButton != null) endTurnButton.interactable = allowInput;

            for (int i = 0; i < _slots.Count; i++)
            {
                int idx = i;
                var card = battle.GetHandCard(idx);

                if (card == null)
                {
                    _slots[idx].gameObject.SetActive(false);
                    continue;
                }

                bool canPlay = allowInput && battle.Energy >= card.cost;

                _slots[idx].Bind(
                    card,
                    interactable: canPlay,
                    onClick: () => OnClickCard(idx),
                    showCost: true
                );
            }
        }

        private void OnClickEndTurn()
        {
            if (_busy) return;
            if (battle == null) return;
            if (!battle.IsPlayerTurn || battle.IsResolving) return;

            battle.EndTurn();
            Refresh(); // 즉시 UI 잠금 반영
        }

        private void OnClickCard(int idx)
        {
            if (_busy) return;
            StartCoroutine(PlayCardFlowCo(idx));
        }

        private IEnumerator PlayCardFlowCo(int idx)
        {
            if (battle == null) yield break;

            var card = battle.GetHandCard(idx);
            if (card == null) yield break;

            _busy = true;
            Refresh(); // ✅ busy true 상태 UI 반영

            try
            {
                var slot = _slots[idx];

                // 1) 클릭 피드백(살짝 흔들림)
                yield return SlotPunchCo(slot.transform);

                // 2) 플레이어/적 애니 (픽셀 Animator 트리거 중심)
                if (animDirector != null)
                    yield return animDirector.PlayPlayerCardCo(card);

                // 3) 카드가 discard로 날아가는 연출 (실제 적용 전에 “복제 카드”만 날림)
                if (discardAnchor != null && flyRoot != null)
                    yield return FlyCardToDiscardCo(card, slot);

                // 4) 실제 카드 적용 (StateChanged -> Refresh는 busy=true로 한번 돌 수 있음)
                battle.TryPlayCardAt(idx);
            }
            finally
            {
                _busy = false;
                Refresh(); // ✅ 핵심: busy 해제 후 다시 Refresh해서 interactable 복구
            }
        }

        private IEnumerator SlotPunchCo(Transform t)
        {
            if (t == null) yield break;

            t.DOKill(true);

            // 더 “화려하게”: 스케일 + 회전 펀치
            Sequence s = DOTween.Sequence();
            s.Join(t.DOPunchScale(Vector3.one * 0.10f, 0.12f, 10, 1f));
            s.Join(t.DOPunchRotation(new Vector3(0, 0, 6f), 0.12f, 10, 1f));
            yield return s.WaitForCompletion();
        }

        private IEnumerator FlyCardToDiscardCo(CardDefinition card, BattleCardButtonView source)
        {
            if (cardPrefab == null || flyRoot == null || discardAnchor == null || source == null) yield break;

            var fly = Instantiate(cardPrefab, flyRoot);
            fly.name = "FlyCardFx";
            fly.Bind(card, interactable: false, onClick: null, showCost: true);

            var rt = fly.transform as RectTransform;
            var srcRt = source.transform as RectTransform;

            if (rt == null || srcRt == null)
            {
                Destroy(fly.gameObject);
                yield break;
            }

            var cg = fly.GetComponent<CanvasGroup>();
            if (cg == null) cg = fly.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = false;

            rt.position = srcRt.position;
            rt.localScale = srcRt.localScale;
            fly.transform.SetAsLastSibling();

            rt.DOKill(true);
            cg.DOKill(true);

            Vector3 start = rt.position;
            Vector3 end = discardAnchor.position;

            // 곡선 중간점(위로 뜨고, 약간 옆으로 휘게)
            float side = Random.Range(-60f, 60f);
            Vector3 mid = (start + end) * 0.5f + new Vector3(side, 120f, 0f);

            float dur = discardFlyDuration;

            Sequence s = DOTween.Sequence();

            // ✅ 곡선 이동
            s.Join(rt.DOPath(new[] { start, mid, end }, dur, PathType.CatmullRom, PathMode.Ignore)
                .SetEase(Ease.InQuad));

            // ✅ 페이드 아웃
            s.Join(cg.DOFade(0f, dur).SetEase(Ease.InQuad));

            // ✅ 회전 + 스케일 다운
            s.Join(rt.DORotate(new Vector3(0, 0, Random.Range(-25f, -8f)), dur).SetEase(Ease.OutQuad));
            s.Join(rt.DOScale(srcRt.localScale * 0.85f, dur).SetEase(Ease.InQuad));

            yield return s.WaitForCompletion();

            Destroy(fly.gameObject);
        }

    }
}
