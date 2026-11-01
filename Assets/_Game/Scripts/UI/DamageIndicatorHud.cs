using UnityEngine;
using UnityEngine.UI;

// Shows the player they were hurt: the screen edges flash red, and a "-2" floats up
// beside the health bar and fades out. Bigger hits give a stronger flash.
public class DamageIndicatorHud : MonoBehaviour
{
    [Tooltip("The Health to watch, usually the player's.")]
    [SerializeField] private Health health;
    [Tooltip("A full-screen red edge image. It should not block clicks (Raycast Target off).")]
    [SerializeField] private Image flash;
    [Tooltip("Optional. Shows how much damage was taken.")]
    [SerializeField] private Text damageText;

    [Header("Flash")]
    [Tooltip("Flash strength for the smallest hit.")]
    [SerializeField, Range(0f, 1f)] private float minFlashAlpha = 0.35f;
    [Tooltip("Flash strength for a hit that takes all health at once.")]
    [SerializeField, Range(0f, 1f)] private float maxFlashAlpha = 0.7f;
    [SerializeField, Min(0.05f)] private float flashFadeTime = 0.6f;

    [Header("Damage number")]
    [SerializeField, Min(0.05f)] private float numberTime = 0.9f;
    [Tooltip("How far the number floats up, in canvas pixels.")]
    [SerializeField] private float numberRise = 40f;

    private float flashAlpha;
    private float numberTimeLeft;
    private Vector2 numberStart;

    public float FlashAlpha => flashAlpha;

    private void Awake()
    {
        if (damageText != null)
        {
            numberStart = damageText.rectTransform.anchoredPosition;
        }
        SetFlashAlpha(0f);
        SetNumberVisible(false);
    }

    private void OnEnable()
    {
        if (health == null || flash == null)
        {
            Debug.LogError("DamageIndicatorHud needs a Health and a flash Image assigned.", this);
            enabled = false;
            return;
        }

        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }
    }

    private void Update()
    {
        // Unscaled time keeps the effect fading even if the game is paused.
        if (flashAlpha > 0f)
        {
            SetFlashAlpha(Mathf.MoveTowards(flashAlpha, 0f, maxFlashAlpha / flashFadeTime * Time.unscaledDeltaTime));
        }

        if (numberTimeLeft > 0f)
        {
            numberTimeLeft -= Time.unscaledDeltaTime;
            float progress = 1f - Mathf.Clamp01(numberTimeLeft / numberTime);
            damageText.rectTransform.anchoredPosition = numberStart + Vector2.up * (numberRise * progress);
            Color color = damageText.color;
            color.a = 1f - progress * progress;
            damageText.color = color;
            if (numberTimeLeft <= 0f)
            {
                SetNumberVisible(false);
            }
        }
    }

    private void OnDamaged(int amount)
    {
        float share = health.MaxHealth > 0 ? Mathf.Clamp01((float)amount / health.MaxHealth) : 1f;
        SetFlashAlpha(Mathf.Max(flashAlpha, Mathf.Lerp(minFlashAlpha, maxFlashAlpha, share)));

        if (damageText != null)
        {
            damageText.text = $"-{amount}";
            damageText.rectTransform.anchoredPosition = numberStart;
            Color color = damageText.color;
            color.a = 1f;
            damageText.color = color;
            numberTimeLeft = numberTime;
            SetNumberVisible(true);
        }
    }

    private void SetFlashAlpha(float alpha)
    {
        flashAlpha = alpha;
        if (flash != null)
        {
            Color color = flash.color;
            color.a = alpha;
            flash.color = color;
            flash.enabled = alpha > 0f;
        }
    }

    private void SetNumberVisible(bool visible)
    {
        if (damageText != null)
        {
            damageText.enabled = visible;
        }
    }
}
