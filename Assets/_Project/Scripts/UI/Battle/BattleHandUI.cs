// Assets/_Project/Scripts/UI/Battle/BattleHandUI.cs
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

        [Header("Fan Layout")]
        [Tooltip("FanHandLayout 컴포넌트. 비워두면 handRoot에서 자동 탐색/생성")]
        public FanHandLayout fanLayout;

        [Header("FX Anchors")]
        public RectTransform flyRoot;          // 카드가 날아다닐 레이어(캔버스 안). 비우면 자동 탐색
        public RectTransform discardAnchor;    // Discard 더미 위치(캔버스 안)
        public RectTransform drawAnchor;       // Draw 더미 위치(캔버스 안). 드로우 애니메이션 시작점
        public float discardFlyDuration = 0.28f;
        
        [Header("Draw Animation")]
        public float drawFlyDuration = 0.25f;
        public float drawStaggerDelay = 0.08f;  // 카드 간 딜레이

        public int maxHandSlots = 10;

        private readonly List<BattleCardButtonView> _slots = new();

        public DungeonDeck.Battle.View.BattleAnimDirector animDirector;

        private bool _busy = false;
        private Canvas _canvas;
        private Camera _uiCam;
        private int _lastHandCount = 0;

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

            // FanHandLayout 설정
            SetupFanLayout();

            BuildSlots();

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(OnClickEndTurn);
            }
        }

        private void SetupFanLayout()
        {
            // FanHandLayout이 없으면 handRoot에서 찾거나 생성
            if (fanLayout == null && handRoot != null)
            {
                fanLayout = handRoot.GetComponent<FanHandLayout>();
                if (fanLayout == null)
                {
                    fanLayout = handRoot.gameObject.AddComponent<FanHandLayout>();
                }
            }

            // 기존 GridLayoutGroup이 있으면 비활성화
            var gridLayout = handRoot?.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                gridLayout.enabled = false;
            }

            var horizLayout = handRoot?.GetComponent<HorizontalLayoutGroup>();
            if (horizLayout != null)
            {
                horizLayout.enabled = false;
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

        private void Start()
        {
            StartCoroutine(InitialDrawCo());
        }
        
        private IEnumerator InitialDrawCo()
        {
            yield return null;
            
            if (battle == null) yield break;
            
            int handCount = battle.HandCount;
            _lastHandCount = handCount;
            
            // 카드 슬롯 초기 상태 설정 (숨김)
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var card = battle.GetHandCard(i);
                
                if (card != null)
                {
                    slot.Bind(card, interactable: false, onClick: null, showCost: true);
                    SetupHoverHandler(slot, i);
                    
                    var cg = slot.GetComponent<CanvasGroup>();
                    if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    
                    var rt = slot.transform as RectTransform;
                    if (rt != null) rt.localScale = Vector3.one * 0.5f;
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }
            
            // 드로우 애니메이션 재생
            yield return PlayDrawAnimationCo(0, handCount);
            
            // 레이아웃 갱신
            UpdateFanLayout(true);
            
            // 최종 Refresh
            Refresh();
        }

        private void BuildSlots()
        {
            if (_slots.Count > 0) return;

            for (int i = 0; i < maxHandSlots; i++)
            {
                var view = Instantiate(cardPrefab, handRoot);
                view.name = $"CardSlot_{i}";
                view.gameObject.SetActive(false);
                
                // CardHoverLiftFx가 있으면 비활성화 (FanHandLayout이 호버 처리)
                var hoverLiftFx = view.GetComponent<CardHoverLiftFx>();
                if (hoverLiftFx != null)
                {
                    hoverLiftFx.enabled = false;
                }
                
                _slots.Add(view);
            }
        }

        private void SetupHoverHandler(BattleCardButtonView slot, int index)
        {
            var handler = slot.GetComponent<FanCardHoverHandler>();
            if (handler == null)
            {
                handler = slot.gameObject.AddComponent<FanCardHoverHandler>();
            }
            handler.layout = fanLayout;
            handler.cardIndex = index;
        }

        private void UpdateFanLayout(bool animate)
        {
            if (fanLayout == null) return;

            // 활성화된 카드 슬롯들의 RectTransform 수집
            List<RectTransform> activeCards = new();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].gameObject.activeSelf)
                {
                    activeCards.Add(_slots[i].transform as RectTransform);
                }
            }

            fanLayout.SetCards(activeCards, animate);
        }

        private void Refresh()
        {
            if (battle == null) return;

            if (energyText != null) energyText.text = $"EN {battle.Energy}";
            if (playerHpText != null) playerHpText.text = $"P {battle.PlayerHP}/{battle.PlayerMaxHP} (B {battle.PlayerBlock})";
            if (enemyHpText != null) enemyHpText.text = $"E {battle.EnemyHP}/{battle.EnemyMaxHP}";

            bool allowInput = !_busy && battle.IsPlayerTurn && !battle.IsResolving;
            if (endTurnButton != null) endTurnButton.interactable = allowInput;

            int currentHandCount = battle.HandCount;
            int newCards = currentHandCount - _lastHandCount;
            
            // 새로 드로우된 카드가 있으면 애니메이션 재생
            if (newCards > 0 && !_busy)
            {
                StartCoroutine(DrawNewCardsCo(_lastHandCount, newCards, allowInput));
                _lastHandCount = currentHandCount;
                return;
            }
            
            _lastHandCount = currentHandCount;

            // 기존 카드 업데이트
            int activeCount = 0;
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
                
                SetupHoverHandler(_slots[idx], activeCount);
                activeCount++;
            }

            // 레이아웃 갱신
            UpdateFanLayout(true);
        }
        
        private IEnumerator DrawNewCardsCo(int startIndex, int count, bool allowInput)
        {
            // 기존 카드들 먼저 업데이트
            for (int i = 0; i < startIndex && i < _slots.Count; i++)
            {
                int idx = i;
                var card = battle.GetHandCard(idx);
                if (card != null)
                {
                    bool canPlay = allowInput && battle.Energy >= card.cost;
                    _slots[idx].Bind(card, interactable: canPlay, onClick: () => OnClickCard(idx), showCost: true);
                }
            }
            
            // 새 카드들 준비 (숨김 상태로)
            for (int i = startIndex; i < startIndex + count && i < _slots.Count; i++)
            {
                int idx = i;
                var card = battle.GetHandCard(idx);
                if (card != null)
                {
                    _slots[idx].Bind(card, interactable: false, onClick: null, showCost: true);
                    SetupHoverHandler(_slots[idx], idx);
                    
                    var cg = _slots[idx].GetComponent<CanvasGroup>();
                    if (cg == null) cg = _slots[idx].gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    
                    var rt = _slots[idx].transform as RectTransform;
                    if (rt != null) rt.localScale = Vector3.one * 0.5f;
                }
            }
            
            // 레이아웃 갱신 (애니메이션 없이 - 드로우 애니메이션이 별도로 처리)
            UpdateFanLayout(false);
            
            // 드로우 애니메이션
            yield return PlayDrawAnimationCo(startIndex, count);
            
            // 최종 상태 적용
            for (int i = startIndex; i < startIndex + count && i < _slots.Count; i++)
            {
                int idx = i;
                var card = battle.GetHandCard(idx);
                if (card != null)
                {
                    bool canPlay = allowInput && battle.Energy >= card.cost;
                    _slots[idx].Bind(card, interactable: canPlay, onClick: () => OnClickCard(idx), showCost: true);
                }
            }
            
            // 레이아웃 다시 갱신 (최종 위치)
            UpdateFanLayout(true);
        }
        
        private IEnumerator PlayDrawAnimationCo(int startIndex, int count)
        {
            if (fanLayout == null) yield break;

            for (int i = startIndex; i < startIndex + count && i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.gameObject.activeSelf) continue;
                
                var rt = slot.transform as RectTransform;
                var cg = slot.GetComponent<CanvasGroup>();
                if (rt == null) continue;
                if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
                
                // FanLayout에서 목표 위치 가져오기
                int activeIndex = 0;
                for (int j = 0; j < i; j++)
                {
                    if (_slots[j].gameObject.activeSelf) activeIndex++;
                }
                
                Vector3 targetPos = fanLayout.GetBasePosition(activeIndex);
                Quaternion targetRot = fanLayout.GetBaseRotation(activeIndex);

                // 시작 위치 설정
                Vector3 startPos = targetPos;
                if (drawAnchor != null)
                {
                    startPos = rt.parent.InverseTransformPoint(drawAnchor.position);
                }
                else
                {
                    startPos = targetPos + new Vector3(0, -150f, 0);
                }
                
                rt.anchoredPosition = startPos;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one * 0.5f;
                cg.alpha = 0f;
                
                // 애니메이션
                rt.DOKill(true);
                cg.DOKill(true);
                
                Sequence seq = DOTween.Sequence();
                seq.Join(rt.DOAnchorPos(targetPos, drawFlyDuration).SetEase(Ease.OutBack));
                seq.Join(rt.DOLocalRotateQuaternion(targetRot, drawFlyDuration).SetEase(Ease.OutQuad));
                seq.Join(rt.DOScale(Vector3.one, drawFlyDuration).SetEase(Ease.OutBack));
                seq.Join(cg.DOFade(1f, drawFlyDuration * 0.7f).SetEase(Ease.OutQuad));
                
                // 스태거 딜레이
                yield return new WaitForSeconds(drawStaggerDelay);
            }
            
            // 마지막 애니메이션 완료 대기
            yield return new WaitForSeconds(Mathf.Max(0, drawFlyDuration - drawStaggerDelay));
        }

        private void OnClickEndTurn()
        {
            if (_busy) return;
            if (battle == null) return;
            if (!battle.IsPlayerTurn || battle.IsResolving) return;

            // 호버 해제
            fanLayout?.ClearHover();

            battle.EndTurn();
            Refresh();
        }

        private void OnClickCard(int idx)
        {
            if (_busy) return;
            
            // 호버 해제
            fanLayout?.ClearHover();
            
            StartCoroutine(PlayCardFlowCo(idx));
        }

        private IEnumerator PlayCardFlowCo(int idx)
        {
            if (battle == null) yield break;

            var card = battle.GetHandCard(idx);
            if (card == null) yield break;

            _busy = true;
            
            var slot = _slots[idx];
            var srcRt = slot.transform as RectTransform;
            
            // ✅ 1) 날아갈 복제본 먼저 생성 (원본 위치/회전/스케일 캡처)
            BattleCardButtonView flyCard = null;
            Vector3 srcPos = Vector3.zero;
            Quaternion srcRot = Quaternion.identity;
            Vector3 srcScale = Vector3.one;
            
            if (discardAnchor != null && flyRoot != null && srcRt != null)
            {
                srcPos = srcRt.position;
                srcRot = srcRt.localRotation;
                srcScale = srcRt.localScale;
                
                flyCard = Instantiate(cardPrefab, flyRoot);
                flyCard.name = "FlyCardFx";
                flyCard.Bind(card, interactable: false, onClick: null, showCost: true);
                
                var hoverLiftFx = flyCard.GetComponent<CardHoverLiftFx>();
                if (hoverLiftFx != null) hoverLiftFx.enabled = false;
                
                var flyRt = flyCard.transform as RectTransform;
                if (flyRt != null)
                {
                    flyRt.position = srcPos;
                    flyRt.localRotation = srcRot;
                    flyRt.localScale = srcScale;
                }
                
                var cg = flyCard.GetComponent<CanvasGroup>();
                if (cg == null) cg = flyCard.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.blocksRaycasts = false;
                
                flyCard.transform.SetAsLastSibling();
            }
            
            // ✅ 2) 원본 슬롯 즉시 숨김 (잔상 방지)
            slot.gameObject.SetActive(false);
            
            // ✅ 3) 레이아웃 즉시 갱신 (남은 카드들 재배치)
            UpdateFanLayout(true);

            try
            {
                // ✅ 4) 실제 카드 처리 시작
                bool started = battle.TryPlayCardAt(idx);
                if (!started)
                {
                    // 실패 시 복제본 제거
                    if (flyCard != null) Destroy(flyCard.gameObject);
                    yield break;
                }

                // ✅ 5) 복제본 날아가는 애니메이션
                if (flyCard != null)
                {
                    yield return FlyCardAnimationCo(flyCard, srcPos, srcScale);
                    Destroy(flyCard.gameObject);
                }

                // 카드 사용 완료 후 손패 수 동기화
                _lastHandCount = battle.HandCount;
            }
            finally
            {
                _busy = false;
                Refresh();
            }
        }
        
        /// <summary>
        /// 복제 카드 날아가는 애니메이션
        /// </summary>
        private IEnumerator FlyCardAnimationCo(BattleCardButtonView flyCard, Vector3 startPos, Vector3 startScale)
        {
            if (flyCard == null || discardAnchor == null) yield break;
            
            var rt = flyCard.transform as RectTransform;
            var cg = flyCard.GetComponent<CanvasGroup>();
            if (rt == null) yield break;
            if (cg == null) cg = flyCard.gameObject.AddComponent<CanvasGroup>();
            
            rt.DOKill(true);
            cg.DOKill(true);

            Vector3 end = discardAnchor.position;
            float side = Random.Range(-60f, 60f);
            Vector3 mid = (startPos + end) * 0.5f + new Vector3(side, 120f, 0f);

            float dur = discardFlyDuration;

            Sequence s = DOTween.Sequence();
            s.Join(rt.DOPath(new[] { startPos, mid, end }, dur, PathType.CatmullRom, PathMode.Ignore)
                .SetEase(Ease.InQuad));
            s.Join(cg.DOFade(0f, dur).SetEase(Ease.InQuad));
            s.Join(rt.DORotate(new Vector3(0, 0, Random.Range(-25f, -8f)), dur).SetEase(Ease.OutQuad));
            s.Join(rt.DOScale(startScale * 0.85f, dur).SetEase(Ease.InQuad));

            yield return s.WaitForCompletion();
        }
    }
}
