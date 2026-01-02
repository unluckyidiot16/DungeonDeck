// Assets/_Project/Scripts/UI/Cards/CardEffectTokenView.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.UI.Cards
{
    public class CardEffectTokenView : MonoBehaviour
    {
        [Header("Refs")]
        public Image targetIcon;
        public Image effectIcon;
        public TMP_Text valueText;
        public TMP_Text repeatText;   // "x2" 같은 것 (없어도 됨)
        public TMP_Text labelText;    // 취약 같은 라벨 (옵션)

        public void Setup(CardIconPalette palette, CardEffectTarget target, CardEffectKind kind, int value, int repeat)
        {
            if (targetIcon != null)
            {
                targetIcon.sprite = palette != null ? palette.GetTarget(target) : null;
                targetIcon.gameObject.SetActive(targetIcon.sprite != null);
            }

            if (effectIcon != null)
            {
                effectIcon.sprite = palette != null ? palette.GetEffect(kind) : null;
                effectIcon.gameObject.SetActive(effectIcon.sprite != null);
            }

            if (valueText != null)
                valueText.text = value.ToString();

            if (repeatText != null)
            {
                bool show = repeat > 1;
                repeatText.gameObject.SetActive(show);
                if (show) repeatText.text = $"x{repeat}";
            }

            // 상태계열은 라벨이 있으면 더 직관적 (옵션)
            if (labelText != null)
            {
                if (kind == CardEffectKind.ApplyVulnerable)
                {
                    labelText.gameObject.SetActive(true);
                    labelText.text = "취약";
                }
                else
                {
                    labelText.gameObject.SetActive(false);
                }
            }
        }
    }
}