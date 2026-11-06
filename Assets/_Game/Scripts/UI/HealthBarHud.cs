using UnityEngine;
using UnityEngine.UI;

// The player's health bar: the fill shrinks at once, and a lighter strip behind it drains down after a moment.
public class HealthBarHud : MonoBehaviour
{
    [Tooltip("The Health to show, usually the player's.")]
    [SerializeField] private Health health;
    [SerializeField] private Image fill;
    [Tooltip("Optional. Shows the health just lost, then drains away.")]
    [SerializeField] private Image recentDamage;
    [Tooltip("Optional. Reads like \"3 / 5\".")]
    [SerializeField] private Text label;

    [Header("Look")]
    [SerializeField] private Color healthyColor = new Color(0.42f, 0.78f, 0.30f);
    [SerializeField] private Color lowColor = new Color(0.86f, 0.22f, 0.16f);
    [Tooltip("At or below this share of max health the bar turns the low color.")]
    [SerializeField, Range(0f, 1f)] private float lowHealthShare = 0.4f;

    [Header("Recent damage strip")]
    [SerializeField, Min(0f)] private float drainDelay = 0.4f;
    [Tooltip("How much of the bar drains per second.")]
    [SerializeField, Min(0.01f)] private float drainSpeed = 0.8f;

    private float shownShare = 1f;
    private float recentShare = 1f;
    private float drainStartTime;

    public float ShownShare => shownShare;
    public float RecentDamageShare => recentShare;

    private void OnEnable()
    {
        if (health == null || fill == null)
        {
            Debug.LogError("HealthBarHud needs a Health and a fill Image assigned.", this);
            enabled = false;
            return;
        }

        health.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
        }
    }

    // Start, not OnEnable, so Health has finished its Awake and holds its starting value.
    private void Start()
    {
        if (health != null)
        {
            Draw(health.CurrentHealth, health.MaxHealth, true);
        }
    }

    private void Update()
    {
        // Nothing to animate unless the strip is still longer than the fill.
        if (recentShare <= shownShare || Time.unscaledTime < drainStartTime)
        {
            return;
        }

        recentShare = Mathf.MoveTowards(recentShare, shownShare, drainSpeed * Time.unscaledDeltaTime);
        SetWidth(recentDamage, recentShare);
    }

    private void OnHealthChanged(int current, int max)
    {
        Draw(current, max, false);
    }

    private void Draw(int current, int max, bool snap)
    {
        float share = max > 0 ? (float)current / max : 0f;

        // Lost health waits in the strip; gained health (healing, respawning) shows at once.
        if (share < shownShare && !snap)
        {
            drainStartTime = Time.unscaledTime + drainDelay;
        }
        else
        {
            recentShare = share;
        }

        shownShare = share;
        SetWidth(fill, shownShare);
        SetWidth(recentDamage, recentShare);
        fill.color = share <= lowHealthShare ? lowColor : healthyColor;

        if (label != null)
        {
            label.text = $"{current} / {max}";
        }
    }

    private static void SetWidth(Image bar, float share)
    {
        if (bar != null)
        {
            bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
        }
    }
}
