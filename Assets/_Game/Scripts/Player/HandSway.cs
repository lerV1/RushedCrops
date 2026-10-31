using UnityEngine;

// Moves the first-person hands a little so they feel attached to a body:
// they lag behind when the view turns and bob while the player walks.
// Put it on the object that holds the hands (HandsRoot).
public class HandSway : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerLook look;
    [SerializeField] private Rigidbody body;

    [Header("Sway")]
    [Tooltip("How far the hands drift, in metres per degree-per-second of turning.")]
    [SerializeField] private float swayAmount = 0.0002f;
    [Tooltip("The furthest the hands can drift from their rest position, in metres.")]
    [SerializeField] private float maxSway = 0.04f;
    [Tooltip("How quickly the hands catch up with the view. Higher is snappier.")]
    [SerializeField] private float swaySmoothing = 8f;

    [Header("Walk Bob")]
    [Tooltip("Bob cycles per second at Full Bob Speed. One cycle is two steps.")]
    [SerializeField] private float bobFrequency = 1.6f;
    [Tooltip("Size of the bob in metres.")]
    [SerializeField] private float bobAmount = 0.012f;
    [Tooltip("Ground speed (m/s) at which the bob reaches full size.")]
    [SerializeField] private float fullBobSpeed = 5f;

    private Vector3 restPosition;
    private Vector3 swayOffset;
    private float bobPhase;
    private float bobWeight;

    private void Awake()
    {
        restPosition = transform.localPosition;
    }

    // LateUpdate runs after PlayerLook.Update, so this frame's turn is already known.
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        transform.localPosition = restPosition + GetSwayOffset(dt) + GetBobOffset(dt);
    }

    private Vector3 GetSwayOffset(float dt)
    {
        // Drift opposite to the turn, as if the hands are a moment behind the head.
        Vector2 turnSpeed = look != null ? look.LookDelta / dt : Vector2.zero;
        Vector3 target = new Vector3(-turnSpeed.x, -turnSpeed.y, 0f) * swayAmount;
        target = Vector3.ClampMagnitude(target, maxSway);

        swayOffset = Vector3.Lerp(swayOffset, target, 1f - Mathf.Exp(-swaySmoothing * dt));
        return swayOffset;
    }

    private Vector3 GetBobOffset(float dt)
    {
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        float groundSpeed = new Vector2(velocity.x, velocity.z).magnitude;

        // No bob in the air: a jump or fall shouldn't look like walking.
        bool airborne = Mathf.Abs(velocity.y) > 1f;
        float walkAmount = airborne ? 0f : groundSpeed / fullBobSpeed;

        // Faster steps when sprinting, but the size stops growing at full walking speed.
        bobPhase += dt * bobFrequency * 2f * Mathf.PI * walkAmount;
        bobWeight = Mathf.Lerp(bobWeight, Mathf.Clamp01(walkAmount), 1f - Mathf.Exp(-10f * dt));

        // Side to side once per cycle, down and up once per step: a small figure eight.
        float x = Mathf.Sin(bobPhase) * bobAmount;
        float y = Mathf.Sin(bobPhase * 2f) * bobAmount * 0.6f;
        return new Vector3(x, y, 0f) * bobWeight;
    }
}
