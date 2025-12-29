// Assets/_Project/Scripts/Battle/DeckRuntime.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Config.Cards;
using Random = UnityEngine.Random;

namespace DungeonDeck.Battle
{
    /// <summary>
    /// 전투 중 덱 관리.
    /// 드로우 파일, 버림 파일, 손패, 소멸 파일.
    /// </summary>
    public class DeckRuntime
    {
        private readonly List<CardDefinition> _draw = new();
        private readonly List<CardDefinition> _discard = new();
        private readonly List<CardDefinition> _hand = new();
        private readonly List<CardDefinition> _exhaust = new();

        public event Action DeckChanged;

        // ─────────────────────────────────────────
        // Properties
        // ─────────────────────────────────────────
        public int HandCount => _hand.Count;
        public int DrawCount => _draw.Count;
        public int DiscardCount => _discard.Count;
        public int ExhaustCount => _exhaust.Count;

        public IReadOnlyList<CardDefinition> Hand => _hand;
        public IReadOnlyList<CardDefinition> DrawPile => _draw;
        public IReadOnlyList<CardDefinition> DiscardPile => _discard;
        public IReadOnlyList<CardDefinition> ExhaustPile => _exhaust;

        // ─────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────
        public DeckRuntime(List<CardDefinition> sourceDeck)
        {
            if (sourceDeck != null)
                _draw.AddRange(sourceDeck);

            Shuffle(_draw);
        }

        // ─────────────────────────────────────────
        // Draw
        // ─────────────────────────────────────────
        public void Draw(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_draw.Count == 0)
                    Reshuffle();

                if (_draw.Count == 0)
                    break;

                var card = _draw[0];
                _draw.RemoveAt(0);
                _hand.Add(card);
            }

            DeckChanged?.Invoke();
        }

        // ─────────────────────────────────────────
        // Hand Access
        // ─────────────────────────────────────────
        public CardDefinition PeekHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return null;
            return _hand[index];
        }

        public int FindHandIndex(CardDefinition card)
        {
            if (card == null) return -1;
            for (int i = 0; i < _hand.Count; i++)
            {
                if (_hand[i] == card) return i;
            }
            return -1;
        }

        public bool HasCardInHand(CardDefinition card)
        {
            return FindHandIndex(card) >= 0;
        }

        // ─────────────────────────────────────────
        // Play / Discard / Exhaust
        // ─────────────────────────────────────────
        public bool PlayFromHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return false;

            var card = _hand[index];
            _hand.RemoveAt(index);
            _discard.Add(card);

            DeckChanged?.Invoke();
            return true;
        }

        public bool ExhaustFromHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return false;

            var card = _hand[index];
            _hand.RemoveAt(index);
            _exhaust.Add(card);

            DeckChanged?.Invoke();
            return true;
        }

        public void DiscardHand()
        {
            if (_hand.Count == 0) return;

            _discard.AddRange(_hand);
            _hand.Clear();

            DeckChanged?.Invoke();
        }

        public bool DiscardFromHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return false;

            var card = _hand[index];
            _hand.RemoveAt(index);
            _discard.Add(card);

            DeckChanged?.Invoke();
            return true;
        }

        // ─────────────────────────────────────────
        // Shuffle / Reshuffle
        // ─────────────────────────────────────────
        private void Reshuffle()
        {
            if (_discard.Count == 0) return;

            _draw.AddRange(_discard);
            _discard.Clear();
            Shuffle(_draw);

            DeckChanged?.Invoke();
        }

        public void ShuffleDrawPile()
        {
            Shuffle(_draw);
            DeckChanged?.Invoke();
        }

        private static void Shuffle(List<CardDefinition> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int r = Random.Range(i, list.Count);
                (list[i], list[r]) = (list[r], list[i]);
            }
        }

        // ─────────────────────────────────────────
        // Add Cards (mid-battle)
        // ─────────────────────────────────────────
        public void AddToHand(CardDefinition card)
        {
            if (card == null) return;
            _hand.Add(card);
            DeckChanged?.Invoke();
        }

        public void AddToDrawTop(CardDefinition card)
        {
            if (card == null) return;
            _draw.Insert(0, card);
            DeckChanged?.Invoke();
        }

        public void AddToDiscard(CardDefinition card)
        {
            if (card == null) return;
            _discard.Add(card);
            DeckChanged?.Invoke();
        }
    }
}
