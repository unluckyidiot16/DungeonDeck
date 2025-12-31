using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DungeonDeck.Battle.View
{ 
    /// <summary>
    /// 적 프리팹 내부에 붙는 HUD 뷰.
    /// BattleEnemy의 OnStatsChanged 이벤트를 구독해
    /// HP/Block 등의 UI를 자동 갱신합니다.
    /// </summary>
    [RequireComponent(typeof(BattleEnemy))]
    public class EnemyHUDView : MonoBehaviour
    {
        [Header("HP")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMP_Text hpText;

        [Header("Block (optional)")]
        [SerializeField] private Slider blockSlider;
        [SerializeField] private TMP_Text blockText;

        [Header("Dead (optional)")]
        [SerializeField] private GameObject deadMarker;

        private BattleEnemy _enemy;
        private bool _bound;

        private void Awake()
        {
            _enemy = GetComponent<BattleEnemy>();
        }

        private void OnEnable()
        {
            Bind();
            RefreshFromEnemy(_enemy);
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (_bound) return;
            if (_enemy == null) return;

            // ✅ BattleEnemy.OnStatsChanged -> EnemyHUDView.RefreshFromEnemy(enemy)
            // (주의) Unity 메시지 함수 Update()는 파라미터를 받을 수 없으므로 이름을 피한다.
            _enemy.OnStatsChanged += RefreshFromEnemy;
            _enemy.OnDefeated += RefreshFromEnemy; // 사망 시점에도 최종 상태 반영

            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound) return;

            if (_enemy != null)
            {
                _enemy.OnStatsChanged -= RefreshFromEnemy;
                _enemy.OnDefeated -= RefreshFromEnemy;
            }

            _bound = false;
        }

        /// <summary>
        /// BattleEnemy 상태 기반 UI 갱신.
        /// 이벤트 핸들러로 직접 연결됩니다.
        /// </summary>
        public void RefreshFromEnemy(BattleEnemy enemy)
        {
            if (enemy == null) return;

            // HP
            if (hpSlider != null)
            {
                hpSlider.maxValue = Mathf.Max(1, enemy.MaxHP);
                hpSlider.value = Mathf.Clamp(enemy.HP, 0, enemy.MaxHP);
            }

            if (hpText != null)
                hpText.text = $"{enemy.HP}/{enemy.MaxHP}";

            // Block (optional)
            int block = enemy.Block;

            if (blockSlider != null)
            {
                blockSlider.gameObject.SetActive(block > 0);
                blockSlider.maxValue = Mathf.Max(1, enemy.MaxHP);
                blockSlider.value = Mathf.Clamp(block, 0, enemy.MaxHP);
            }

            if (blockText != null)
                blockText.text = block > 0 ? block.ToString() : string.Empty;

            // Dead marker (optional)
            if (deadMarker != null)
                deadMarker.SetActive(!enemy.IsAlive);
        }
    }
}
