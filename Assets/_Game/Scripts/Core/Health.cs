using System;
using UnityEngine;

// Hit points for anything that can be hurt: the player, enemies, animals or bosses.
// It only counts health. What happens on death (respawning, dropping loot, playing an
// animation) belongs in another component that listens to the Died event.
//
// Example:
//   health.HealthChanged += (current, max) => Debug.Log($"{current}/{max}");
//   health.Died += OnDied;
//   health.TakeDamage(20);
public class Health : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;

    private int currentHealth;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    // Current health, then max health.
    public event Action<int, int> HealthChanged;
    // How much health a hit actually removed (a 5 damage hit on 2 health reports 2).
    public event Action<int> Damaged;
    // Raised once when health reaches zero. RestoreFullHealth brings the object back.
    public event Action Died;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        // Zero or negative damage would do nothing or heal, so it is ignored.
        // A dead object can't be hurt again, which also stops Died firing twice.
        if (amount <= 0 || IsDead)
        {
            return;
        }

        int before = currentHealth;
        currentHealth = Mathf.Max(currentHealth - amount, 0);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        Damaged?.Invoke(before - currentHealth);

        if (currentHealth == 0)
        {
            Died?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        // Healing can't bring back the dead; use RestoreFullHealth for that.
        if (amount <= 0 || IsDead || currentHealth == maxHealth)
        {
            return;
        }

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // Fills health to the maximum. This also revives a dead object.
    public void RestoreFullHealth()
    {
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
