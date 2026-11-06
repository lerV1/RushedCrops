using UnityEngine;

// Hurts anything with Health that enters this trigger, like spikes or thorns.
[RequireComponent(typeof(Collider))]
public class DamageDealer : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 10;

    public int Damage => damage;

    // Makes the collider a trigger when the component is added.
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
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
