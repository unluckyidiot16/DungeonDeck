// Assets/_Project/Scripts/Battle/BattleActionQueue.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 카드 액션 버퍼링 및 순차 실행.
    /// 입력 큐잉으로 빠른 연타 지원.
    /// </summary>
    public class BattleActionQueue
    {
        public enum ActionKind { PlayCard, EndTurn }

        public struct QueuedAction
        {
            public ActionKind Kind;
            public CardDefinition Card;
            public int HandIndex;
            public int TargetIndex;
            public int Seq;
        }

        private readonly Queue<QueuedAction> _queue = new();
        private int _seq = 0;
        private bool _endTurnQueued = false;

        public event Func<QueuedAction, IEnumerator> OnExecuteCard;
        public event Func<IEnumerator> OnExecuteEndTurn;
        public event Action QueueChanged;

        // ─────────────────────────────────────────
        // Properties
        // ─────────────────────────────────────────
        public int PendingCount => _queue.Count;
        public bool HasQueuedEndTurn => _endTurnQueued;
        public bool IsEmpty => _queue.Count == 0;

        // ─────────────────────────────────────────
        // Enqueue
        // ─────────────────────────────────────────
        public bool EnqueueCard(CardDefinition card, int handIndex, int targetIndex, int availableEnergy)
        {
            if (card == null) return false;
            if (_endTurnQueued) return false;

            // 큐에 있는 카드들의 에너지 소비를 시뮬레이션
            int energyAfterQueue = SimulateEnergyAfterQueue(availableEnergy);
            if (energyAfterQueue < card.cost) return false;

            var action = new QueuedAction
            {
                Kind = ActionKind.PlayCard,
                Card = card,
                HandIndex = handIndex,
                TargetIndex = targetIndex,
                Seq = ++_seq
            };

            _queue.Enqueue(action);
            QueueChanged?.Invoke();
            return true;
        }

        public bool EnqueueEndTurn()
        {
            if (_endTurnQueued) return false;

            _endTurnQueued = true;
            _queue.Enqueue(new QueuedAction
            {
                Kind = ActionKind.EndTurn,
                Card = null,
                HandIndex = -1,
                TargetIndex = -1,
                Seq = ++_seq
            });

            QueueChanged?.Invoke();
            return true;
        }

        private int SimulateEnergyAfterQueue(int currentEnergy)
        {
            int energy = currentEnergy;

            foreach (var a in _queue)
            {
                if (a.Kind != ActionKind.PlayCard) continue;
                if (a.Card == null) continue;

                energy -= a.Card.cost;

                if (a.Card.effectKind == CardEffectKind.GainEnergy)
                    energy += Mathf.Max(0, a.Card.value);
            }

            return energy;
        }

        // ─────────────────────────────────────────
        // Process
        // ─────────────────────────────────────────
        public IEnumerator ProcessAllCo()
        {
            while (_queue.Count > 0)
            {
                var action = _queue.Dequeue();
                QueueChanged?.Invoke();

                if (action.Kind == ActionKind.EndTurn)
                {
                    _endTurnQueued = false;
                    Clear();

                    if (OnExecuteEndTurn != null)
                        yield return OnExecuteEndTurn.Invoke();

                    yield break;
                }

                if (OnExecuteCard != null)
                    yield return OnExecuteCard.Invoke(action);
            }
        }

        public bool TryDequeue(out QueuedAction action)
        {
            if (_queue.Count == 0)
            {
                action = default;
                return false;
            }

            action = _queue.Dequeue();

            if (action.Kind == ActionKind.EndTurn)
                _endTurnQueued = false;

            QueueChanged?.Invoke();
            return true;
        }

        // ─────────────────────────────────────────
        // Clear
        // ─────────────────────────────────────────
        public void Clear()
        {
            _queue.Clear();
            _endTurnQueued = false;
            QueueChanged?.Invoke();
        }
    }
}
