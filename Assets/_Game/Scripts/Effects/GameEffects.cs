using UnityEngine;

// The little bursts that make actions feel solid: wood chips off a chopped tree, stone chips off a rock,
// dirt from the hoe, water from the can, leaves from a harvest, dust from a boss's slam. One particle system
// per kind is made at start and reused, so a burst costs nothing to spawn. Call GameEffects.Play from
// anywhere; with no GameEffects in the scene (as in some tests) it does nothing.
public enum Effect { WoodChips, StoneChips, Dirt, Leaves, Water, Dust, Sparkle, Juice }

public class GameEffects : MonoBehaviour
{
    [Tooltip("Particles that add light: sparkles.")]
    [SerializeField] private Material glow;
    [Tooltip("Particles that blend over the scene: chips, dirt, leaves, water, dust.")]
    [SerializeField] private Material soft;

    private static GameEffects instance;
    private ParticleSystem[] systems;
    private int[] counts;

    private void Awake()
    {
        instance = this;
        int kinds = System.Enum.GetValues(typeof(Effect)).Length;
        systems = new ParticleSystem[kinds];
        counts = new int[kinds];
        Make(Effect.WoodChips, soft, new Color(0.62f, 0.4f, 0.2f), 0.13f, 3.6f, 1.4f, 10);
        Make(Effect.StoneChips, soft, new Color(0.62f, 0.62f, 0.6f), 0.13f, 3.8f, 1.6f, 10);
        Make(Effect.Dirt, soft, new Color(0.45f, 0.31f, 0.17f), 0.24f, 2.2f, 1.1f, 12);
        Make(Effect.Leaves, soft, new Color(0.38f, 0.62f, 0.2f), 0.2f, 3f, 0.3f, 16);
        Make(Effect.Water, soft, new Color(0.55f, 0.8f, 1f), 0.11f, 2.2f, 1.5f, 10);
        Make(Effect.Dust, soft, new Color(0.82f, 0.75f, 0.6f), 0.55f, 2.6f, 0.08f, 16);
        Make(Effect.Sparkle, glow, new Color(1f, 0.88f, 0.4f), 0.14f, 1.6f, -0.25f, 12);
        Make(Effect.Juice, soft, new Color(0.95f, 0.45f, 0.18f), 0.13f, 3f, 1.4f, 10);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Make(Effect kind, Material material, Color colour, float size, float speed, float gravity, int count)
    {
        var system = ParticleRecipes.Create(kind + " burst", transform, Vector3.zero);
        ParticleRecipes.Spray(system, material, colour, size, speed, gravity, count);
        system.Play();
        systems[(int)kind] = system;
        counts[(int)kind] = count;
    }

    // A burst at a point. scale multiplies how many particles fly (2 for a big moment, 0.5 for a small one).
    public static void Play(Effect kind, Vector3 position, float scale = 1f)
    {
        if (instance == null || instance.systems == null) return;
        ParticleSystem system = instance.systems[(int)kind];
        if (system == null) return;
        system.transform.position = position;
        ParticleRecipes.Burst(system, Mathf.Max(1, Mathf.RoundToInt(instance.counts[(int)kind] * scale)));
    }

    public void Configure(Material glowMaterial, Material softMaterial)
    {
        glow = glowMaterial;
        soft = softMaterial;
    }
}
