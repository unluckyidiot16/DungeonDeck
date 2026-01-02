// Assets/_Project/Scripts/UI/Cards/CardEffectLineView.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.UI.Cards
{
    public class CardEffectLineView : MonoBehaviour
    {
        [Header("Refs")]
        public CardIconPalette palette;
        public RectTransform root;

        [Tooltip("토큰 프리팹: CardEffectTokenView 포함")]
        public CardEffectTokenView tokenPrefab;

        [Tooltip("구분자 프리팹(+) - TMP_Text 하나만 있어도 됨. 없으면 공백으로 구분")]
        public TMP_Text plusPrefab;

        [Header("Options")]
        public bool showSelfTargetIcon = false; // Self는 숨기고 싶으면 false

        private readonly List<GameObject> _spawned = new();

        public void Bind(CardDefinition card)
        {
            Clear();

            if (card == null || root == null || tokenPrefab == null)
                return;

            int idx = 0;
            foreach (var e in card.EnumerateEffects())
            {
                if (e.value == 0) continue;

                var resolvedTarget = CardDefinition.ResolveTarget(e.target, e.kind);

                // Self 타겟 아이콘 숨김 옵션
                if (!showSelfTargetIcon && resolvedTarget == CardEffectTarget.Self)
                    resolvedTarget = 0; // None 처리(아이콘 null이 되게)

                if (idx > 0)
                    SpawnPlus();

                var token = Instantiate(tokenPrefab, root);
                token.gameObject.SetActive(true);
                token.Setup(palette, resolvedTarget, e.kind, e.value, Mathf.Max(1, e.repeat));
                _spawned.Add(token.gameObject);

                idx++;
            }
        }

        private void SpawnPlus()
        {
            if (plusPrefab == null) return;

            var plus = Instantiate(plusPrefab, root);
            plus.text = "+";
            plus.gameObject.SetActive(true);
            _spawned.Add(plus.gameObject);
        }

        private void Clear()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);

            _spawned.Clear();
        }
    }
}
