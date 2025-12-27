using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DungeonDeck.Core;
using DungeonDeck.Run;
using DungeonDeck.Config.Balance;
using DungeonDeck.Config.Oaths;
using DungeonDeck.Config.Map;

namespace DungeonDeck.Title
{
    public class TitleController : MonoBehaviour
    {
        [Header("Config (assign in Title scene)")]
        public RunBalanceDefinition balance;
        public OathDefinition defaultOath;
        public MapPlanDefinition defaultMapPlan;

        [Header("UI")]
        public Button continueButton;
        public Button newGameButton;
        public TMP_Text infoText;

        private void Awake()
        {
            if (RunSession.I == null)
            {
                var go = new GameObject("RunSession");
                go.AddComponent<RunSession>();
            }
            if (RunSaveManager.I == null)
            {
                var go = new GameObject("RunSaveManager");
                go.AddComponent<RunSaveManager>();
            }

            if (continueButton != null) continueButton.onClick.AddListener(OnClickContinue);
            if (newGameButton != null) newGameButton.onClick.AddListener(OnClickNewGame);
        }

        private void Start() => Refresh();

        private void Refresh()
        {
            bool has = RunSaveManager.HasSave();

            if (continueButton != null)
                continueButton.interactable = has;

            if (infoText != null)
            {
                if (!has) infoText.text = "NO SAVE";
                else if (RunSaveManager.I.TryPeekSummary(out var sum))
                {
                    infoText.text =
                        $"Oath: {sum.oathId}\n" +
                        $"HP: {sum.hp}/{sum.maxHp}   Gold: {sum.gold}\n" +
                        $"Node: {sum.nodeIndex + 1}   SavedScene: {sum.sceneName}\n" +
                        $"Saved(UTC): {sum.savedAtUtc}";
                }
                else infoText.text = "SAVE EXISTS (but unreadable)";
            }
        }

        private void OnClickContinue()
        {
            bool ok = RunSaveManager.I.TryContinueFromSave(defaultOath, balance, defaultMapPlan);
            if (!ok) Refresh();
        }

        private void OnClickNewGame()
        {
            if (balance == null || defaultOath == null || defaultMapPlan == null)
            {
                Debug.LogError("[Title] Missing balance/defaultOath/defaultMapPlan.");
                return;
            }

            // ✅ NewGame = 초기화 + 새 런
            RunSaveManager.ClearSave();
            PlayerPrefs.Save();

            RunSession.I.StartNewRun(defaultOath, balance, defaultMapPlan);
            SceneManager.LoadScene(SceneRoutes.Map);
        }
    }
}
