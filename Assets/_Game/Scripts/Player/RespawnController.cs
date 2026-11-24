using System;
using UnityEngine;

// Brings the player back at the latest checkpoint when their Health runs out.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
public class RespawnController : MonoBehaviour
{
    [Tooltip("Where the player comes back before reaching any checkpoint. If empty, the player's starting position is used.")]
    [SerializeField] private Transform startingCheckpoint;

    private Health health;
    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private PlayerController movement;
    private PlayerLook look;
    private PlayerInteractor interactor;
    private PlayerTools tools;
    private MeleeAttack melee;
    private bool awaitingRespawn;

    public Transform CurrentCheckpoint { get; private set; }
    public bool IsAwaitingRespawn => awaitingRespawn;
    public event Action Respawned;
    public event Action DeathStarted;

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        CurrentCheckpoint = startingCheckpoint;
        movement = GetComponent<PlayerController>();
        look = GetComponent<PlayerLook>();
        interactor = GetComponent<PlayerInteractor>();
        tools = GetComponent<PlayerTools>();
        melee = GetComponent<MeleeAttack>();
    }

    private void OnEnable()
    {
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Died -= OnDied;
    }

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
        if (!awaitingRespawn)
        {
            return;
        }

        Vector3 position = CurrentCheckpoint != null ? CurrentCheckpoint.position : startPosition;
        Quaternion rotation = CurrentCheckpoint != null ? CurrentCheckpoint.rotation : startRotation;

        // Clear motion while dynamic, then hold the body still for the position snap.
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        // Move both the body and the transform, so the camera snaps there this frame
        // instead of the interpolated body gliding across the map.
        rb.position = position;
        rb.rotation = rotation;
        transform.position = position;
        transform.rotation = rotation;

        health.RestoreFullHealth();
        awaitingRespawn = false;
        rb.isKinematic = false;
        SetControlsEnabled(true);
        Debug.Log("Player respawned at the checkpoint.", this);
        Respawned?.Invoke();
    }

    private void OnDied()
    {
        if (awaitingRespawn)
        {
            return;
        }

        awaitingRespawn = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        SetControlsEnabled(false);
        DeathStarted?.Invoke();
    }

    private void SetControlsEnabled(bool value)
    {
        if (movement != null) movement.enabled = value;
        if (look != null) look.enabled = value;
        if (interactor != null) interactor.enabled = value;
        if (tools != null) tools.enabled = value;
        if (melee != null) melee.enabled = value;
    }
}
