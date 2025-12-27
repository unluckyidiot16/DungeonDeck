using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Battle;

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

        public int maxHandSlots = 5;

        private readonly List<BattleCardButtonView> _slots = new();
        
        public DungeonDeck.Battle.View.BattleAnimDirector animDirector;
        private bool _busy = false;


        private void Awake()
        {
            if (battle == null) battle = FindObjectOfType<BattleController>();
            if (handRoot == null) handRoot = transform;

            if (animDirector == null)
                animDirector = FindObjectOfType<DungeonDeck.Battle.View.BattleAnimDirector>(true);
            
            BuildSlots();

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(() => battle?.EndTurn());
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
            Refresh();
        }

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

            for (int i = 0; i < _slots.Count; i++)
            {
                int idx = i;
                var card = battle.GetHandCard(idx);

                if (card == null)
                {
                    _slots[idx].gameObject.SetActive(false);
                    continue;
                }

                bool canPlay = battle.Energy >= card.cost;

                _slots[idx].Bind(
                    card,
                    interactable: canPlay && !_busy,
                    onClick: () => OnClickCard(idx),
                    showCost: true
                );
            }
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
            if (endTurnButton != null) endTurnButton.interactable = false;

            // 카드 UI “살짝 눌림” (아주 미니멀)
            var slot = _slots[idx];
            yield return PunchScaleCo(slot.transform, 1.08f, 0.08f);

            // 캐릭터 애니
            if (animDirector != null)
                yield return animDirector.PlayPlayerCardCo(card);

            // 실제 카드 적용
            battle.TryPlayCardAt(idx);

            _busy = false;
            if (endTurnButton != null) endTurnButton.interactable = true;
        }

        private IEnumerator PunchScaleCo(Transform t, float up, float dur)
        {
            if (t == null) yield break;

            Vector3 baseScale = t.localScale;
            Vector3 upScale = baseScale * up;

            float half = Mathf.Max(0.01f, dur * 0.5f);
            float tt = 0f;

            while (tt < half)
            {
                tt += Time.unscaledDeltaTime;
                t.localScale = Vector3.Lerp(baseScale, upScale, Mathf.Clamp01(tt / half));
                yield return null;
            }

            tt = 0f;
            while (tt < half)
            {
                tt += Time.unscaledDeltaTime;
                t.localScale = Vector3.Lerp(upScale, baseScale, Mathf.Clamp01(tt / half));
                yield return null;
            }

            t.localScale = baseScale;
        }

        
    }
}
