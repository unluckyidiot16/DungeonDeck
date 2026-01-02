using System;
using DG.Tweening;
// using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Config.Cards;
using DungeonDeck.Config.UI;
using DungeonDeck.UI.Cards;

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
        
        [Header("Effect Line (Icon Tokens)")]
        [Tooltip("연결되면 effectText 대신 아이콘 토큰 라인을 사용합니다. (없으면 기존 텍스트 fallback)")]
        public CardEffectLineView effectLineView;

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
            
            // ✅ 활성화 전에 DOTween 정리 및 transform 초기화
            if (has && !gameObject.activeSelf)
            {
                // 비활성 상태에서 활성화될 때 transform 초기화
                transform.DOKill(true);
                transform.localScale = Vector3.one;
                transform.localRotation = Quaternion.identity;
            }
            
            gameObject.SetActive(has);
            if (!has) return;
            
            // ✅ CardHoverLiftFx가 있으면 기준값 리셋
            var hoverFx = GetComponent<CardHoverLiftFx>();
            if (hoverFx != null)
            {
                hoverFx.ResetBaseTransform();
            }

            if (nameText != null) nameText.text = card.GetDisplayName();
            // ✅ 1순위: 아이콘 토큰 라인 / 2순위: 기존 TMP 텍스트 라인
            if (effectLineView != null)
            {
                effectLineView.Bind(card);
                if (effectText != null) effectText.gameObject.SetActive(false);
            }
            else
            {
                if (effectText != null)
                {
                    effectText.gameObject.SetActive(true);
                    effectText.text = card.GetCompactEffectLine(); // 기존 유지(디버그/임시)
                }
            }

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
            
            if (nameText == null) nameText = FindTmp("NameText");
            if (costText == null) costText = FindTmp("CostText");
            if (effectText == null) effectText = FindTmp("EffectText");
            if (rarityText == null) rarityText = FindTmp("RarityText");

            if (frameImage == null) frameImage = FindImg("Frame");
            if (iconImage == null) iconImage = FindImg("Icon");
            
            // ✅ 아이콘 라인 자동 탐색(프리팹에 붙여두면 자동 연결)
            if (effectLineView == null)
                effectLineView = GetComponentInChildren<CardEffectLineView>(true);
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