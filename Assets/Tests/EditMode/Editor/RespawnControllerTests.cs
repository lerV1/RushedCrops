using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Edit Mode tests for RespawnController and DamageDealer working with Health.
public class RespawnControllerTests
{
    private GameObject player;
    private GameObject startCheckpoint;
    private GameObject laterCheckpoint;
    private Rigidbody body;
    private Health health;
    private RespawnController respawn;

    [SetUp]
    public void SetUp()
    {
        startCheckpoint = new GameObject("Start checkpoint");
        startCheckpoint.transform.position = new Vector3(1f, 2f, 3f);
        laterCheckpoint = new GameObject("Later checkpoint");
        laterCheckpoint.transform.position = new Vector3(-10f, 1f, 20f);

        player = new GameObject("Player");
        player.transform.position = new Vector3(50f, 5f, 50f);
        body = player.AddComponent<Rigidbody>();
        health = HealthTests.CreateHealth(player, 5);
        respawn = player.AddComponent<RespawnController>();

        var settings = new SerializedObject(respawn);
        settings.FindProperty("startingCheckpoint").objectReferenceValue = startCheckpoint.transform;
        settings.ApplyModifiedPropertiesWithoutUndo();

        // Unity skips these in Edit Mode, so run them the way Unity would at startup.
        TestMessages.Send(respawn, "Awake");
        TestMessages.Send(respawn, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        TestMessages.Send(respawn, "OnDisable");
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(startCheckpoint);
        Object.DestroyImmediate(laterCheckpoint);
    }

    [Test]
    public void StartsWithTheStartingCheckpoint()
    {
        Assert.AreEqual(startCheckpoint.transform, respawn.CurrentCheckpoint);
    }

    [Test]
    public void Death_RestoresFullHealthAtTheCheckpoint()
    {
        int respawns = 0;
        respawn.Respawned += () => respawns++;
        body.linearVelocity = new Vector3(3f, -8f, 1f);
        body.angularVelocity = new Vector3(0f, 2f, 0f);

        health.TakeDamage(5);

        Assert.AreEqual(1, respawns);
        Assert.AreEqual(5, health.CurrentHealth);
        Assert.IsFalse(health.IsDead);
        Assert.AreEqual(startCheckpoint.transform.position, player.transform.position);
        Assert.AreEqual(Vector3.zero, body.linearVelocity);
        Assert.AreEqual(Vector3.zero, body.angularVelocity);
    }

    [Test]
    public void SetCheckpoint_ChangesWhereThePlayerRespawns()
    {
        respawn.SetCheckpoint(laterCheckpoint.transform);

        health.TakeDamage(5);

        Assert.AreEqual(laterCheckpoint.transform, respawn.CurrentCheckpoint);
        Assert.AreEqual(laterCheckpoint.transform.position, player.transform.position);
    }

    [Test]
    public void DamageThatDoesNotKill_DoesNotRespawn()
    {
        int respawns = 0;
        respawn.Respawned += () => respawns++;

        health.TakeDamage(4);

        Assert.AreEqual(0, respawns);
        Assert.AreEqual(1, health.CurrentHealth);
        Assert.AreEqual(new Vector3(50f, 5f, 50f), player.transform.position);
    }

    [Test]
    public void DamageDealer_HurtsTheHealthItIsGiven()
    {
        var hazard = new GameObject("Hazard");
        hazard.AddComponent<BoxCollider>();
        var dealer = hazard.AddComponent<DamageDealer>();
        var settings = new SerializedObject(dealer);
        settings.FindProperty("damage").intValue = 2;
        settings.ApplyModifiedPropertiesWithoutUndo();

        dealer.DealDamage(health);

        Assert.AreEqual(3, health.CurrentHealth);
        Object.DestroyImmediate(hazard);
    }

    [Test]
    public void DamageDealer_IgnoresInvalidDamage()
    {
        var hazard = new GameObject("Broken hazard");
        hazard.AddComponent<BoxCollider>();
        var dealer = hazard.AddComponent<DamageDealer>();
        var settings = new SerializedObject(dealer);
        settings.FindProperty("damage").intValue = 0;
        settings.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "Broken hazard has damage 0; it must be at least 1.");

        dealer.DealDamage(health);

        Assert.AreEqual(5, health.CurrentHealth);
        Object.DestroyImmediate(hazard);
    }
}
