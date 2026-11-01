using System;
using UnityEngine;

// Brings the player back at the latest checkpoint when their Health runs out.
// Health does the counting; this component only decides what happens on death.
// Checkpoint triggers call SetCheckpoint as the player walks through them.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
public class RespawnController : MonoBehaviour
{
    [Tooltip("Where the player comes back before reaching any checkpoint. If empty, the player's starting position is used.")]
    [SerializeField] private Transform startingCheckpoint;

    private Health health;
    private Rigidbody rb;
    private Vector3 startPosition;

    public Transform CurrentCheckpoint { get; private set; }

    public event Action Respawned;

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        CurrentCheckpoint = startingCheckpoint;
    }

    private void OnEnable()
    {
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Died -= OnDied;
    }

    // The next respawn happens here.
    public void SetCheckpoint(Transform checkpoint)
    {
        if (checkpoint == null)
        {
            Debug.LogWarning("SetCheckpoint was given no checkpoint, so the old one is kept.", this);
            return;
        }

        CurrentCheckpoint = checkpoint;
    }

    public void Respawn()
    {
        Vector3 position = CurrentCheckpoint != null ? CurrentCheckpoint.position : startPosition;

        // Stop all movement so the player doesn't keep falling or sliding after the move.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        // Move both the body and the transform, so the camera snaps there this frame
        // instead of the interpolated body gliding across the map.
        rb.position = position;
        transform.position = position;

        health.RestoreFullHealth();
        Debug.Log("Player respawned at the checkpoint.", this);
        Respawned?.Invoke();
    }

    private void OnDied()
    {
        Respawn();
    }
}
