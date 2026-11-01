using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Edit Mode tests for Health. This folder is named Editor, so Unity compiles it into the
// built-in editor assembly, which can see the game scripts and the test framework.
public class HealthTests
{
    private GameObject gameObject;
    private Health health;

    [SetUp]
    public void SetUp()
    {
        gameObject = new GameObject("Health test");
        health = CreateHealth(gameObject, 5);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gameObject);
    }

    // Unity doesn't run Awake in Edit Mode, so the tests call it the way Unity would at startup.
    public static Health CreateHealth(GameObject owner, int maxHealth)
    {
        Health created = owner.GetComponent<Health>();
        if (created == null) created = owner.AddComponent<Health>();
        var settings = new SerializedObject(created);
        settings.FindProperty("maxHealth").intValue = maxHealth;
        settings.ApplyModifiedPropertiesWithoutUndo();
        TestMessages.Send(created, "Awake");
        return created;
    }

    [Test]
    public void StartsAtMaxHealth()
    {
        Assert.AreEqual(5, health.MaxHealth);
        Assert.AreEqual(5, health.CurrentHealth);
        Assert.IsFalse(health.IsDead);
    }

    [Test]
    public void TakeDamage_RemovesThatMuchHealth()
    {
        int reportedHealth = -1;
        health.HealthChanged += (current, max) => reportedHealth = current;

        health.TakeDamage(2);

        Assert.AreEqual(3, health.CurrentHealth);
        Assert.AreEqual(3, reportedHealth);
        Assert.IsFalse(health.IsDead);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public void TakeDamage_IgnoresZeroAndNegativeAmounts(int amount)
    {
        int changes = 0;
        health.HealthChanged += (current, max) => changes++;

        health.TakeDamage(amount);

        Assert.AreEqual(5, health.CurrentHealth);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void TakeDamage_NeverGoesBelowZero()
    {
        health.TakeDamage(999);

        Assert.AreEqual(0, health.CurrentHealth);
        Assert.IsTrue(health.IsDead);

        health.TakeDamage(1);
        Assert.AreEqual(0, health.CurrentHealth);
    }

    [Test]
    public void Heal_AddsHealthUpToTheMaximum()
    {
        health.TakeDamage(3);

        health.Heal(1);
        Assert.AreEqual(3, health.CurrentHealth);

        health.Heal(50);
        Assert.AreEqual(5, health.CurrentHealth);
    }

    [TestCase(0)]
    [TestCase(-2)]
    public void Heal_IgnoresZeroAndNegativeAmounts(int amount)
    {
        health.TakeDamage(3);

        health.Heal(amount);

        Assert.AreEqual(2, health.CurrentHealth);
    }

    [Test]
    public void Heal_DoesNotReviveTheDead()
    {
        health.TakeDamage(5);

        health.Heal(3);

        Assert.AreEqual(0, health.CurrentHealth);
        Assert.IsTrue(health.IsDead);
    }

    [Test]
    public void RestoreFullHealth_FillsHealthAndRevives()
    {
        health.TakeDamage(5);
        int reportedHealth = -1;
        health.HealthChanged += (current, max) => reportedHealth = current;

        health.RestoreFullHealth();

        Assert.AreEqual(5, health.CurrentHealth);
        Assert.AreEqual(5, reportedHealth);
        Assert.IsFalse(health.IsDead);
    }

    [Test]
    public void Died_FiresOnceWhenHealthReachesZero()
    {
        int deaths = 0;
        health.Died += () => deaths++;

        health.TakeDamage(4);
        Assert.AreEqual(0, deaths);

        health.TakeDamage(1);
        health.TakeDamage(1);
        health.TakeDamage(10);

        Assert.AreEqual(1, deaths);
    }
}

// Calls a private Unity message such as Awake or OnEnable, which Unity skips in Edit Mode.
public static class TestMessages
{
    public static void Send(Object target, string message)
    {
        var method = target.GetType().GetMethod(message,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Assert.IsNotNull(method, $"{target.GetType().Name} has no {message} method.");
        method.Invoke(target, null);
    }
}
