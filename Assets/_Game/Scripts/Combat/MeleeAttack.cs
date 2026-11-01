using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One melee swing for the weapon in hand. PlayerTools calls TryAttack when the player
// clicks with the hatchet selected. A swing has three parts:
//   windup:   the arm swings; nothing can be hit yet.
//   active:   a box in front of the camera hurts what is inside it, each target once.
//   recovery: the arm comes back. The next swing waits until this is over.
// This component only finds targets and deals damage through their Health.
// The flash (HitFlash) and the push (Knockback) are separate components on the target.
//
// Example:
//   if (melee.TryAttack()) hands.PlaySwing();
//   melee.HitLanded += target => Debug.Log($"Hit {target.name}");
public class MeleeAttack : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera the player looks through. The hitbox sits in front of it.")]
    [SerializeField] private Transform viewPoint;

    [Header("Damage")]
    [SerializeField, Min(1)] private int damage = 25;
    [Tooltip("How fast a hit pushes the target away, in metres per second. 0 means no push.")]
    [SerializeField, Min(0f)] private float knockbackStrength = 6f;

    [Header("Hitbox")]
    [Tooltip("How far in front of the camera the hitbox reaches, in metres.")]
    [SerializeField, Min(0.1f)] private float reach = 2f;
    [Tooltip("Width and height of the hitbox, in metres.")]
    [SerializeField] private Vector2 hitboxSize = new Vector2(1f, 1.2f);
    [Tooltip("Layers the hitbox can hit.")]
    [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;
    [Tooltip("Layers that stop a swing, like walls and fences, so nothing is hit through them.")]
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;

    [Header("Timing (seconds)")]
    [Tooltip("From the click until the weapon can hit. 0.3 matches the moment the swing animation strikes.")]
    [SerializeField, Min(0f)] private float windup = 0.3f;
    [Tooltip("How long the weapon can hit.")]
    [SerializeField, Min(0.01f)] private float activeTime = 0.15f;
    [Tooltip("After the hit window, before the next swing can start.")]
    [SerializeField, Min(0f)] private float recovery = 0.35f;

    // Everything already hit by this swing. A target with several colliders is still one Health.
    private readonly HashSet<Health> hitThisSwing = new HashSet<Health>();

    // True from the click until recovery ends.
    public bool IsBusy { get; private set; }

    public int Damage => damage;

    // Raised after a target takes damage from a swing.
    public event Action<Health> HitLanded;

    private void OnDisable()
    {
        // A swing stops halfway if the player is disabled, so it must not stay busy forever.
        StopAllCoroutines();
        IsBusy = false;
    }

    // Starts a swing. Returns false if the last swing has not finished yet.
    public bool TryAttack()
    {
        if (IsBusy)
        {
            return false;
        }
        if (viewPoint == null)
        {
            Debug.LogError("MeleeAttack needs the camera assigned as its View Point.", this);
            return false;
        }

        StartCoroutine(Swing());
        return true;
    }

    private IEnumerator Swing()
    {
        IsBusy = true;
        hitThisSwing.Clear();
        // "finally" runs even if a script reacting to the hit throws an error,
        // so one broken target can't leave the weapon unable to swing again.
        try
        {
            yield return new WaitForSeconds(windup);

            // Check every physics step of the hit window, so a target moving in still gets hit.
            float end = Time.time + activeTime;
            do
            {
                CheckHitbox();
                yield return new WaitForFixedUpdate();
            }
            while (Time.time < end);

            yield return new WaitForSeconds(recovery);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Looks in the hitbox once and hurts every target not hit yet this swing.
    // Returns how many targets were hit. The swing calls this during its hit window.
    public int CheckHitbox()
    {
        Vector3 halfSize = new Vector3(hitboxSize.x, hitboxSize.y, reach) * 0.5f;
        Vector3 center = viewPoint.position + viewPoint.forward * (reach * 0.5f);
        // Triggers are ignored: pickups, checkpoints and hazards are not things to hit.
        Collider[] found = Physics.OverlapBox(center, halfSize, viewPoint.rotation, hitLayers, QueryTriggerInteraction.Ignore);

        int hits = 0;
        foreach (Collider part in found)
        {
            // Skip the attacker's own body.
            if (part.transform.IsChildOf(transform))
            {
                continue;
            }

            // Only things with Health can be hit. The collider may be on a child, so look up the parents.
            Health target = part.GetComponentInParent<Health>();
            if (target == null || target.IsDead || hitThisSwing.Contains(target))
            {
                continue;
            }

            if (IsBlocked(part, target))
            {
                continue;
            }

            hitThisSwing.Add(target);
            Hit(target);
            hits++;
        }
        return hits;
    }

    // True when something solid, like a wall, stands between the camera and this part of the target.
    private bool IsBlocked(Collider part, Health target)
    {
        Vector3 from = viewPoint.position;
        // ClosestPoint only works on simple and convex colliders; use the middle of anything else.
        bool simple = !(part is MeshCollider mesh) || mesh.convex;
        Vector3 to = simple ? part.ClosestPoint(from) : part.bounds.center;

        if (!Physics.Linecast(from, to, out RaycastHit hit, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }
        // Reaching the target itself, or passing through the attacker, is not blocked.
        Transform hitObject = hit.collider.transform;
        return !hitObject.IsChildOf(target.transform) && !hitObject.IsChildOf(transform);
    }

    private void Hit(Health target)
    {
        target.TakeDamage(damage);

        // The push goes away from the attacker, along the ground.
        var knockback = target.GetComponent<Knockback>();
        if (knockback != null && knockbackStrength > 0f)
        {
            Vector3 away = target.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = new Vector3(viewPoint.forward.x, 0f, viewPoint.forward.z);
            }
            knockback.Push(away.normalized * knockbackStrength);
        }

        HitLanded?.Invoke(target);
    }

    // Draws the hitbox in the Scene view while this object is selected.
    private void OnDrawGizmosSelected()
    {
        if (viewPoint == null)
        {
            return;
        }
        Vector3 center = viewPoint.position + viewPoint.forward * (reach * 0.5f);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(center, viewPoint.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(hitboxSize.x, hitboxSize.y, reach));
    }
}
