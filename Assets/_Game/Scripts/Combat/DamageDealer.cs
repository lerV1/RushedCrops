using UnityEngine;

// Hurts anything with a Health component that enters this trigger: the player,
// enemies, animals. Use it for hazards such as spikes, fire or thorn bushes.
[RequireComponent(typeof(Collider))]
public class DamageDealer : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 10;

    public int Damage => damage;

    // Unity calls Reset when the component is first added, so the collider starts as a trigger.
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // The collider may sit on a child object, so look up the parents for the Health.
        Health target = other.GetComponentInParent<Health>();
        if (target != null)
        {
            DealDamage(target);
        }
    }

    public void DealDamage(Health target)
    {
        if (damage <= 0)
        {
            Debug.LogWarning($"{name} has damage {damage}; it must be at least 1.", this);
            return;
        }

        target.TakeDamage(damage);
    }
}
