using UnityEngine;

// Makes a fire's light flicker and glow brighter at night, when it matters.
public class FireFlicker : MonoBehaviour
{
    [SerializeField] private Light glow;
    [SerializeField, Min(0f)] private float dayIntensity = 0.6f;
    [SerializeField, Min(0f)] private float nightIntensity = 3.2f;
    [SerializeField, Range(0f, 1f)] private float flicker = 0.3f;

    [SerializeField] private EnvironmentCycle cycle;
    private float seed;

    private void Awake()
    {
        seed = Random.value * 100f;
    }

    private void Update()
    {
        if (glow == null)
        {
            return;
        }
        float bright = cycle != null && cycle.IsNight ? nightIntensity : dayIntensity;
        float noise = Mathf.PerlinNoise(seed, Time.time * 6f) * 0.7f + Mathf.PerlinNoise(seed + 7f, Time.time * 17f) * 0.3f;
        glow.intensity = bright * (1f - flicker + noise * flicker * 2f);
    }

    public void Configure(Light light) => glow = light;
}
