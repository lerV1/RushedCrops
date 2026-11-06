using System;
using UnityEngine;

// Shared game progress: barn repairs, the boss and victory. Items live in Inventory, health in Health.
// Runs before other scripts so the progress is set when they first read it.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Barn")]
    [SerializeField, Min(1)] private int barnRepairStages = 3;

    public int BarnRepairs { get; private set; }
    public int BarnRepairStages => barnRepairStages;
    public bool IsBarnRepaired => BarnRepairs >= barnRepairStages;
    public bool IsBossDefeated { get; private set; }
    public bool HasWon { get; private set; }

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

    public void ResetState()
    {
        BarnRepairs = 0;
        IsBossDefeated = false;
        HasWon = false;
    }

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

    // The game is won once the boss is beaten and every barn repair is done.
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
