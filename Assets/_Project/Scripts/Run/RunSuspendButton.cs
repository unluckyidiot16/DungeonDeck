using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;

namespace DungeonDeck.Run
{
    public class RunSuspendButton : MonoBehaviour
    {
        // UI Button OnClick에 연결
        public void OnClickSuspend()
        {
            if (RunSaveManager.I != null)
                RunSaveManager.I.SaveCurrentRun();

            // Boot로 가도 되지만, Title이 있는 지금은 Title이 UX상 더 자연스러움.
            SceneManager.LoadScene(SceneRoutes.Title);
        }
    }
}