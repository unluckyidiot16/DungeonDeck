// Assets/_Project/Scripts/Core/GameBootstrapper.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Run;
using DungeonDeck.Config.Balance;
using DungeonDeck.Config.Oaths;
using DungeonDeck.Config.Map;

namespace DungeonDeck.Core
{
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Default Config (assign in Boot scene)")]
        public RunBalanceDefinition balance;
        public OathDefinition defaultOath;
        public MapPlanDefinition defaultMapPlan;

        [Header("Flow")]
        [Tooltip("MVP 디버그용: Boot 진입 시 자동으로 새 런 시작 후 Map으로 이동")]
        [SerializeField] private bool autoStartRunOnBoot = true;
        
        private void Awake()
        {
            // Ensure RunSession exists
            if (RunSession.I == null)
            {
                var go = new GameObject("RunSession");
                go.AddComponent<RunSession>();
            }
        }

        private void Start()
        {
            if (autoStartRunOnBoot)
                StartNewRunAndGoMap();
        }

        public void StartNewRunAndGoMap()
        {
            if (balance == null || defaultOath == null || defaultMapPlan == null)
            {
                Debug.LogError("[Boot] Missing balance/defaultOath/defaultMapPlan on GameBootstrapper.");
                return;
            }

            RunSession.I.StartNewRun(defaultOath, balance, defaultMapPlan);
            SceneManager.LoadScene(SceneRoutes.Map);
        }
    }
}