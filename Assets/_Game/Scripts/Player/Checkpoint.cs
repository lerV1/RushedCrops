using UnityEngine;

// A trigger that becomes the player's respawn point when they walk through it.
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Tooltip("Where the player reappears. If empty, this object's position is used.")]
    [SerializeField] private Transform spawnPoint;

    public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

    // Makes the collider a trigger when the component is added.
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
