using UnityEngine;

namespace DungeonDeck.Run
{
    public class RunAbortButton : MonoBehaviour
    {
        // UI Button OnClick에 연결
        public void OnClickAbort()
        {
            if (RunSession.I != null)
                RunSession.I.EndRun(RunEndOutcome.Aborted);

            // Ensure save is cleared on abort
            RunSaveManager.ClearSave();
            PlayerPrefs.Save();
        }
    }
}