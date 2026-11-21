using System;
using UnityEngine;

// Shared game progress: barn repairs, the bosses and victory. Items live in Inventory, health in Health.
// Runs before other scripts so the progress is set when they first read it.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Barn")]
    [SerializeField, Min(1)] private int barnRepairStages = 3;

    [Header("Bosses")]
    [Tooltip("How many bosses there are to beat: the carrot, the tomato and the onion.")]
    [SerializeField, Min(1)] private int bossCount = 3;

    public int BarnRepairs { get; private set; }
    public int BarnRepairStages => barnRepairStages;
    public bool IsBarnRepaired => BarnRepairs >= barnRepairStages;
    public int BossesBeaten { get; private set; }
    public int BossCount => bossCount;
    // True once any boss is beaten (the barn's last repair waits for this).
    public bool IsBossDefeated => BossesBeaten > 0;
    public bool AreAllBossesBeaten => BossesBeaten >= bossCount;
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
        BossesBeaten = 0;
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
        if (AreAllBossesBeaten)
        {
            return;
        }

        BossesBeaten++;
        Debug.Log($"Boss defeated ({BossesBeaten}/{bossCount}).");
        BossDefeated?.Invoke();
        CheckForVictory();
    }

    // The game is won once every boss is beaten and every barn repair is done.
    private void CheckForVictory()
    {
        if (!HasWon && AreAllBossesBeaten && IsBarnRepaired)
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
