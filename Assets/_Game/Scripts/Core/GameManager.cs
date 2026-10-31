using System;
using UnityEngine;

// Holds the game state the other systems share: health, the selected crop, barn repairs and
// the boss. Resource counts (coins, seeds, crops, wood, stone) live in Inventory instead.
// Other scripts change this state through the methods below and listen to the events to update the HUD.
//
// DefaultExecutionOrder(-100) makes Unity run this Awake before the farming and combat
// scripts, so health and progress are already set when they first read them.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Health and Respawn")]
    [SerializeField, Min(1)] private int maxHealth = 5;
    [Tooltip("The player's Rigidbody. It is moved back to the checkpoint on respawn.")]
    [SerializeField] private Rigidbody player;
    [Tooltip("First checkpoint. If empty, the player's starting position is used.")]
    [SerializeField] private Transform respawnPoint;

    [Header("Barn")]
    [SerializeField, Min(1)] private int barnRepairStages = 3;

    private Vector3 checkpoint;

    public int Health { get; private set; }
    public int MaxHealth => maxHealth;
    public CropType SelectedCrop { get; private set; }
    public int BarnRepairs { get; private set; }
    public int BarnRepairStages => barnRepairStages;
    public bool IsBarnRepaired => BarnRepairs >= barnRepairStages;
    public bool IsBossDefeated { get; private set; }
    public bool HasWon { get; private set; }

    public event Action SelectedCropChanged;
    // Current health, then max health.
    public event Action<int, int> HealthChanged;
    public event Action PlayerRespawned;
    // Number of repairs finished so far.
    public event Action<int> BarnRepaired;
    public event Action BossDefeated;
    public event Action VictoryReached;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("A second GameManager was removed. Keep only one in the scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResetState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Puts health and progress back to how a new game starts.
    public void ResetState()
    {
        Health = maxHealth;
        SelectedCrop = CropType.Carrot;
        BarnRepairs = 0;
        IsBossDefeated = false;
        HasWon = false;

        if (respawnPoint != null)
        {
            checkpoint = respawnPoint.position;
        }
        else if (player != null)
        {
            checkpoint = player.position;
        }

        Debug.Log($"Game ready: {Health} health.");
    }

    // ---------- Crop selection ----------

    public void SelectCrop(CropType type)
    {
        SelectedCrop = type;
        SelectedCropChanged?.Invoke();
    }

    // ---------- Health and respawn ----------

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || HasWon)
        {
            return;
        }

        Health = Mathf.Max(Health - amount, 0);
        HealthChanged?.Invoke(Health, maxHealth);

        if (Health == 0)
        {
            Respawn();
        }
    }

    // Changes where the player comes back after losing all health.
    public void SetCheckpoint(Vector3 position)
    {
        checkpoint = position;
    }

    public void Respawn()
    {
        Health = maxHealth;

        if (player != null)
        {
            player.position = checkpoint;
            player.linearVelocity = Vector3.zero;
            player.angularVelocity = Vector3.zero;
        }

        HealthChanged?.Invoke(Health, maxHealth);
        PlayerRespawned?.Invoke();
        Debug.Log("Player respawned at the checkpoint.");
    }

    // ---------- Barn, boss and victory ----------

    public void RecordBarnRepair()
    {
        if (IsBarnRepaired)
        {
            return;
        }

        BarnRepairs++;
        Debug.Log($"Barn repair {BarnRepairs}/{barnRepairStages} complete.");
        BarnRepaired?.Invoke(BarnRepairs);
        CheckForVictory();
    }

    public void RecordBossDefeat()
    {
        if (IsBossDefeated)
        {
            return;
        }

        IsBossDefeated = true;
        Debug.Log("Boss defeated. The next region is open.");
        BossDefeated?.Invoke();
        CheckForVictory();
    }

    // The prototype ends once the boss is beaten and every barn repair is done.
    // The final repair needs rare materials from the boss region, so the boss always comes first.
    private void CheckForVictory()
    {
        if (!HasWon && IsBossDefeated && IsBarnRepaired)
        {
            TriggerVictory();
        }
    }

    public void TriggerVictory()
    {
        if (HasWon)
        {
            return;
        }

        HasWon = true;
        Debug.Log("Victory! The barn is rebuilt.");
        VictoryReached?.Invoke();
    }
}
