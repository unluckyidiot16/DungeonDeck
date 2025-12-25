using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.UI.Battle
{
    public class BattleCardButtonView : MonoBehaviour
    {
        [Header("Wiring")]
        public Button button;
        public TMP_Text nameText;
        public TMP_Text costText;

        private void Awake()
        {
            EnsureWired();
        }

        public void Bind(CardDefinition card, bool interactable, Action onClick)
        {
            EnsureWired();

            bool has = card != null;

            gameObject.SetActive(has);

            if (!has)
                return;

            if (nameText != null) nameText.text = card.id;
            if (costText != null) costText.text = $"COST {card.cost}";

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick.Invoke());
                button.interactable = interactable;
            }
        }

        private void EnsureWired()
        {
            if (button == null) button = GetComponent<Button>();

            // 프리팹에서 미리 연결하는 게 베스트지만,
            // 혹시 누락돼도 최소한 컴파일/동작은 하게 런타임 자동 생성
            if (button == null)
            {
                if (GetComponent<Image>() == null) gameObject.AddComponent<Image>();
                button = gameObject.AddComponent<Button>();
            }

            if (nameText == null) nameText = FindOrCreateTMP("Name");
            if (costText == null) costText = FindOrCreateTMP("Cost");
        }

        private TMP_Text FindOrCreateTMP(string childName)
        {
            var t = transform.Find(childName);
            if (t != null)
                return t.GetComponent<TMP_Text>() ?? t.gameObject.AddComponent<TextMeshProUGUI>();

            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;

            // 아주 기본 배치(프리팹 만들면 이 부분은 무시해도 됨)
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, childName == "Cost" ? 0 : 0.5f);
            rt.anchorMax = new Vector2(1, childName == "Cost" ? 0.5f : 1);
            rt.offsetMin = new Vector2(8, 6);
            rt.offsetMax = new Vector2(-8, -6);

            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = childName == "Cost" ? 20 : 28;

            return tmp;
        }
    }
}
