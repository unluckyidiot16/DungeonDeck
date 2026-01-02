// Assets/_Project/Config/Cards/CardDefinition.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DungeonDeck.Config.Cards
{
    public enum CardEffectKind
    {
        Attack,
        Block,
        Draw,
        GainEnergy,
        ApplyVulnerable,
        Heal,
    }

    public enum CardEffectTarget
    {
        Auto = 0,      // kind 기반으로 자동 추론(구버전 호환 기본값)
        Self = 1,      // 플레이어
        Enemy = 2,     // 단일 적(선택/요청 슬롯)
        AllEnemies = 3 // 전체 적
    }

    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    // 인스펙터 저장용(멀티 이펙트)
    [Serializable]
    public struct CardEffectSpec
    {
        public CardEffectKind kind;
        public int value;

        [Min(1)]
        [Tooltip("같은 효과 반복 횟수(연타/반복). 기본 1")]
        public int repeat;

        [Tooltip("Auto면 kind 기반으로 대상 자동 추론")]
        public CardEffectTarget target;
    }

    // 런타임/호환용(단일 진실 타입)
    [Serializable]
    public struct CardEffectEntry
    {
        public CardEffectKind kind;
        public CardEffectTarget target;
        public int value;

        [Min(1)]
        public int repeat;

        public CardEffectEntry(CardEffectKind kind, CardEffectTarget target, int value, int repeat = 1)
        {
            this.kind = kind;
            this.target = target;
            this.value = value;
            this.repeat = Mathf.Max(1, repeat);
        }
    }

    [CreateAssetMenu(menuName = "DungeonDeck/Cards/Card", fileName = "CardDefinition")]
    public class CardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "strike";

        [Header("Display")]
        public string displayName = "Strike";
        public Sprite icon;
        public CardRarity rarity = CardRarity.Common;

        [TextArea(2, 4)]
        public string effectTextOverride;

        [Header("Cost/Effect (Legacy v1)")]
        public int cost = 1;
        public CardEffectKind effectKind = CardEffectKind.Attack;
        public int value = 6;

        [Header("Effects (Multi)")]
        [Tooltip("비어있으면 Legacy(effectKind/value)를 자동으로 1개 효과로 처리합니다.")]
        public List<CardEffectSpec> effects = new();

        [Header("Keywords")]
        [Tooltip("사용 후 전투에서 제거(Exhaust)")]
        public bool exhaustOnPlay = false;

        [Header("Animation - Approach")]
        [Tooltip("적에게 접근할 때 사용할 애니메이션")]
        public ApproachAnimType approachAnimType = ApproachAnimType.Run;

        [Header("Animation - Attack")]
        [Tooltip("공격 시 사용할 애니메이션 (Attack 타입 카드에만 적용)")]
        public AttackAnimType attackAnimType = AttackAnimType.Slash;
        
        [Header("UI - Target Icon Sprite Tags (TMP)")]
        [Tooltip("GetCompactEffectLine()에서 🎯🧑🌐 대신 TMP 인라인 스프라이트(<sprite name=...>)를 사용")]
        public bool useTargetSpriteTags = true;

        [Tooltip("TMP Sprite Asset 안의 sprite name")]
        public string targetSpriteSelf = "target_self";
        public string targetSpriteEnemy = "target_enemy";
        public string targetSpriteAll = "target_all";

        [Tooltip("스프라이트가 없을 때 fallback(디버그용)")]
        public bool fallbackToEmojiIfSpriteTagMissing = true;


        // ─────────────────────────────────────────────
        // v2: Multi Effects + v1: Legacy fallback (단일 API)
        // ─────────────────────────────────────────────
        public IEnumerable<CardEffectEntry> EnumerateEffects()
        {
            if (effects != null && effects.Count > 0)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var e = effects[i];
                    yield return new CardEffectEntry(
                        e.kind,
                        e.target,
                        e.value,
                        Mathf.Max(1, e.repeat)
                    );
                }
                yield break;
            }

            // Legacy fallback
            yield return new CardEffectEntry(
                effectKind,
                CardEffectTarget.Auto,
                value,
                1
            );
        }

        public bool HasEffect(CardEffectKind kind)
        {
            foreach (var e in EnumerateEffects())
                if (e.kind == kind && e.value != 0) return true;
            return false;
        }

        public bool HasAnyEnemyTargetEffect()
        {
            foreach (var e in EnumerateEffects())
            {
                if (e.value == 0) continue;
                var t = ResolveTarget(e.target, e.kind);
                if (t == CardEffectTarget.Enemy || t == CardEffectTarget.AllEnemies)
                    return true;
            }
            return false;
        }
        
        // CardDefinition.cs 안에 추가 (HasAnyEnemyTargetEffect() 근처에 두면 깔끔)
        public bool HasAnySingleEnemyTargetEffect()
        {
            foreach (var e in EnumerateEffects())
            {
                if (e.value == 0) continue;

                // CardDefinition 내부에 있는 ResolveTarget / DefaultTargetFor를 그대로 활용
                var t = ResolveTarget(e.target, e.kind);
                if (t == CardEffectTarget.Enemy) return true;
            }
            return false;
        }

        public bool HasAnyAllEnemiesTargetEffect()
        {
            foreach (var e in EnumerateEffects())
            {
                if (e.value == 0) continue;

                var t = ResolveTarget(e.target, e.kind);
                if (t == CardEffectTarget.AllEnemies) return true;
            }
            return false;
        }


        // CardDefinition.cs 안에서 아래 두 개만 public으로 변경
        public static CardEffectTarget DefaultTargetFor(CardEffectKind kind)
        {
            switch (kind)
            {
                case CardEffectKind.Attack:
                case CardEffectKind.ApplyVulnerable:
                    return CardEffectTarget.Enemy;
                default:
                    return CardEffectTarget.Self;
            }
        }

        public static CardEffectTarget ResolveTarget(CardEffectTarget t, CardEffectKind kind)
            => (t != CardEffectTarget.Auto) ? t : DefaultTargetFor(kind);


        // ─────────────────────────────────────────────
        // UI helpers (CardView)
        // ─────────────────────────────────────────────
        public string GetDisplayName()
            => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

        // ✅ “효과별 타겟 아이콘 라인”
        // 예: 🎯⚔6x2 + 🧑🛡5 + 🌐🧪취약1
        public string GetCompactEffectLine()
        {
            var sb = new StringBuilder(64);
            bool first = true;

            foreach (var raw in EnumerateEffects())
            {
                if (raw.value == 0) continue;

                var t = ResolveTarget(raw.target, raw.kind);
                int rep = Mathf.Max(1, raw.repeat);

                if (!first) sb.Append(" + ");

                sb.Append(TargetIcon(t));
                sb.Append(EffectIcon(raw.kind));

                if (raw.kind == CardEffectKind.ApplyVulnerable)
                    sb.Append("취약");

                sb.Append(raw.value);

                if (rep > 1) sb.Append("x").Append(rep);

                first = false;
            }

            return first ? string.Empty : sb.ToString();
        }

        // (기존 요약 라인 유지: 타겟 아이콘 없이)
        public string GetEffectText()
        {
            if (!string.IsNullOrWhiteSpace(effectTextOverride))
                return effectTextOverride;

            var parts = new List<string>(4);
            foreach (var e in EnumerateEffects())
            {
                if (e.value == 0) continue;
                parts.Add(FormatEffectIcon(e.kind, e.value, Mathf.Max(1, e.repeat)));
            }

            return parts.Count == 0 ? "" : string.Join(" + ", parts);
        }

        private string TargetIcon(CardEffectTarget t)
        {
            // 1) TMP 인라인 스프라이트 태그 사용
            if (useTargetSpriteTags)
            {
                string name = t switch
                {
                    CardEffectTarget.Self => targetSpriteSelf,
                    CardEffectTarget.Enemy => targetSpriteEnemy,
                    CardEffectTarget.AllEnemies => targetSpriteAll,
                    _ => null
                };

                if (!string.IsNullOrEmpty(name))
                    return $"<sprite name=\"{name}\">";
            }

            // 2) fallback (이모지)
            if (!fallbackToEmojiIfSpriteTagMissing) return "";

            return t switch
            {
                CardEffectTarget.Self => "🧑",
                CardEffectTarget.Enemy => "🎯",
                CardEffectTarget.AllEnemies => "🌐",
                _ => ""
            };
        }


        private static string EffectIcon(CardEffectKind k)
        {
            switch (k)
            {
                case CardEffectKind.Attack: return "⚔";
                case CardEffectKind.Block: return "🛡";
                case CardEffectKind.Draw: return "🎴";
                case CardEffectKind.GainEnergy: return "⚡";
                case CardEffectKind.ApplyVulnerable: return "🧪";
                case CardEffectKind.Heal: return "✚";
                default: return "";
            }
        }

        private static string FormatEffectIcon(CardEffectKind kind, int v, int repeat)
        {
            string tail = repeat > 1 ? $"x{repeat}" : "";
            switch (kind)
            {
                case CardEffectKind.Attack:          return $"⚔️{v}{tail}";
                case CardEffectKind.Block:           return $"🛡️{v}{tail}";
                case CardEffectKind.Draw:            return $"🃏{v}{tail}";
                case CardEffectKind.GainEnergy:      return $"⚡{v}{tail}";
                case CardEffectKind.ApplyVulnerable: return $"🧪취약{v}{tail}";
                case CardEffectKind.Heal:            return $"❤️{v}{tail}";
                default:                             return $"{kind} {v}{tail}";
            }
        }
    }
}
