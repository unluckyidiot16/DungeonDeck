using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;
using DungeonDeck.Run;

namespace DungeonDeck.Ending
{
    public class EndRestartButtons : MonoBehaviour
    {
        // End 씬 Restart 버튼 OnClick에 연결
        public void OnClickRestartSameOath()
        {
            if (RunSession.I != null)
            {
                RunSession.I.RestartSameRunAndGoToMap();
                return;
            }
        
            Debug.LogWarning("[End] RunSession missing. Going Boot.");
            SceneManager.LoadScene(SceneRoutes.Boot);
        }
    
        // End 씬 Main 버튼 OnClick에 연결 (프로젝트 정책대로 Boot/Title로)
        public void OnClickMain()
        {
            SceneManager.LoadScene(SceneRoutes.Boot);
        }
    }
}