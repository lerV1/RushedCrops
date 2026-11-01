using UnityEngine;

// A straw training dummy for practising melee hits. Its Health takes the damage,
// HitFlash flashes it and Knockback pushes it. This script moves it back to its spot
// after a push and lets the player reset it.
// Look at it and press Interact to reset it: full health and back on its spot.
// The prompt shows its health.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Knockback))]
public class PracticeDummy : MonoBehaviour, IInteractable
{
    [Tooltip("How fast, in metres per second, the dummy slides back to its spot after a push.")]
    [SerializeField, Min(0f)] private float returnSpeed = 1.5f;
    [Tooltip("How long, in seconds, it waits after a push before sliding back.")]
    [SerializeField, Min(0f)] private float returnDelay = 0.6f;

    private Health health;
    private Rigidbody body;
    private Knockback knockback;
    private Vector3 home;
    private float waitLeft;

    public Vector3 Home => home;

    private void Awake()
    {
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody>();
        knockback = GetComponent<Knockback>();
        home = transform.position;
    }

    public bool CanInteract => true;

    public string GetPrompt() => health.IsDead
        ? "Reset dummy (knocked out)"
        : $"Reset dummy ({health.CurrentHealth}/{health.MaxHealth} HP)";

    public void Interact(PlayerInteractor player) => ResetDummy();

    // Full health, no push, and back on its spot.
    public void ResetDummy()
    {
        health.RestoreFullHealth();
        knockback.Stop();
        waitLeft = 0f;
        body.linearVelocity = Vector3.zero;
        // Move the body and the transform, so it doesn't glide back through things.
        body.position = home;
        transform.position = home;
    }

    private void FixedUpdate()
    {
        // While a push lasts, Knockback owns the velocity. Setting it here would cancel the push.
        if (knockback.IsActive)
        {
            waitLeft = returnDelay;
            return;
        }
        if (waitLeft > 0f)
        {
            waitLeft -= Time.fixedDeltaTime;
            return;
        }

        Vector3 toHome = home - body.position;
        toHome.y = 0f;
        // Slow down at the end, so it stops on the spot instead of overshooting.
        float speed = Mathf.Min(returnSpeed, toHome.magnitude / Time.fixedDeltaTime);
        Vector3 move = toHome.sqrMagnitude > 0.0001f ? toHome.normalized * speed : Vector3.zero;
        body.linearVelocity = new Vector3(move.x, body.linearVelocity.y, move.z);
    }
}
