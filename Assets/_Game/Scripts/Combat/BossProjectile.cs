using UnityEngine;

// Something a boss spits at the player, like a tomato seed or an onion tear. It flies in an arc to where the
// player was standing when it was thrown, hurts the player if it touches them, and splats on anything else.
public class BossProjectile : MonoBehaviour
{
    [Tooltip("How fat the shot is, in metres, for hitting the player.")]
    [SerializeField, Min(0.05f)] private float radius = 0.35f;
    [Tooltip("Seconds before a shot that hit nothing disappears.")]
    [SerializeField, Min(0.5f)] private float lifetime = 4f;

    private Vector3 velocity;
    private int damage;
    private Transform shooter;
    private float age;

    // Throws it from where it is now so it lands on target after flightTime seconds.
    public void Launch(Vector3 target, float flightTime, int hitDamage, Transform owner)
    {
        damage = hitDamage;
        shooter = owner;
        // The start speed that reaches the target in flightTime while gravity pulls it down.
        velocity = (target - transform.position) / flightTime - 0.5f * Physics.gravity * flightTime;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        velocity += Physics.gravity * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;
        RaycastHit nearestHit = default;
        bool hasHit = false;
        foreach (RaycastHit hit in Physics.SphereCastAll(transform.position, radius, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (shooter != null && hit.collider.transform.IsChildOf(shooter))
            {
                continue;
            }
            if (!hasHit || hit.distance < nearestHit.distance)
            {
                nearestHit = hit;
                hasHit = true;
            }
        }

        if (hasHit)
        {
            PlayerController controller = nearestHit.collider.GetComponentInParent<PlayerController>();
            if (controller != null)
            {
                Health player = nearestHit.collider.GetComponentInParent<Health>();
                if (player != null)
                {
                    CropBoss boss = shooter != null ? shooter.GetComponentInParent<CropBoss>() : null;
                    player.TakeDamage(damage, boss != null ? boss.DisplayName : "Boss projectile");
                }
            }
            GameAudio.Play(Sfx.ShotLand, nearestHit.point);
            GameEffects.Play(Effect.Juice, nearestHit.point + Vector3.up * 0.1f, 0.6f);
            Destroy(gameObject);
            return;
        }
        transform.position += step;
        if (velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }
    }
}
