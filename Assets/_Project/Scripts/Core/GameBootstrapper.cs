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
            // Auto start for M1
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