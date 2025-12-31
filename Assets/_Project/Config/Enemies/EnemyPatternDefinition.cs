// Assets/_Project/Scripts/Config/Enemies/EnemyPatternDefinition.cs
using System;
using UnityEngine;

namespace DungeonDeck.Config.Enemies
{
    [CreateAssetMenu(menuName = "DungeonDeck/Enemies/Enemy Pattern Definition", fileName = "EnemyPattern_")]
    public class EnemyPatternDefinition : ScriptableObject
    {
        [Serializable]
        public struct Step
        {
            [Tooltip("인텐드 아이콘 키 (예: atk, heavy, buff 등)")]
            public string intentId;

            [Tooltip("ATK에 곱하는 배율")]
            public float damageMultiplier;

            [Tooltip("추가 고정 데미지")]
            public int flatBonus;

            [Tooltip("연타 횟수(표시용/확장용)")]
            public int hits;

            public int EvalDamage(int atk)
            {
                float mul = Mathf.Max(0f, damageMultiplier);
                int h = Mathf.Max(1, hits);
                int baseDmg = Mathf.RoundToInt(atk * mul) + flatBonus;
                return Mathf.Max(0, baseDmg) * h;
            }
        }

        public bool loop = true;
        public Step[] steps = Array.Empty<Step>();

        public int StepCount => steps == null ? 0 : steps.Length;

        public Step GetStep(int index)
        {
            if (steps == null || steps.Length == 0) return default;

            if (loop)
            {
                int i = index % steps.Length;
                if (i < 0) i += steps.Length;
                return steps[i];
            }

            int clamped = Mathf.Clamp(index, 0, steps.Length - 1);
            return steps[clamped];
        }
    }
}