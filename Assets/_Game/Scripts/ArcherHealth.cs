using System;
using UnityEngine;

namespace BowMan
{
    public sealed class ArcherHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;

        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDefeated => CurrentHealth == 0;
        public event Action Defeated;

        // Validates the configured health limit and starts the archer at full health.
        private void Awake()
        {
            if (maxHealth <= 0)
                throw new InvalidOperationException("Maximum health must be positive.");
            CurrentHealth = maxHealth;
        }

        // Applies positive damage, clamps health at zero, and announces defeat only on the first lethal hit.
        public void TakeDamage(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Damage must be positive.");
            if (IsDefeated) return;

            CurrentHealth = Math.Max(0, CurrentHealth - amount);
            if (IsDefeated) Defeated?.Invoke();
        }
    }
}
