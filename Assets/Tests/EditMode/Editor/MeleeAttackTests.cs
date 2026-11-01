using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Edit Mode tests for melee combat: MeleeAttack finds and damages targets,
// Knockback pushes them, HitFlash flashes them and PracticeDummy resets.
// Everything is built far from the map, so the scene's own colliders never get in the way.
public class MeleeAttackTests
{
    private static readonly Vector3 Origin = new Vector3(5000f, 500f, 5000f);

    private GameObject attacker;
    private Transform view;
    private MeleeAttack melee;
    private GameObject target;
    private Health targetHealth;
    private GameObject wall;

    [SetUp]
    public void SetUp()
    {
        // The attacker stands at Origin looking along +Z, with its own body and Health inside the hitbox.
        attacker = new GameObject("Attacker");
        attacker.transform.position = Origin;
        attacker.AddComponent<CapsuleCollider>();
        HealthTests.CreateHealth(attacker, 100);
        view = new GameObject("View").transform;
        view.SetParent(attacker.transform, false);
        melee = attacker.AddComponent<MeleeAttack>();
        var settings = new SerializedObject(melee);
        settings.FindProperty("viewPoint").objectReferenceValue = view;
        settings.FindProperty("damage").intValue = 25;
        settings.FindProperty("reach").floatValue = 2f;
        settings.FindProperty("hitboxSize").vector2Value = new Vector2(1f, 1.2f);
        settings.FindProperty("knockbackStrength").floatValue = 6f;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(attacker);
        if (target != null) Object.DestroyImmediate(target);
        if (wall != null) Object.DestroyImmediate(wall);
    }

    // A target with Health on its root and three colliders on its children, like the practice dummy.
    private void CreateTarget(float distance)
    {
        target = new GameObject("Target");
        target.transform.position = Origin + Vector3.forward * distance;
        targetHealth = HealthTests.CreateHealth(target, 100);
        foreach (string part in new[] { "Body", "Arms", "Head" })
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = part;
            child.transform.SetParent(target.transform, false);
            child.transform.localScale = Vector3.one * 0.4f;
        }
        target.transform.Find("Arms").localPosition = Vector3.right * 0.3f;
        target.transform.Find("Head").localPosition = Vector3.up * 0.3f;
    }

    private int Swing()
    {
        // In Edit Mode the swing's coroutine stops at its first wait, so the hit window is run by hand.
        Assert.IsTrue(melee.TryAttack(), "A new swing should start.");
        Physics.SyncTransforms();
        return melee.CheckHitbox();
    }

    [Test]
    public void Hit_InFront_DamagesOnceEvenWithSeveralColliders()
    {
        CreateTarget(1.2f);
        int events = 0;
        melee.HitLanded += hit => events++;

        Assert.AreEqual(1, Swing());
        Assert.AreEqual(75, targetHealth.CurrentHealth);
        Assert.AreEqual(1, events);

        // Later checks in the same swing find the same colliders again, but the target is already hit.
        Assert.AreEqual(0, melee.CheckHitbox());
        Assert.AreEqual(75, targetHealth.CurrentHealth);
    }

    [Test]
    public void Miss_OutOfReachOrBehind_DoesNothing()
    {
        CreateTarget(4f);
        Assert.AreEqual(0, Swing());
        Assert.AreEqual(100, targetHealth.CurrentHealth);

        target.transform.position = Origin - Vector3.forward * 1.2f;
        Physics.SyncTransforms();
        Assert.AreEqual(0, melee.CheckHitbox());
        Assert.AreEqual(100, targetHealth.CurrentHealth);
    }

    [Test]
    public void Swing_IgnoresTheAttackerAndThingsWithoutHealth()
    {
        // A crate with no Health, right in front.
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = Origin + new Vector3(0.6f, 0f, 1f);
        wall.transform.localScale = Vector3.one * 0.3f;

        Assert.AreEqual(0, Swing());
        Assert.AreEqual(100, attacker.GetComponent<Health>().CurrentHealth, "The attacker's own collider is inside the hitbox.");
    }

    [Test]
    public void Wall_BetweenAttackerAndTarget_BlocksTheHit()
    {
        CreateTarget(1.6f);
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.position = Origin + Vector3.forward * 0.9f;
        wall.transform.localScale = new Vector3(3f, 3f, 0.1f);

        Assert.AreEqual(0, Swing());
        Assert.AreEqual(100, targetHealth.CurrentHealth);
    }

    [Test]
    public void Swing_CantStartAgainUntilTheLastOneEnds()
    {
        Assert.IsTrue(melee.TryAttack());
        Assert.IsTrue(melee.IsBusy);
        Assert.IsFalse(melee.TryAttack(), "Clicking during a swing must not start another.");

        // Disabling the attacker cancels the swing, so it can't stay busy forever.
        TestMessages.Send(melee, "OnDisable");
        Assert.IsFalse(melee.IsBusy);
    }

    [Test]
    public void DeadTarget_IsNotHit()
    {
        CreateTarget(1.2f);
        targetHealth.TakeDamage(100);
        Assert.AreEqual(0, Swing());
    }

    [Test]
    public void Hit_PushesTheTargetAwayFromTheAttacker()
    {
        CreateTarget(1.2f);
        var body = target.AddComponent<Rigidbody>();
        var knockback = target.AddComponent<Knockback>();
        TestMessages.Send(knockback, "Awake");

        Swing();
        Assert.IsTrue(knockback.IsActive);
        TestMessages.Send(knockback, "FixedUpdate");
        Assert.Greater(body.linearVelocity.z, 0f, "The target moves away from the attacker along +Z.");
        Assert.AreEqual(0f, body.linearVelocity.x, 0.001f);
    }

    [Test]
    public void Knockback_FadesOutAndKeepsGravity()
    {
        target = new GameObject("Pushed");
        var body = target.AddComponent<Rigidbody>();
        var knockback = target.AddComponent<Knockback>();
        TestMessages.Send(knockback, "Awake");

        knockback.Push(new Vector3(6f, 3f, 0f));
        body.linearVelocity = new Vector3(0f, -2f, 0f);
        TestMessages.Send(knockback, "FixedUpdate");
        Assert.AreEqual(6f, body.linearVelocity.x, 0.001f, "The push starts at full speed.");
        Assert.AreEqual(-2f, body.linearVelocity.y, 0.001f, "A push never changes the up and down speed.");

        for (int i = 0; i < 100 && knockback.IsActive; i++) TestMessages.Send(knockback, "FixedUpdate");
        Assert.IsFalse(knockback.IsActive, "The push ends by itself.");
        Assert.AreEqual(Vector3.zero, knockback.CurrentVelocity);
    }

    [Test]
    public void Dummy_LeavesThePushAloneWhileItLasts_ThenReturns()
    {
        target = new GameObject("Dummy");
        target.transform.position = Origin;
        var body = target.AddComponent<Rigidbody>();
        HealthTests.CreateHealth(target, 100);
        var knockback = target.AddComponent<Knockback>();
        var dummy = target.AddComponent<PracticeDummy>();
        TestMessages.Send(knockback, "Awake");
        TestMessages.Send(dummy, "Awake");

        knockback.Push(new Vector3(5f, 0f, 0f));
        TestMessages.Send(knockback, "FixedUpdate");
        Vector3 pushed = body.linearVelocity;
        TestMessages.Send(dummy, "FixedUpdate");
        Assert.AreEqual(pushed, body.linearVelocity, "The dummy's own movement must not cancel the push.");

        // Once the push and the wait are over, it heads back to its spot.
        knockback.Stop();
        target.transform.position = Origin + Vector3.right * 1f;
        Physics.SyncTransforms();
        for (int i = 0; i < 200; i++) TestMessages.Send(dummy, "FixedUpdate");
        Assert.Less(body.linearVelocity.x, 0f, "The dummy slides back toward where it started.");
    }

    [Test]
    public void Dummy_Reset_RestoresHealthAndPosition()
    {
        target = new GameObject("Dummy");
        target.transform.position = Origin;
        var body = target.AddComponent<Rigidbody>();
        targetHealth = HealthTests.CreateHealth(target, 100);
        var knockback = target.AddComponent<Knockback>();
        var dummy = target.AddComponent<PracticeDummy>();
        TestMessages.Send(knockback, "Awake");
        TestMessages.Send(dummy, "Awake");

        targetHealth.TakeDamage(100);
        StringAssert.Contains("knocked out", dummy.GetPrompt());
        knockback.Push(Vector3.right * 5f);
        target.transform.position = Origin + Vector3.right * 2f;
        body.linearVelocity = Vector3.right * 3f;

        dummy.ResetDummy();
        Assert.AreEqual(100, targetHealth.CurrentHealth);
        Assert.AreEqual(Origin, target.transform.position);
        Assert.AreEqual(Vector3.zero, body.linearVelocity);
        Assert.IsFalse(knockback.IsActive);
        StringAssert.Contains("100/100", dummy.GetPrompt());
    }

    [Test]
    public void HitFlash_ShowsTheColourThenRestoresTheOriginalLook()
    {
        target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        targetHealth = HealthTests.CreateHealth(target, 100);
        var renderer = target.GetComponent<Renderer>();
        var flash = target.AddComponent<HitFlash>();
        TestMessages.Send(flash, "Awake");
        TestMessages.Send(flash, "OnEnable");

        // Damage flashes the object; a second hit during the flash keeps it flashing.
        targetHealth.TakeDamage(10);
        Assert.IsTrue(flash.IsFlashing);
        Assert.IsTrue(renderer.HasPropertyBlock());
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Assert.AreEqual(Color.white, block.GetColor("_BaseColor"));
        targetHealth.TakeDamage(10);

        flash.StopFlash();
        Assert.IsFalse(flash.IsFlashing);
        Assert.IsFalse(renderer.HasPropertyBlock(), "The renderer had no property block before, so none is left.");
        TestMessages.Send(flash, "OnDisable");
    }

    [Test]
    public void HitFlash_EndsByItselfWhenItsTimeIsUp()
    {
        target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        targetHealth = HealthTests.CreateHealth(target, 100);
        var renderer = target.GetComponent<Renderer>();
        var flash = target.AddComponent<HitFlash>();
        TestMessages.Send(flash, "Awake");

        flash.Flash();
        flash.Advance(0.05f);
        Assert.IsTrue(flash.IsFlashing, "0.05 seconds is shorter than the 0.12 second flash.");
        flash.Advance(0.1f);
        Assert.IsFalse(flash.IsFlashing);
        Assert.IsFalse(renderer.HasPropertyBlock(), "The flash colour must be removed when the timer runs out.");
    }

    [Test]
    public void HitFlash_KeepsAPropertyBlockTheRendererAlreadyHad()
    {
        target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        targetHealth = HealthTests.CreateHealth(target, 100);
        var renderer = target.GetComponent<Renderer>();
        var own = new MaterialPropertyBlock();
        own.SetColor("_BaseColor", Color.green);
        renderer.SetPropertyBlock(own);
        var flash = target.AddComponent<HitFlash>();
        TestMessages.Send(flash, "Awake");

        flash.Flash();
        flash.Flash(); // flashing twice must not save the flash colour as the original
        flash.StopFlash();

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Assert.AreEqual(Color.green, block.GetColor("_BaseColor"));
    }

    [Test]
    public void Hatchet_IsAToolItem()
    {
        Assert.AreEqual(ToolType.Hatchet, ResourceTypes.ToTool(ResourceType.Hatchet));
        Assert.AreEqual(ResourceType.Hatchet, ResourceTypes.FromTool(ToolType.Hatchet));
        Assert.IsTrue(ResourceTypes.IsTool(ResourceType.Hatchet));
    }
}
