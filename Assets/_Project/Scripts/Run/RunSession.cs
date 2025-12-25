// Assets/_Project/Scripts/Run/RunSession.cs
using UnityEngine;
using DungeonDeck.Config.Balance;
using DungeonDeck.Config.Map;
using DungeonDeck.Config.Oaths;

namespace DungeonDeck.Run
{
    public class RunSession : MonoBehaviour
    {
        public static RunSession I { get; private set; }

        public RunState State { get; private set; }
        public MapPlanDefinition Plan { get; private set; }
        public RunBalanceDefinition Balance { get; private set; }
        public OathDefinition Oath { get; private set; }

        // Battle context (minimal)
        public MapNodeType PendingBattleType { get; private set; } = MapNodeType.Battle;

        private void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartNewRun(OathDefinition oath, RunBalanceDefinition balance, MapPlanDefinition plan)
        {
            Oath = oath;
            Balance = balance;
            Plan = plan;
            State = RunFactory.CreateNewRun(oath, balance);
        }

        public bool IsNodeCleared(int index) => State != null && index < State.nodeIndex;

        public MapNodeType GetNodeType(int index)
        {
            if (Plan == null || Plan.nodes == null || Plan.nodes.Count == 0)
                return MapNodeType.Battle;

            index = Mathf.Clamp(index, 0, Plan.nodes.Count - 1);
            return Plan.nodes[index];
        }

        public void EnterNode(int index)
        {
            if (State == null) return;

            // Lock index (M1: linear)
            index = Mathf.Clamp(index, 0, Plan.nodes.Count - 1);

            var type = GetNodeType(index);
            PendingBattleType = type;

            // We mark "current node" by index; clear happens when finished
            // For M1: Battle scene handles win/lose then advances or stays
            State.nodeIndex = index;

            // Scene load is handled by MapController (keeps RunSession simple)
        }

        public void MarkNodeClearedAndAdvance()
        {
            if (State == null || Plan == null) return;

            // cleared nodeIndex => next index
            State.nodeIndex += 1;
            if (State.nodeIndex > Plan.nodes.Count) State.nodeIndex = Plan.nodes.Count;
        }

        public bool IsRunFinished()
        {
            if (Plan == null || State == null) return false;
            return State.nodeIndex >= Plan.nodes.Count;
        }
    }
}
