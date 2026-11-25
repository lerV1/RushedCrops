using UnityEngine;

// The little bursts that make actions feel solid: wood chips off a chopped tree, stone chips off a rock,
// dirt from the hoe, water from the can, leaves from a harvest, dust from a boss's slam. One particle system
// per kind is assigned in the Inspector and reused. Call GameEffects.Play from
// anywhere; with no active GameEffects in the scene it does nothing.
public enum Effect { WoodChips, StoneChips, Dirt, Leaves, Water, Dust, Sparkle, Juice }

public class GameEffects : MonoBehaviour
{
    [Tooltip("One saved particle system per Effect, in the enum's order.")]
    [SerializeField] private ParticleSystem[] systems;
    [Tooltip("Particles emitted by a normal burst of each Effect, in the same order.")]
    [SerializeField] private int[] counts;

    private static event System.Action<Effect, Vector3, float> EffectRequested;

    private void Awake()
    {
        int kinds = System.Enum.GetValues(typeof(Effect)).Length;
        if (systems == null || counts == null || systems.Length != kinds || counts.Length != kinds)
        {
            Debug.LogError("GameEffects needs one particle system and burst count for each Effect.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < kinds; i++)
        {
            if (systems[i] == null || counts[i] <= 0)
            {
                Debug.LogError("GameEffects has a missing particle system or an invalid burst count.", this);
                enabled = false;
                return;
            }
            systems[i].Play();
        }
    }

    private void OnEnable()
    {
        EffectRequested += OnEffectRequested;
    }

    private void OnDisable()
    {
        EffectRequested -= OnEffectRequested;
    }

    // A burst at a point. scale multiplies how many particles fly (2 for a big moment, 0.5 for a small one).
    public static void Play(Effect kind, Vector3 position, float scale = 1f)
    {
        EffectRequested?.Invoke(kind, position, scale);
    }

    private void OnEffectRequested(Effect kind, Vector3 position, float scale)
    {
        ParticleSystem system = systems[(int)kind];
        if (system == null) return;
        system.transform.position = position;
        system.Emit(Mathf.Max(1, Mathf.RoundToInt(counts[(int)kind] * scale)));
    }
}
