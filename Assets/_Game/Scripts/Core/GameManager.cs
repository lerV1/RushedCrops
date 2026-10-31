using System;
using UnityEngine;

// Holds the game state the other systems share: coins, seeds, harvested crops, materials,
// health, barn repairs and the boss. Other scripts change it through the methods below and
// listen to the events to update the HUD.
//
// DefaultExecutionOrder(-100) makes Unity run this Awake before the farming and combat
// scripts, so the starting values are already set when they first read them.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Provisional values. Seeds run out, so the player has to use the merchant to keep farming.
    [Header("Starting Resources")]
    [SerializeField, Min(0)] private int startingCoins = 0;
    [SerializeField, Min(0)] private int startingCarrotSeeds = 4;
    [SerializeField, Min(0)] private int startingTomatoSeeds = 4;
    [SerializeField, Min(0)] private int startingWood = 0;
    [SerializeField, Min(0)] private int startingRareMaterials = 0;

    [Header("Health and Respawn")]
    [SerializeField, Min(1)] private int maxHealth = 5;
    [Tooltip("The player's Rigidbody. It is moved back to the checkpoint on respawn.")]
    [SerializeField] private Rigidbody player;
    [Tooltip("First checkpoint. If empty, the player's starting position is used.")]
    [SerializeField] private Transform respawnPoint;

    [Header("Barn")]
    [SerializeField, Min(1)] private int barnRepairStages = 3;

    private static readonly int CropTypeCount = Enum.GetValues(typeof(CropType)).Length;

    // Seeds and harvested crops are counted per crop type, indexed by (int)CropType.
    private readonly int[] seeds = new int[CropTypeCount];
    private readonly int[] harvested = new int[CropTypeCount];
    private Vector3 checkpoint;

    public int Coins { get; private set; }
    public int Wood { get; private set; }
    public int RareMaterials { get; private set; }
    public int Health { get; private set; }
    public int MaxHealth => maxHealth;
    public CropType SelectedCrop { get; private set; }
    public int BarnRepairs { get; private set; }
    public int BarnRepairStages => barnRepairStages;
    public bool IsBarnRepaired => BarnRepairs >= barnRepairStages;
    public bool IsBossDefeated { get; private set; }
    public bool HasWon { get; private set; }

    // Raised whenever coins, seeds, crops, materials or the selected crop change.
    public event Action ResourcesChanged;
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

    // Puts every value back to its starting amount.
    public void ResetState()
    {
        Coins = startingCoins;
        seeds[(int)CropType.Carrot] = startingCarrotSeeds;
        seeds[(int)CropType.Tomato] = startingTomatoSeeds;
        Array.Clear(harvested, 0, harvested.Length);
        Wood = startingWood;
        RareMaterials = startingRareMaterials;
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

        Debug.Log($"Game ready: {Coins} coins, {GetSeeds(CropType.Carrot)} carrot seeds, " +
                  $"{GetSeeds(CropType.Tomato)} tomato seeds, {Health} health.");
    }

    // ---------- Coins ----------

    public void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Coins += amount;
        ResourcesChanged?.Invoke();
    }

    // Returns false and spends nothing when the player can't afford it.
    public bool SpendCoins(int amount)
    {
        if (amount < 0 || amount > Coins)
        {
            return false;
        }

        Coins -= amount;
        ResourcesChanged?.Invoke();
        return true;
    }

    // ---------- Seeds and planting ----------

    public void SelectCrop(CropType type)
    {
        SelectedCrop = type;
        ResourcesChanged?.Invoke();
    }

    public int GetSeeds(CropType type) => seeds[(int)type];

    public void AddSeeds(CropType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        seeds[(int)type] += amount;
        ResourcesChanged?.Invoke();
    }

    // Uses one seed of the selected crop. Returns false when none are left.
    public bool TryPlantSeed() => TryPlantSeed(SelectedCrop);

    public bool TryPlantSeed(CropType type)
    {
        if (seeds[(int)type] <= 0)
        {
            Debug.Log($"No {type} seeds left. Buy more from the merchant.");
            return false;
        }

        seeds[(int)type]--;
        ResourcesChanged?.Invoke();
        return true;
    }

    // ---------- Harvest and selling ----------

    public int GetHarvested(CropType type) => harvested[(int)type];

    public void AddHarvestedCrop(CropType type, int amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        harvested[(int)type] += amount;
        ResourcesChanged?.Invoke();
    }

    // Sells up to 'amount' crops at the merchant's price and returns the coins earned.
    // The merchant decides the price, so this works with its changing offers.
    public int SellCrops(CropType type, int amount, int pricePerCrop)
    {
        int sold = Mathf.Clamp(amount, 0, harvested[(int)type]);
        if (sold == 0 || pricePerCrop < 0)
        {
            return 0;
        }

        int earned = sold * pricePerCrop;
        harvested[(int)type] -= sold;
        Coins += earned;
        ResourcesChanged?.Invoke();
        Debug.Log($"Sold {sold} {type} for {earned} coins.");
        return earned;
    }

    // ---------- Materials ----------

    public void AddWood(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Wood += amount;
        ResourcesChanged?.Invoke();
    }

    public void AddRareMaterials(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        RareMaterials += amount;
        ResourcesChanged?.Invoke();
    }

    // Spends materials for a repair only when both amounts are available.
    public bool TrySpendMaterials(int wood, int rareMaterials)
    {
        if (wood < 0 || rareMaterials < 0 || wood > Wood || rareMaterials > RareMaterials)
        {
            return false;
        }

        Wood -= wood;
        RareMaterials -= rareMaterials;
        ResourcesChanged?.Invoke();
        return true;
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
