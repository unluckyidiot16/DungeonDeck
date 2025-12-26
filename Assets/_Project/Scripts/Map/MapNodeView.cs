// Assets/_Project/Scripts/Map/MapNodeView.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Config.Map;
using TMPro;

namespace DungeonDeck.Map
{
    public class MapNodeView : MonoBehaviour
    {
        [Header("UI")]
        public Button button;
        public TMP_Text label; // (M1) replace with TMP later if you want

        public void Bind(int index, MapNodeType type, bool cleared, bool isCurrent, Action onClick)
        {
            if (label != null)
            {
                label.text = $"{index + 1}. {type}" + (cleared ? " ✓" : "");
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (!cleared)
                    button.onClick.AddListener(() => onClick?.Invoke());

                // M1: only allow clicking next node (isCurrent)
                button.interactable = isCurrent && !cleared;
            }
        }
    }
}