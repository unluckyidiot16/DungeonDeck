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
        
        [Tooltip("New Game에서 고를 수 있는 서약 목록(비워두면 defaultOath로 바로 시작).")]
        public OathDefinition[] newGameOaths;


        [Header("UI")]
        public Button continueButton;
        public Button newGameButton;
        public TMP_Text infoText;
        
        [Header("Oath Select UI")]
        public OathSelectPanel oathSelectPanel;

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

            // ✅ 디버그: 현재 설정 상태 확인
            Debug.Log($"[Title] OnClickNewGame - oathSelectPanel: {(oathSelectPanel != null ? "OK" : "NULL")}, " +
                      $"newGameOaths: {(newGameOaths != null ? newGameOaths.Length.ToString() : "NULL")}");

            // ✅ 1) 패널이 있으면 서약 선택부터
            if (oathSelectPanel != null && newGameOaths != null && newGameOaths.Length > 0)
            {
                Debug.Log("[Title] Showing OathSelectPanel...");
                oathSelectPanel.gameObject.SetActive(true);
                oathSelectPanel.Show(newGameOaths, BeginNewGameWithOath, defaultOath);
                return;
            }
            
            // ✅ 2) 없으면 기존처럼 default로 시작
            Debug.Log($"[Title] No oath selection UI. Using defaultOath: {defaultOath?.id}");
            BeginNewGameWithOath(defaultOath);
        }
        
        private void BeginNewGameWithOath(OathDefinition oath)
        {
            if (oath == null) oath = defaultOath;
            
            // ✅ 디버그: 선택된 서약 확인
            Debug.Log($"[Title] BeginNewGameWithOath - Selected oath: \"{oath?.id}\" ({oath?.name})");
            
            // ✅ NewGame = 초기화 + 새 런
            RunSaveManager.ClearSave();
            PlayerPrefs.Save();
            
            RunSession.I.StartNewRun(oath, balance, defaultMapPlan);
            
            // ✅ 디버그: RunSession에 저장된 값 확인
            Debug.Log($"[Title] After StartNewRun - RunSession.State.oathId: \"{RunSession.I?.State?.oathId}\"");
            
            SceneManager.LoadScene(SceneRoutes.Map);
        }
    }
}
