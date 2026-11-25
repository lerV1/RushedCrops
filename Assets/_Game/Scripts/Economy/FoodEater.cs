using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Lets the player eat harvested crops with H when hurt.
public class FoodEater : MonoBehaviour
{
    /// <summary>Raised after food was consumed and successfully restored health.</summary>
    public event System.Action AteFood;
    [Serializable]
    private struct FoodHealing
    {
        public ResourceType food;
        [Min(1)] public int healing;
    }

    [SerializeField] private InputActionReference eatAction;
    [SerializeField] private Health health;
    [SerializeField] private Inventory inventory;
    [SerializeField] private Text hintText;
    [SerializeField, Min(0f)] private float feedbackSeconds = 1.5f;
    [SerializeField] private FoodHealing[] foodValues =
    {
        new FoodHealing { food = ResourceType.Carrots, healing = 20 },
        new FoodHealing { food = ResourceType.Tomatoes, healing = 15 },
        new FoodHealing { food = ResourceType.Beets, healing = 25 },
        new FoodHealing { food = ResourceType.Cabbages, healing = 30 },
        new FoodHealing { food = ResourceType.Onions, healing = 18 },
        new FoodHealing { food = ResourceType.Wheat, healing = 12 }
    };

    private string feedback;
    private float feedbackUntil;

    private void OnEnable()
    {
        if (eatAction == null || health == null || inventory == null)
        {
            Debug.LogError("FoodEater needs the Eat Food action, player Health and shared Inventory assigned.", this);
            enabled = false;
            return;
        }

        eatAction.action.Enable();
        health.HealthChanged += OnStateChanged;
        inventory.Changed += OnStateChanged;
    }

    private void OnDisable()
    {
        if (eatAction != null) eatAction.action.Disable();
        if (health != null)
        {
            health.HealthChanged -= OnStateChanged;
        }
        if (inventory != null)
        {
            inventory.Changed -= OnStateChanged;
        }
        SetHint(string.Empty);
    }

    private void Start()
    {
        RefreshHint();
    }

    private void Update()
    {
        if (eatAction.action.WasPressedThisFrame())
        {
            TryEat();
        }

        if (!string.IsNullOrEmpty(feedback) && Time.unscaledTime >= feedbackUntil)
        {
            feedback = null;
            RefreshHint();
        }
    }

    public bool TryEat()
    {
        if (health == null || inventory == null || health.IsDead || health.CurrentHealth >= health.MaxHealth)
        {
            return false;
        }

        for (int i = 0; i < foodValues.Length; i++)
        {
            FoodHealing food = foodValues[i];
            if (food.healing <= 0 || inventory.GetCount(food.food) <= 0)
            {
                continue;
            }

            if (!inventory.TrySpend(food.food, 1))
            {
                return false;
            }

            health.Heal(food.healing);
            AteFood?.Invoke();
            feedback = $"Ate {ResourceTypes.DisplayName(food.food)} (+{food.healing} HP)";
            feedbackUntil = Time.unscaledTime + feedbackSeconds;
            GameEffects.Play(Effect.Sparkle, transform.position + Vector3.up, 1f);
            RefreshHint();
            return true;
        }

        return false;
    }

    private void OnStateChanged(int current, int max)
    {
        RefreshHint();
    }

    private void OnStateChanged()
    {
        RefreshHint();
    }

    private void RefreshHint()
    {
        if (hintText == null || health == null || inventory == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(feedback) && Time.unscaledTime < feedbackUntil)
        {
            SetHint(feedback);
            return;
        }

        if (health.IsDead || health.CurrentHealth >= health.MaxHealth)
        {
            SetHint(string.Empty);
            return;
        }

        for (int i = 0; i < foodValues.Length; i++)
        {
            FoodHealing food = foodValues[i];
            if (food.healing > 0 && inventory.GetCount(food.food) > 0)
            {
                SetHint($"[{eatAction.action.GetBindingDisplayString(0)}] Eat {ResourceTypes.DisplayName(food.food)} (+{food.healing} HP)");
                return;
            }
        }

        SetHint(string.Empty);
    }

    private void SetHint(string text)
    {
        if (hintText == null)
        {
            return;
        }

        hintText.text = text;
        hintText.enabled = !string.IsNullOrEmpty(text);
    }
}
