using UnityEngine;
using UnityEngine.Rendering;

// Drives the farm's daylight. All timings and light references are Inspector-editable.
public class EnvironmentCycle : MonoBehaviour
{
    [Header("Time")]
    [SerializeField, Min(30f)] private float dayLengthSeconds = 480f;
    [SerializeField, Range(0f, 24f)] private float currentHour = 9f;
    [SerializeField] private bool cycleRunning = true;

    [Header("Lighting")]
    [SerializeField] private Light sun;
    [SerializeField] private Light moon;
    [SerializeField] private Light[] lanterns = new Light[0];
    [SerializeField, Min(0f)] private float daylightIntensity = 1.35f;
    [SerializeField, Min(0f)] private float moonlightIntensity = 0.45f;
    [SerializeField, Min(0f)] private float lanternIntensity = 3f;

    [Header("Night readability")]
    [SerializeField] private Color nightSkyAmbient = new Color(0.23f, 0.29f, 0.43f);
    [SerializeField] private Color nightHorizonAmbient = new Color(0.21f, 0.25f, 0.34f);
    [SerializeField] private Color nightGroundAmbient = new Color(0.11f, 0.15f, 0.21f);

    private WorldClock clock;
    private Material originalSkybox;
    private Material runtimeSkybox;

    public float Hour => clock != null ? clock.Hour : currentHour;
    public int Day => clock != null ? clock.Day : 1;
    public bool IsNight => Hour < 6f || Hour >= 18.5f;

    private void OnEnable()
    {
        clock = new WorldClock(currentHour);
        originalSkybox = RenderSettings.skybox;
        if (originalSkybox != null)
        {
            // Never recolour the shared sky material while the player runs the scene.
            runtimeSkybox = new Material(originalSkybox);
            RenderSettings.skybox = runtimeSkybox;
        }
        ApplyLighting();
    }

    private void Update()
    {
        if (cycleRunning) clock.Advance(Time.deltaTime, dayLengthSeconds);
        currentHour = clock.Hour;
        ApplyLighting();
    }

    private void OnDisable()
    {
        if (runtimeSkybox == null) return;
        if (RenderSettings.skybox == runtimeSkybox) RenderSettings.skybox = originalSkybox;
        Destroy(runtimeSkybox);
        runtimeSkybox = null;
    }

    private void OnValidate()
    {
        dayLengthSeconds = Mathf.Max(30f, dayLengthSeconds);
        if (!Application.isPlaying || clock == null) return;
        clock.SetHour(currentHour);
        ApplyLighting();
    }

    // Useful for scene previews and automated checks; it does not advance the day counter.
    public void SetHour(float hour)
    {
        if (clock == null) clock = new WorldClock(currentHour);
        clock.SetHour(hour);
        currentHour = clock.Hour;
        ApplyLighting();
    }

    public void SetRunning(bool running) => cycleRunning = running;

    private void ApplyLighting()
    {
        float sunAngle = (Hour - 6f) * 15f;
        float elevation = Mathf.Sin(sunAngle * Mathf.Deg2Rad);
        float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.25f, elevation));
        float highSun = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.55f, elevation));

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(sunAngle, -35f, 0f);
            sun.color = Color.Lerp(new Color(1f, 0.48f, 0.22f), new Color(1f, 0.94f, 0.81f), highSun);
            sun.intensity = daylightIntensity * daylight;
            sun.enabled = daylight > 0.01f;
            RenderSettings.sun = sun;
        }
        if (moon != null)
        {
            moon.transform.rotation = Quaternion.Euler(sunAngle + 180f, -35f, 0f);
            moon.intensity = moonlightIntensity * (1f - daylight);
            moon.enabled = daylight < 0.99f;
        }
        foreach (Light lantern in lanterns)
        {
            if (lantern == null) continue;
            lantern.enabled = IsNight;
            lantern.intensity = lanternIntensity;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(nightSkyAmbient, new Color(0.60f, 0.72f, 0.85f), daylight);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightHorizonAmbient, new Color(0.55f, 0.60f, 0.46f), daylight);
        RenderSettings.ambientGroundColor = Color.Lerp(nightGroundAmbient, new Color(0.29f, 0.33f, 0.21f), daylight);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = Mathf.Lerp(45f, 85f, daylight);
        RenderSettings.fogEndDistance = Mathf.Lerp(150f, 235f, daylight);
        RenderSettings.fogColor = Color.Lerp(new Color(0.10f, 0.17f, 0.28f), new Color(0.64f, 0.76f, 0.76f), daylight);
        if (runtimeSkybox != null)
        {
            if (runtimeSkybox.HasProperty("_Exposure")) runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(0.23f, 1.15f, daylight));
            if (runtimeSkybox.HasProperty("_SkyTint")) runtimeSkybox.SetColor("_SkyTint", Color.Lerp(new Color(0.16f, 0.23f, 0.42f), new Color(0.47f, 0.63f, 0.77f), daylight));
        }
    }
}
