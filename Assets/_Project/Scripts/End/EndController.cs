using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;
using DungeonDeck.Run;

namespace DungeonDeck.Ending
{
    public class EndController : MonoBehaviour
    {
        [SerializeField] private EndView view;

        private bool _didWire = false;

        private void Awake()
        {
            if (view == null) view = FindObjectOfType<EndView>(true);
        }

        private void Start()
        {
            // Double safety: clear persisted save when reaching End
            RunSaveManager.ClearSave();
            PlayerPrefs.Save();

            WireOnce();
            Refresh();
        }


        private void WireOnce()
        {
            if (_didWire) return;
            _didWire = true;

            if (view == null) return;

            if (view.restartButton != null)
            {
                view.restartButton.onClick.RemoveAllListeners();
                view.restartButton.onClick.AddListener(RestartRun);
            }

            if (view.mainButton != null)
            {
                view.mainButton.onClick.RemoveAllListeners();
                view.mainButton.onClick.AddListener(GoMain);
            }
        }

        private void Refresh()
        {
            if (view == null || view.resultText == null) return;

            var run = RunSession.I;
            var outcome = (run != null && run.State != null) ? run.State.lastOutcome : RunEndOutcome.None;

            switch (outcome)
            {
                case RunEndOutcome.Victory:
                    view.resultText.text = "VICTORY";
                    break;
                case RunEndOutcome.Defeat:
                    view.resultText.text = "DEFEAT";
                    break;
                case RunEndOutcome.Aborted:
                    view.resultText.text = "ABORTED";
                    break;
                default:
                    view.resultText.text = "END";
                    break;
            }
        }


        private void RestartRun()
        {
            // 새 런 생성 (RunFactory가 존재한다고 가정)
            // 프로젝트에 따라 RunFactory API 이름이 다를 수 있으니,
            // 아래 2줄 중 하나만 맞춰서 사용하면 됨.

            // 1) RunFactory에 CreateNewRun 같은 함수가 있으면 사용:
            // RunFactory.CreateNewRun();

            // ✅ End에서 Restart: 즉시 재시작(부트 스킵) 정책으로 통일
            if (RunSession.I != null)
            {
                RunSession.I.RestartSameRunAndGoToMap();
                return;
            }
            
            Debug.LogWarning("[End] RunSession missing. Going Boot.");
            SceneManager.LoadScene(SceneRoutes.Boot);
        }

        private void GoMain()
        {
            SceneManager.LoadScene(SceneRoutes.Boot);
        }
    }
}