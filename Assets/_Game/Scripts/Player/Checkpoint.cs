using UnityEngine;

// A trigger zone that becomes the player's respawn point when they walk through it.
// Put it on an object with a collider marked Is Trigger.
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Tooltip("Where the player reappears. If empty, this object's position is used.")]
    [SerializeField] private Transform spawnPoint;

    public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

    // Unity calls Reset when the component is first added, so the collider starts as a trigger.
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        RespawnController player = other.GetComponentInParent<RespawnController>();
        if (player == null || !player.CompareTag(PlayerTag))
        {
            return;
        }

        if (player.CurrentCheckpoint != SpawnPoint)
        {
            player.SetCheckpoint(SpawnPoint);
            Debug.Log($"Checkpoint reached: {name}.", this);
        }
    }
}
