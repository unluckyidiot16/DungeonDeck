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

        [Header("Oath Buttons (미리 배치)")]
        [Tooltip("Inspector에서 미리 만들어둔 버튼들. 각 버튼에 OathDefinition을 1:1 매칭.")]
        public List<OathButton> oathButtons = new();

        [Header("Detail")]
        public TMP_Text detailText;

        [Header("Actions")]
        public Button confirmButton;
        public Button cancelButton;

        private Action<OathDefinition> _onConfirm;
        private OathDefinition _selected;

        private void Awake()
        {
            if (root == null) root = gameObject;
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);

            // 각 버튼에 클릭 이벤트 연결
            for (int i = 0; i < oathButtons.Count; i++)
            {
                var ob = oathButtons[i];
                if (ob == null || ob.button == null) continue;
                
                ob.button.onClick.AddListener(() => OnOathButtonClicked(ob));
            }

            Hide();
        }

        public void Show(Action<OathDefinition> onConfirm, OathDefinition preselect = null)
        {
            _onConfirm = onConfirm;
            root.SetActive(true);

            RefreshButtons();

            // 기본 선택
            if (preselect != null)
            {
                Select(preselect);
            }
            else
            {
                // 첫 번째 유효한 서약 선택
                for (int i = 0; i < oathButtons.Count; i++)
                {
                    if (oathButtons[i]?.oath != null)
                    {
                        Select(oathButtons[i].oath);
                        break;
                    }
                }
            }
        }

        // ✅ 기존 호출부 호환용 오버로드
        public void Show(OathDefinition[] oaths, Action<OathDefinition> onConfirm, OathDefinition preselect = null)
        {
            // oaths 파라미터는 무시 (이미 Inspector에서 설정됨)
            Show(onConfirm, preselect);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            _onConfirm = null;
            _selected = null;
        }

        private void OnOathButtonClicked(OathButton ob)
        {
            if (ob == null || ob.oath == null) return;
            
            Debug.Log($"[OathSelectPanel] Button clicked: {ob.oath.id}");
            Select(ob.oath);
        }

        private void Select(OathDefinition oath)
        {
            _selected = oath;
            
            Debug.Log($"[OathSelectPanel] Selected: {oath?.id}");

            if (confirmButton != null) 
                confirmButton.interactable = (oath != null);

            // 선택 상태 시각화
            RefreshButtonVisuals();

            // 상세 정보 표시
            if (detailText != null)
            {
                if (oath == null) 
                    detailText.text = "No oath selected.";
                else 
                    detailText.text = $"<b>{oath.displayName}</b>\n{oath.id}";
            }
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < oathButtons.Count; i++)
            {
                var ob = oathButtons[i];
                if (ob == null) continue;

                bool valid = ob.oath != null;
                if (ob.button != null) 
                    ob.button.gameObject.SetActive(valid);

                if (valid && ob.label != null)
                    ob.label.text = ob.oath.displayName;
            }
        }

        private void RefreshButtonVisuals()
        {
            for (int i = 0; i < oathButtons.Count; i++)
            {
                var ob = oathButtons[i];
                if (ob == null || ob.oath == null) continue;

                bool isSelected = (ob.oath == _selected);

                // 선택된 버튼 하이라이트
                if (ob.selectedMarker != null)
                    ob.selectedMarker.SetActive(isSelected);

                // 또는 버튼 색상 변경
                if (ob.button != null)
                {
                    var colors = ob.button.colors;
                    colors.normalColor = isSelected ? ob.selectedColor : ob.normalColor;
                    ob.button.colors = colors;
                }
            }
        }

        private void Confirm()
        {
            if (_selected == null) return;
            
            Debug.Log($"[OathSelectPanel] Confirm: {_selected.id}");
            
            var cb = _onConfirm;
            var selected = _selected;
            Hide();
            cb?.Invoke(selected);
        }
    }

    /// <summary>
    /// Inspector에서 설정하는 서약 버튼 정보
    /// </summary>
    [Serializable]
    public class OathButton
    {
        [Tooltip("이 버튼이 나타내는 서약")]
        public OathDefinition oath;
        
        [Tooltip("클릭할 버튼")]
        public Button button;
        
        [Tooltip("서약 이름 표시 (선택)")]
        public TMP_Text label;
        
        [Tooltip("선택됨 표시 오브젝트 (선택)")]
        public GameObject selectedMarker;

        [Header("Colors")]
        public Color normalColor = Color.white;
        public Color selectedColor = new Color(1f, 0.9f, 0.5f);
    }
}