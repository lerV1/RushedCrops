using System;
using System.Collections.Generic;
using UnityEngine;

// Shared game progress: barn repairs, the bosses and victory. Items live in Inventory, health in Health.
// Runs before other scripts so the progress is set when they first read it.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{

    [Header("Barn")]
    [SerializeField, Min(1)] private int barnRepairStages = 3;

    [Header("Bosses")]
    [Tooltip("How many different bosses must be beaten to win: the Carrot King, Tomato Brute and Onion Overlord, grown from their boss seeds.")]
    [SerializeField, Min(1)] private int bossCount = 3;

    public int BarnRepairs { get; private set; }
    public int BarnRepairStages => barnRepairStages;
    public bool IsBarnRepaired => BarnRepairs >= barnRepairStages;
    // The name of each boss beaten so far. Beating the same boss again (planting its seed twice) adds nothing.
    private readonly List<string> bossesBeaten = new List<string>();

    public int BossesBeaten => bossesBeaten.Count;
    public int BossCount => bossCount;
    // True once any boss is beaten, a bonus boss too (the barn's last repair waits for this).
    public bool IsBossDefeated { get; private set; }
    public bool AreAllBossesBeaten => BossesBeaten >= bossCount;
    public bool HasWon { get; private set; }

    public event Action<int> BarnRepaired;
    public event Action BossDefeated;
    public event Action VictoryReached;

    private void Awake()
    {
        ResetState();
    }

    public void ResetState()
    {
        BarnRepairs = 0;
        bossesBeaten.Clear();
        IsBossDefeated = false;
        HasWon = false;
    }

    public string[] GetSavedBosses() => bossesBeaten.ToArray();

    public void RestoreProgress(int barnRepairs, string[] savedBosses, bool bossDefeated, bool hasWon)
    {
        BarnRepairs = Mathf.Clamp(barnRepairs, 0, barnRepairStages);
        bossesBeaten.Clear();
        if (savedBosses != null)
        {
            foreach (string boss in savedBosses)
            {
                if (!string.IsNullOrEmpty(boss) && !bossesBeaten.Contains(boss))
                {
                    bossesBeaten.Add(boss);
                }
            }
        }
        IsBossDefeated = bossDefeated || bossesBeaten.Count > 0;
        HasWon = hasWon;
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

    public bool HasBeaten(string bossName) => bossesBeaten.Contains(bossName);

    // Called whenever a boss dies. Any boss unlocks the barn's last repair. A boss that counts toward winning is
    // counted once, by name; returns false if nothing was counted (a bonus boss, a repeat, or all already beaten).
    public bool RecordBossDefeat(string bossName, bool countsTowardVictory)
    {
        IsBossDefeated = true;
        if (!countsTowardVictory)
        {
            return false;
        }
        if (AreAllBossesBeaten || string.IsNullOrEmpty(bossName) || bossesBeaten.Contains(bossName))
        {
            return false;
        }

        bossesBeaten.Add(bossName);
        Debug.Log($"{bossName} defeated ({BossesBeaten}/{bossCount}).");
        BossDefeated?.Invoke();
        CheckForVictory();
        return true;
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
