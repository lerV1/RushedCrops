using UnityEngine;

// How each of the game's particle effects looks and moves, kept in one place. The map builder uses these
// for the scenery (fire, smoke, river foam, pollen and fireflies) and GameEffects for the bursts of chips,
// dirt and leaves. Glow materials add light (fire, fireflies); soft ones blend over the scene.
public static class ParticleRecipes
{
    public static ParticleSystem Create(string name, Transform parent, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    public static void Fire(ParticleSystem system, Material glow)
    {
        var main = Main(system, true, 60, ParticleSystemSimulationSpace.Local);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.gravityModifier = -0.1f;
        Emit(system, 30f);
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.28f;
        shape.angle = 8f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        Fade(system, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.08f), 1f);
        Shrink(system, 1f, 0.2f);
        Look(system, glow);
    }

    public static void Smoke(ParticleSystem system, Material soft)
    {
        var main = Main(system, true, 30, ParticleSystemSimulationSpace.World);
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        Emit(system, 5f);
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.2f;
        shape.angle = 12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        Fade(system, new Color(0.55f, 0.55f, 0.55f), new Color(0.8f, 0.8f, 0.8f), 0.35f);
        Shrink(system, 0.6f, 2.4f);
        Wander(system, 0.25f, 0.3f);
        Look(system, soft);
    }

    // White spray rising off rapids, spread across the river.
    public static void Foam(ParticleSystem system, Material soft, float width)
    {
        var main = Main(system, true, 120, ParticleSystemSimulationSpace.World);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.gravityModifier = 0.9f;
        Emit(system, 40f);
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.1f, 1f);
        shape.rotation = new Vector3(-90f, 0f, 0f);
        shape.randomDirectionAmount = 0.35f;
        Fade(system, Color.white, new Color(0.85f, 0.95f, 1f), 0.8f);
        Look(system, soft);
    }

    // Pollen and seed fluff drifting in the sun. Its emitter follows the player (see AmbientLife).
    public static void Motes(ParticleSystem system, Material soft)
    {
        var main = Main(system, true, 200, ParticleSystemSimulationSpace.World);
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        Emit(system, 6f);
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 5f, 40f);
        Fade(system, new Color(1f, 0.97f, 0.85f), new Color(1f, 1f, 0.9f), 0.7f);
        Wander(system, 0.35f, 0.15f);
        Look(system, soft);
    }

    // Fireflies: small green-yellow lights that wander and pulse.
    public static void Fireflies(ParticleSystem system, Material glow)
    {
        var main = Main(system, true, 220, ParticleSystemSimulationSpace.World);
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        Emit(system, 0f);
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(36f, 2.5f, 36f);
        var colour = system.colorOverLifetime;
        colour.enabled = true;
        var pulse = new Gradient();
        pulse.SetKeys(
            new[] { new GradientColorKey(new Color(0.75f, 1f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.4f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.2f, 0.35f),
                    new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0.3f, 0.8f), new GradientAlphaKey(0f, 1f) });
        colour.color = pulse;
        Wander(system, 0.6f, 0.2f);
        Look(system, glow);
    }

    // A one-off spray, as when a hatchet bites into wood. Play it with Burst() after moving it into place.
    public static void Spray(ParticleSystem system, Material material, Color colour, float size, float speed, float gravity, int count)
    {
        var main = Main(system, false, count * 3, ParticleSystemSimulationSpace.World);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        main.startColor = new ParticleSystem.MinMaxGradient(colour, Color.Lerp(colour, Color.white, 0.25f));
        Emit(system, 0f);
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var colourOverLife = system.colorOverLifetime;
        colourOverLife.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colourOverLife.color = fade;
        Shrink(system, 1f, 0.4f);
        Look(system, material);
    }

    public static void Burst(ParticleSystem system, int count)
    {
        system.Emit(count);
    }

    // ---------- Shared pieces ----------

    private static ParticleSystem.MainModule Main(ParticleSystem system, bool loop, int max, ParticleSystemSimulationSpace space)
    {
        var main = system.main;
        main.loop = loop;
        main.playOnAwake = loop;
        main.maxParticles = max;
        main.simulationSpace = space;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        return main;
    }

    private static void Emit(ParticleSystem system, float rate)
    {
        var emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;
    }

    private static void Fade(ParticleSystem system, Color from, Color to, float alpha)
    {
        var colour = system.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.15f), new GradientAlphaKey(alpha * 0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
    }

    private static void Shrink(ParticleSystem system, float start, float end)
    {
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end));
    }

    private static void Wander(ParticleSystem system, float strength, float frequency)
    {
        var noise = system.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = 0.2f;
    }

    private static void Look(ParticleSystem system, Material material)
    {
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
