using UnityEngine;
using UnityEngine.UI;

// The bar across the top of the screen during a boss fight: the boss's name and how much health it has left.
// It shows the nearest boss that is fighting the player and hides when no fight is on.
public class BossHealthHud : MonoBehaviour
{
    [Tooltip("The whole bar, hidden outside fights.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Text nameText;
    [SerializeField] private Image fill;
    [Tooltip("Optional. Reads like \"120 / 175\".")]
    [SerializeField] private Text amount;
    [SerializeField] private Color calmColor = new Color(0.86f, 0.22f, 0.16f);
    [Tooltip("The bar's colour once the boss gets angry and speeds up.")]
    [SerializeField] private Color angryColor = new Color(0.95f, 0.55f, 0.1f);

    private void Update()
    {
        if (panel == null || fill == null)
        {
            return;
        }

        CropBoss shown = null;
        BossCombat shownCombat = null;
        float nearest = float.MaxValue;
        Vector3 here = Camera.main != null ? Camera.main.transform.position : transform.position;
        foreach (CropBoss boss in CropBoss.All)
        {
            var combat = boss.GetComponent<BossCombat>();
            if (combat == null || !combat.IsFighting)
            {
                continue;
            }
            float distance = Vector3.Distance(here, boss.transform.position);
            if (distance < nearest)
            {
                nearest = distance;
                shown = boss;
                shownCombat = combat;
            }
        }

        panel.SetActive(shown != null);
        if (shown == null)
        {
            return;
        }
        int current = shown.Health.CurrentHealth, max = shown.Health.MaxHealth;
        if (nameText != null && nameText.text != shown.DisplayName)
        {
            nameText.text = shown.DisplayName;
        }
        fill.rectTransform.anchorMax = new Vector2(max > 0 ? Mathf.Clamp01((float)current / max) : 0f, 1f);
        fill.color = shownCombat.IsAngry ? angryColor : calmColor;
        if (amount != null)
        {
            amount.text = $"{current} / {max}";
        }
    }
}
