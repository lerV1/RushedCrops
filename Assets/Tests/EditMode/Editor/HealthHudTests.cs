using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Edit Mode tests for the Damaged event, the health bar and the damage indicator.
public class HealthHudTests
{
    private GameObject player;
    private GameObject hud;
    private Health health;
    private HealthBarHud bar;
    private DamageIndicatorHud indicator;
    private Image fill;
    private Image recentDamage;
    private Image flash;
    private Text label;
    private Text damageText;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        health = HealthTests.CreateHealth(player, 5);

        hud = new GameObject("HUD", typeof(RectTransform));
        fill = MakeImage("Fill");
        recentDamage = MakeImage("Recent damage");
        flash = MakeImage("Flash");
        label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(hud.transform, false);
        damageText = new GameObject("Damage number", typeof(RectTransform)).AddComponent<Text>();
        damageText.transform.SetParent(hud.transform, false);

        bar = hud.AddComponent<HealthBarHud>();
        var barSettings = new SerializedObject(bar);
        barSettings.FindProperty("health").objectReferenceValue = health;
        barSettings.FindProperty("fill").objectReferenceValue = fill;
        barSettings.FindProperty("recentDamage").objectReferenceValue = recentDamage;
        barSettings.FindProperty("label").objectReferenceValue = label;
        barSettings.ApplyModifiedPropertiesWithoutUndo();

        indicator = hud.AddComponent<DamageIndicatorHud>();
        var indicatorSettings = new SerializedObject(indicator);
        indicatorSettings.FindProperty("health").objectReferenceValue = health;
        indicatorSettings.FindProperty("flash").objectReferenceValue = flash;
        indicatorSettings.FindProperty("damageText").objectReferenceValue = damageText;
        indicatorSettings.ApplyModifiedPropertiesWithoutUndo();

        // Unity skips these in Edit Mode, so run them in the order Unity would.
        TestMessages.Send(indicator, "Awake");
        TestMessages.Send(bar, "OnEnable");
        TestMessages.Send(indicator, "OnEnable");
        TestMessages.Send(bar, "Start");
    }

    [TearDown]
    public void TearDown()
    {
        TestMessages.Send(bar, "OnDisable");
        TestMessages.Send(indicator, "OnDisable");
        Object.DestroyImmediate(hud);
        Object.DestroyImmediate(player);
    }

    private Image MakeImage(string name)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(hud.transform, false);
        return image;
    }

    [Test]
    public void Damaged_ReportsOnlyTheHealthActuallyLost()
    {
        int reported = 0;
        health.Damaged += amount => reported += amount;

        health.TakeDamage(2);
        Assert.AreEqual(2, reported);

        health.TakeDamage(10);
        Assert.AreEqual(5, reported);
    }

    [Test]
    public void Damaged_DoesNotFireForInvalidDamageOrHealing()
    {
        int hits = 0;
        health.Damaged += amount => hits++;

        health.TakeDamage(0);
        health.TakeDamage(-3);
        health.TakeDamage(1);
        health.Heal(1);

        Assert.AreEqual(1, hits);
    }

    [Test]
    public void Bar_StartsFull()
    {
        Assert.AreEqual(1f, fill.rectTransform.anchorMax.x, 0.001f);
        Assert.AreEqual("5 / 5", label.text);
    }

    [Test]
    public void Bar_ShrinksAtOnceAndKeepsTheRecentDamageStrip()
    {
        health.TakeDamage(2);

        Assert.AreEqual(0.6f, fill.rectTransform.anchorMax.x, 0.001f);
        Assert.AreEqual(1f, recentDamage.rectTransform.anchorMax.x, 0.001f);
        Assert.AreEqual("3 / 5", label.text);
    }

    [Test]
    public void Bar_TurnsTheLowColorWhenHealthIsLow()
    {
        Color healthy = fill.color;

        health.TakeDamage(3);

        Assert.AreNotEqual(healthy, fill.color);
    }

    [Test]
    public void Bar_FillsAtOnceWhenHealthIsRestored()
    {
        health.TakeDamage(4);

        health.RestoreFullHealth();

        Assert.AreEqual(1f, fill.rectTransform.anchorMax.x, 0.001f);
        Assert.AreEqual(1f, recentDamage.rectTransform.anchorMax.x, 0.001f);
        Assert.AreEqual("5 / 5", label.text);
    }

    [Test]
    public void Indicator_IsHiddenUntilDamaged()
    {
        Assert.AreEqual(0f, indicator.FlashAlpha);
        Assert.IsFalse(flash.enabled);
        Assert.IsFalse(damageText.enabled);
    }

    [Test]
    public void Indicator_FlashesAndShowsTheDamageTaken()
    {
        health.TakeDamage(2);

        Assert.Greater(indicator.FlashAlpha, 0f);
        Assert.IsTrue(flash.enabled);
        Assert.IsTrue(damageText.enabled);
        Assert.AreEqual("-2", damageText.text);
    }

    [Test]
    public void Indicator_BiggerHitsFlashHarder()
    {
        health.TakeDamage(1);
        float smallHit = indicator.FlashAlpha;
        TestMessages.Send(indicator, "Awake"); // clears the flash

        health.TakeDamage(4);

        Assert.Greater(indicator.FlashAlpha, smallHit);
    }

    [Test]
    public void Indicator_IgnoresHealing()
    {
        health.TakeDamage(2);
        TestMessages.Send(indicator, "Awake");

        health.Heal(1);
        health.RestoreFullHealth();

        Assert.AreEqual(0f, indicator.FlashAlpha);
        Assert.IsFalse(damageText.enabled);
    }
}
