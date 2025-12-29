// Assets/_Project/Scripts/Battle/View/PlayerAnimFSM.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;
using DungeonDeck.Debugging;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 플레이어 애니메이션 상태 머신.
    /// BattleAnimDirector에서 분리된 FSM 로직.
    /// </summary>
    public class PlayerAnimFSM
    {
        // ─────────────────────────────────────────
        // State
        // ─────────────────────────────────────────
        public enum State
        {
            Idle,
            Approaching,
            MeleeIdle,
            Attacking,
            Casting,
            Returning
        }

        public enum CommandKind
        {
            Attack,
            Cast,
            Return,
            ApproachOnly
        }

        public struct Command
        {
            public int Token;
            public CommandKind Kind;
            public CardDefinition Card;
            public int TargetIndex;
            public bool Forced;
            public int Frame;

            public Command(int token, CommandKind kind, CardDefinition card, int targetIndex, bool forced)
            {
                Token = token;
                Kind = kind;
                Card = card;
                TargetIndex = targetIndex;
                Forced = forced;
                Frame = Time.frameCount;
            }
        }

        // ─────────────────────────────────────────
        // Fields
        // ─────────────────────────────────────────
        private State _state = State.Idle;
        private bool _isMelee = false;
        private int _meleeTargetIndex = -1;

        private readonly Queue<Command> _cmdQueue = new();
        private readonly Dictionary<int, bool> _completedTokens = new();
        private int _cmdSeq = 0;

        // 중복 방지
        private int _lastEnqueueFrame = -999;
        private CommandKind _lastEnqueueKind;
        private int _lastEnqueueTarget = -999;
        private int _lastAttackFrame = -999;
        private int _lastAttackTarget = -999;
        private const int AttackDedupWindow = 2;

        // ─────────────────────────────────────────
        // Properties
        // ─────────────────────────────────────────
        public State CurrentState => _state;
        public bool IsMelee => _isMelee;
        public int MeleeTargetIndex => _meleeTargetIndex;
        public bool HasPendingCommands => _cmdQueue.Count > 0;

        // ─────────────────────────────────────────
        // State Management
        // ─────────────────────────────────────────
        public void SetState(State newState) => _state = newState;

        public void SetMeleeState(bool melee, int targetIndex)
        {
            _isMelee = melee;
            _meleeTargetIndex = melee ? targetIndex : -1;
        }

        public void Reset()
        {
            _state = State.Idle;
            _isMelee = false;
            _meleeTargetIndex = -1;
            _cmdQueue.Clear();
            _completedTokens.Clear();
        }

        // ─────────────────────────────────────────
        // Command Queue
        // ─────────────────────────────────────────
        public int Enqueue(CommandKind kind, CardDefinition card, int targetIndex, bool forced = false)
        {
            int token = ++_cmdSeq;
            int frame = Time.frameCount;

            // Attack 중복 방지 (연속 프레임)
            if (kind == CommandKind.Attack)
            {
                if ((frame - _lastAttackFrame) <= AttackDedupWindow && targetIndex == _lastAttackTarget)
                {
                    _completedTokens[token] = true;
                    return token;
                }

                _lastAttackFrame = frame;
                _lastAttackTarget = targetIndex;
            }

            // 일반 중복 방지
            if (frame == _lastEnqueueFrame && kind == _lastEnqueueKind && targetIndex == _lastEnqueueTarget)
            {
                _completedTokens[token] = true;
                return token;
            }

            _lastEnqueueFrame = frame;
            _lastEnqueueKind = kind;
            _lastEnqueueTarget = targetIndex;

            _completedTokens[token] = false;
            _cmdQueue.Enqueue(new Command(token, kind, card, targetIndex, forced));

            return token;
        }

        public bool TryDequeue(out Command cmd)
        {
            if (_cmdQueue.Count == 0)
            {
                cmd = default;
                return false;
            }

            cmd = _cmdQueue.Dequeue();
            return true;
        }

        public void MarkCompleted(int token)
        {
            _completedTokens[token] = true;
        }

        public bool IsCompleted(int token)
        {
            return _completedTokens.TryGetValue(token, out bool done) && done;
        }

        public void ClearCompletedToken(int token)
        {
            _completedTokens.Remove(token);
        }

        public IEnumerator WaitForToken(int token)
        {
            while (!IsCompleted(token))
                yield return null;

            ClearCompletedToken(token);
        }

        public void ClearQueue()
        {
            _cmdQueue.Clear();
        }
    }
}
