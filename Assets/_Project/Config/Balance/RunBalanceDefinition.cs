// Assets/_Project/Config/Balance/RunBalanceDefinition.cs
using UnityEngine;

namespace DungeonDeck.Config.Balance
{
    [CreateAssetMenu(menuName = "DungeonDeck/Balance/Run Balance", fileName = "RunBalanceDefinition")]
    public class RunBalanceDefinition : ScriptableObject
    {
        public int startMaxHP = 60;
        public int startGold = 50;

        [Header("Battle")]
        public int startEnergyPerTurn = 3;
        public int startDrawPerTurn = 5;

        [Header("Rewards (M1 minimal)")]
        public int winGold = 20;
    }
}