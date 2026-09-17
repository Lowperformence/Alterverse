using System;
using UnityEngine;

namespace Alterverse.Progression
{
    public sealed class HeroLevel : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxLevel = 10;
        [SerializeField, Min(1)] private int baseXpToLevel = 100;
        [SerializeField, Min(0)] private int xpIncreasePerLevel = 35;

        public int Level { get; private set; } = 1;
        public int CurrentXp { get; private set; }
        public int MaxLevel => maxLevel;
        public int XpToNextLevel => Level >= maxLevel ? 0 : baseXpToLevel + (Level - 1) * xpIncreasePerLevel;

        public event Action<int> LevelChanged;
        public event Action<int, int> XpChanged;

        public void AddXp(int amount)
        {
            if (amount <= 0 || Level >= maxLevel) return;

            CurrentXp += amount;
            while (Level < maxLevel && CurrentXp >= XpToNextLevel)
            {
                CurrentXp -= XpToNextLevel;
                Level++;
                LevelChanged?.Invoke(Level);
            }

            if (Level >= maxLevel) CurrentXp = 0;
            XpChanged?.Invoke(CurrentXp, XpToNextLevel);
        }

        public bool IsAbilityUnlocked(int abilitySlot)
        {
            return abilitySlot switch
            {
                0 => Level >= 1,
                1 => Level >= 2,
                2 => Level >= 3,
                3 => Level >= 5,
                _ => false
            };
        }

        public void ResetForMatch()
        {
            Level = 1;
            CurrentXp = 0;
            LevelChanged?.Invoke(Level);
            XpChanged?.Invoke(CurrentXp, XpToNextLevel);
        }
    }
}
