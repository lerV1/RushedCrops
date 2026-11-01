using UnityEngine;

// Lets hits push this object away. MeleeAttack calls Push with a velocity.
// The push starts at full speed and slows to nothing over Duration.
//
// While IsActive is true, this component sets the Rigidbody's sideways velocity.
// A movement script on the same object must leave the velocity alone until IsActive
// is false again, or it would overwrite the push on the next physics step:
//   if (knockback.IsActive) return;   // at the top of the mover's FixedUpdate
// PracticeDummy does this.
[RequireComponent(typeof(Rigidbody))]
public class Knockback : MonoBehaviour
{
    [Tooltip("How long, in seconds, a push lasts before the object moves on its own again.")]
    [SerializeField, Min(0.01f)] private float duration = 0.25f;

    private Rigidbody body;
    private Vector3 pushVelocity;
    private float timeLeft;

    // True while a push is moving this object.
    public bool IsActive => timeLeft > 0f;

    // The push's speed right now: full speed at the start, zero at the end.
    public Vector3 CurrentVelocity => IsActive ? pushVelocity * (timeLeft / duration) : Vector3.zero;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body.isKinematic)
        {
            Debug.LogWarning($"{name} has a kinematic Rigidbody, so Knockback can't move it.", this);
        }
    }

    // Starts a push. Only the sideways part is used, so a hit never launches the object into the air.
    // A new push replaces one that is still going.
    public void Push(Vector3 velocity)
    {
        pushVelocity = new Vector3(velocity.x, 0f, velocity.z);
        timeLeft = duration;
    }

    // Ends the push straight away, for example when the object is reset.
    public void Stop()
    {
        timeLeft = 0f;
    }

    private void FixedUpdate()
    {
        if (!IsActive)
        {
            return;
        }

        // Keep the up and down speed, so gravity still works during the push.
        Vector3 push = CurrentVelocity;
        body.linearVelocity = new Vector3(push.x, body.linearVelocity.y, push.z);
        timeLeft = Mathf.Max(timeLeft - Time.fixedDeltaTime, 0f);
    }
}
