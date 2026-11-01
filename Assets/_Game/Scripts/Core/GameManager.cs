using System;
using UnityEngine;

// Holds the game progress the other systems share: the selected crop, barn repairs and
// the boss. Resource counts (coins, seeds, crops, wood, stone) live in Inventory, and
// the player's health lives in their Health component (respawning is RespawnController's job).
// Other scripts change this state through the methods below and listen to the events to update the HUD.
//
// DefaultExecutionOrder(-100) makes Unity run this Awake before the farming and combat
// scripts, so progress is already set when they first read it.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Barn")]
    [SerializeField, Min(1)] private int barnRepairStages = 3;

    public CropType SelectedCrop { get; private set; }
    public int BarnRepairs { get; private set; }
    public int BarnRepairStages => barnRepairStages;
    public bool IsBarnRepaired => BarnRepairs >= barnRepairStages;
    public bool IsBossDefeated { get; private set; }
    public bool HasWon { get; private set; }

    public event Action SelectedCropChanged;
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

    // Puts progress back to how a new game starts.
    public void ResetState()
    {
        SelectedCrop = CropType.Carrot;
        BarnRepairs = 0;
        IsBossDefeated = false;
        HasWon = false;
    }

    // ---------- Crop selection ----------

    public void SelectCrop(CropType type)
    {
        SelectedCrop = type;
        SelectedCropChanged?.Invoke();
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
