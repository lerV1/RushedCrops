using UnityEngine;

// Small camera movements that make walking and fighting feel physical: a gentle bob in step with the
// player's stride, a dip on landing from a jump or fall, and a short shake when the player is hurt.
// Sits on the camera; the look script turns the camera's parent, so the two never fight.
public class CameraFeel : MonoBehaviour
{
    [SerializeField] private Rigidbody body;
    [SerializeField] private Health health;

    [Header("Walking bob")]
    [SerializeField, Min(0f)] private float bobHeight = 0.045f;
    [SerializeField, Min(0f)] private float bobSway = 0.025f;
    [Tooltip("Steps per metre walked.")]
    [SerializeField, Min(0.01f)] private float stepsPerMetre = 0.55f;

    [Header("Landing")]
    [SerializeField, Min(0f)] private float landingDip = 0.12f;

    [Header("Hurt")]
    [SerializeField, Min(0f)] private float shakeAngle = 2.2f;
    [SerializeField, Min(0.01f)] private float shakeTime = 0.3f;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private float stride, bobWeight, dip, dipVelocity, shakeLeft, fallSpeed;
    private bool wasGrounded = true;

    public bool Grounded { get; private set; }
    public event System.Action<float> Landed;   // how fast the player was falling, in m/s
    public event System.Action Stepped;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (body == null || dt <= 0f) return;

        Vector3 flat = body.linearVelocity;
        flat.y = 0f;
        float speed = flat.magnitude;
        Grounded = Physics.Raycast(body.position + Vector3.up * 0.1f, Vector3.down, 1.35f, 1 << 6, QueryTriggerInteraction.Ignore);

        // The bob follows distance walked, so sprinting bobs faster, and fades in and out with movement.
        float before = stride;
        if (Grounded) stride += speed * dt * stepsPerMetre;
        if (Mathf.Floor(stride) != Mathf.Floor(before)) Stepped?.Invoke();
        bobWeight = Mathf.MoveTowards(bobWeight, Grounded && speed > 0.5f ? 1f : 0f, dt * 4f);
        float phase = stride * Mathf.PI * 2f;
        Vector3 bob = new Vector3(Mathf.Sin(phase * 0.5f) * bobSway, -Mathf.Abs(Mathf.Sin(phase * 0.5f)) * bobHeight, 0f) * bobWeight;

        // Landing: the harder the fall, the deeper the dip, sprung back up.
        if (!Grounded) fallSpeed = Mathf.Max(fallSpeed, -body.linearVelocity.y);
        if (Grounded && !wasGrounded && fallSpeed > 2f)
        {
            dipVelocity -= Mathf.Clamp01(fallSpeed / 10f) * landingDip * 14f;
            Landed?.Invoke(fallSpeed);
        }
        if (Grounded) fallSpeed = 0f;
        wasGrounded = Grounded;
        dipVelocity += (-dip * 120f - dipVelocity * 14f) * dt;
        dip += dipVelocity * dt;

        // Hurt shake: a quick random wobble that dies away.
        Quaternion shake = Quaternion.identity;
        if (shakeLeft > 0f)
        {
            shakeLeft -= dt;
            float strength = shakeAngle * Mathf.Clamp01(shakeLeft / shakeTime);
            float t = Time.time * 40f;
            shake = Quaternion.Euler((Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * strength, (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * strength, (Mathf.PerlinNoise(t, t) - 0.5f) * strength);
        }

        transform.localPosition = restPosition + bob + Vector3.up * dip;
        transform.localRotation = restRotation * shake;
    }

    private void OnDamaged(int amount) => shakeLeft = shakeTime;

    public void Configure(Rigidbody playerBody, Health playerHealth)
    {
        body = playerBody;
        health = playerHealth;
    }
}
