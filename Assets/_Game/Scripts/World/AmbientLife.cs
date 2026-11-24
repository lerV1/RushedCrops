using UnityEngine;

// Little things drifting in the air round the player: pollen and seed fluff by day, fireflies at night.
// Both emitters follow the camera, so the whole valley feels alive without filling it with particles.
public class AmbientLife : MonoBehaviour
{
    [SerializeField] private ParticleSystem dayMotes;
    [SerializeField] private ParticleSystem fireflies;
    [SerializeField, Min(0f)] private float dayRate = 6f;
    [SerializeField, Min(0f)] private float nightRate = 14f;

    private EnvironmentCycle cycle;
    private Transform follow;
    private float night;

    private void Awake() => cycle = FindAnyObjectByType<EnvironmentCycle>();

    private void LateUpdate()
    {
        if (follow == null && Camera.main != null)
        {
            follow = Camera.main.transform;
        }
        if (follow != null)
        {
            transform.position = new Vector3(follow.position.x, follow.position.y - 1f, follow.position.z);
        }

        // Fade between the two over a few seconds rather than switching at once.
        bool isNight = cycle != null && cycle.IsNight;
        night = Mathf.MoveTowards(night, isNight ? 1f : 0f, Time.deltaTime / 4f);
        SetRate(dayMotes, dayRate * (1f - night));
        SetRate(fireflies, nightRate * night);
    }

    private static void SetRate(ParticleSystem system, float rate)
    {
        if (system == null)
        {
            return;
        }
        var emission = system.emission;
        emission.rateOverTime = rate;
    }

    public void Configure(ParticleSystem day, ParticleSystem night)
    {
        dayMotes = day;
        fireflies = night;
    }
}
