using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonDeck.Battle.Combat;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 플레이어 HUD 뷰(최소 스켈레톤)
    /// BattlePlayer.OnStatsChanged -> HUD 갱신
    /// </summary>
    [RequireComponent(typeof(DungeonDeck.Battle.BattlePlayer))]
    public class PlayerHUDView : MonoBehaviour
    {
        [Header("HP")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMP_Text hpText;

        [Header("Block (optional)")]
        [SerializeField] private Slider blockSlider;
        [SerializeField] private TMP_Text blockText;

        private DungeonDeck.Battle.BattlePlayer _player;
        private bool _bound;

        private void Awake()
        {
            _player = GetComponent<DungeonDeck.Battle.BattlePlayer>();
        } 
        private void OnEnable()
        {
            Bind();
            RefreshFromCombatant(_player);
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (_bound) return;
            if (_player == null) return;
            _player.OnStatsChanged += RefreshFromCombatant;
            _player.OnDefeated += RefreshFromCombatant;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound) return;
            if (_player != null)
            {
                _player.OnStatsChanged -= RefreshFromCombatant;
                _player.OnDefeated -= RefreshFromCombatant;
            }
            _bound = false;
        }

        public void RefreshFromCombatant(ICombatant c)
        {
            if (c == null) return;

            if (hpSlider != null)
            {
                hpSlider.maxValue = Mathf.Max(1, c.MaxHP);
                hpSlider.value = Mathf.Clamp(c.HP, 0, c.MaxHP);
            }
            if (hpText != null)
                hpText.text = $"{c.HP}/{c.MaxHP}";

            int block = c.Block;
            if (blockSlider != null)
            {
                blockSlider.gameObject.SetActive(block > 0);
                blockSlider.maxValue = Mathf.Max(1, c.MaxHP);
                blockSlider.value = Mathf.Clamp(block, 0, c.MaxHP);
            }
            if (blockText != null)
                blockText.text = block > 0 ? block.ToString() : string.Empty;
        }
    }
}