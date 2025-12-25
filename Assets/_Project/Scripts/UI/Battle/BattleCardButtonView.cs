using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Config.Cards;
using DungeonDeck.Config.UI;

namespace DungeonDeck.UI.Battle
{
    public class BattleCardButtonView : MonoBehaviour
    {
        [Header("Core")]
        public Button button;

        [Header("Visuals")]
        public Image backgroundImage;
        public Image frameImage;
        public Image iconImage;

        [Header("Texts (TMP)")]
        public TMP_Text nameText;
        public TMP_Text costText;
        public TMP_Text effectText;
        public TMP_Text rarityText;

        [Header("Theme (optional)")]
        public CardVisualTheme theme;

        private void Awake()
        {
            AutoWireIfNeeded();
        }

        public void Bind(CardDefinition card, bool interactable, Action onClick, bool showCost = true)
        {
            AutoWireIfNeeded();

            bool has = card != null;
            gameObject.SetActive(has);
            if (!has) return;

            if (nameText != null) nameText.text = card.GetDisplayName();
            if (effectText != null) effectText.text = card.GetEffectText();

            if (costText != null)
            {
                costText.gameObject.SetActive(showCost);
                costText.text = showCost ? card.cost.ToString() : "";
            }

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(card.icon != null);
                iconImage.sprite = card.icon;
            }

            if (rarityText != null)
                rarityText.text = card.rarity.ToString();

            ApplyTheme(card);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick.Invoke());
                button.interactable = interactable;
            }
        }

        private void ApplyTheme(CardDefinition card)
        {
            if (card == null) return;

            if (theme != null && theme.TryGet(card.rarity, out var style) && style != null)
            {
                if (backgroundImage != null) backgroundImage.color = style.backgroundTint;

                if (frameImage != null)
                {
                    frameImage.color = style.frameTint;
                    if (style.frameSprite != null) frameImage.sprite = style.frameSprite;
                }

                if (rarityText != null) rarityText.color = style.rarityTextTint;
                return;
            }

            // 테마가 없을 때도 최소한 구분은 나게
            // (색은 네가 theme로 정식 지정하는 게 정답)
            if (backgroundImage != null) backgroundImage.color = Color.white;
            if (frameImage != null) frameImage.color = Color.white;
        }

        private void AutoWireIfNeeded()
        {
            if (button == null) button = GetComponent<Button>();
            if (button == null)
            {
                if (GetComponent<Image>() == null) gameObject.AddComponent<Image>();
                button = gameObject.AddComponent<Button>();
            }

            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            // frame/icon/text는 프리팹에서 직접 연결하는 걸 권장 (없어도 null-safe)

            // TMP들 자동 탐색(프리팹 네이밍 추천: NameText/CostText/EffectText/RarityText)
            if (nameText == null) nameText = FindTmp("NameText");
            if (costText == null) costText = FindTmp("CostText");
            if (effectText == null) effectText = FindTmp("EffectText");
            if (rarityText == null) rarityText = FindTmp("RarityText");

            if (frameImage == null) frameImage = FindImg("Frame");
            if (iconImage == null) iconImage = FindImg("Icon");
        }

        private TMP_Text FindTmp(string childName)
        {
            var t = transform.Find(childName);
            return t ? t.GetComponent<TMP_Text>() : null;
        }

        private Image FindImg(string childName)
        {
            var t = transform.Find(childName);
            return t ? t.GetComponent<Image>() : null;
        }
    }
}
