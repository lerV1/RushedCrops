using UnityEngine;

// Lets hits push this object away; the push slows to nothing over Duration.
// A mover on the same object must leave the velocity alone while IsActive is true.
[RequireComponent(typeof(Rigidbody))]
public class Knockback : MonoBehaviour
{
    [Tooltip("How long, in seconds, a push lasts before the object moves on its own again.")]
    [SerializeField, Min(0.01f)] private float duration = 0.25f;

    private Rigidbody body;
    private Vector3 pushVelocity;
    private float timeLeft;

    public bool IsActive => timeLeft > 0f;

    public Vector3 CurrentVelocity => IsActive ? pushVelocity * (timeLeft / duration) : Vector3.zero;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body.isKinematic)
        {
            Debug.LogWarning($"{name} has a kinematic Rigidbody, so Knockback can't move it.", this);
        }
    }

    // Only the sideways part is used, so a hit never launches the object into the air.
    public void Push(Vector3 velocity)
    {
        pushVelocity = new Vector3(velocity.x, 0f, velocity.z);
        timeLeft = duration;
    }

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
