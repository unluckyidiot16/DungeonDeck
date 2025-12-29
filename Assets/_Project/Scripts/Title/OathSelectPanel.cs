using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DungeonDeck.Config.Oaths;

namespace DungeonDeck.Title
{
    public class OathSelectPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("List")]
        public Transform listRoot;
        public Button oathButtonPrefab;

        [Header("Detail")]
        public TMP_Text detailText;

        [Header("Actions")]
        public Button confirmButton;
        public Button cancelButton;

        private readonly List<Button> _spawned = new();
        private Action<OathDefinition> _onConfirm;
        private OathDefinition _selected;

        private void Awake()
        {
            if (root == null) root = gameObject;
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);

            Hide();
        }

        public void Show(OathDefinition[] oaths, Action<OathDefinition> onConfirm, OathDefinition preselect = null)
        {
            _onConfirm = onConfirm;
            root.SetActive(true);

            Rebuild(oaths);

            if (preselect != null) Select(preselect);
            else if (oaths != null && oaths.Length > 0) Select(oaths[0]);
            else Select(null);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            _onConfirm = null;
            _selected = null;
        }

        private void Confirm()
        {
            if (_selected == null) return;
            var cb = _onConfirm;
            Hide();
            cb?.Invoke(_selected);
        }

        private void Rebuild(OathDefinition[] oaths)
        {
            // clear old
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();

            if (confirmButton != null) confirmButton.interactable = false;

            if (oaths == null || oaths.Length == 0) return;
            if (listRoot == null || oathButtonPrefab == null) return;

            for (int i = 0; i < oaths.Length; i++)
            {
                var oath = oaths[i];
                if (oath == null) continue;

                var btn = Instantiate(oathButtonPrefab, listRoot);
                _spawned.Add(btn);

                var label = btn.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = GetOathLabel(oath);

                btn.onClick.AddListener(() => Select(oath));
            }
        }

        private void Select(OathDefinition oath)
        {
            _selected = oath;

            if (confirmButton != null) confirmButton.interactable = (oath != null);

            if (detailText != null)
            {
                if (oath == null) detailText.text = "No oath selected.";
                else detailText.text = $"{GetOathLabel(oath)}\n{oath.name}";
            }
        }

        private static string GetOathLabel(OathDefinition oath)
        {
            // 안전하게: OathDefinition 필드 구조를 몰라도 SO name은 무조건 존재
            if (oath == null) return "NULL";
            return oath.name;
        }
    }
}
