using System;
using System.Collections.Generic;
using UnityEngine;

// The one shared store of resources: coins, seeds, harvested crops, wood and stone.
// Farming, the shop and barn repairs should all hold a reference to this component
// (drag it into their Inspector field) and change counts only through the methods below.
//
// Every method that changes a count checks its input first and returns false
// if the change is not allowed, so a count can never go below zero.
//
// DefaultExecutionOrder(-100) runs this Awake before other scripts,
// so the starting amounts are already in place when they first read them.
[DefaultExecutionOrder(-100)]
public class Inventory : MonoBehaviour
{
    [Header("Starting Amounts")]
    [SerializeField, Min(0)] private int startingCoins = 20;
    [SerializeField, Min(0)] private int startingCarrotSeeds = 5;
    [SerializeField, Min(0)] private int startingTomatoSeeds = 5;
    [SerializeField, Min(0)] private int startingCarrots = 0;
    [SerializeField, Min(0)] private int startingTomatoes = 0;
    [SerializeField, Min(0)] private int startingWood = 0;
    [SerializeField, Min(0)] private int startingStone = 0;

    private static readonly int ResourceTypeCount = Enum.GetValues(typeof(ResourceType)).Length;

    // One slot per resource, indexed by (int)ResourceType. counts[(int)ResourceType.Wood] is the wood count.
    private readonly int[] counts = new int[ResourceTypeCount];

    // Raised after any count changes. A HUD can listen to this instead of checking every frame.
    public event Action Changed;

    private void Awake()
    {
        ResetToStartingAmounts();
    }

    /// <summary>Sets every count back to its starting amount from the Inspector.</summary>
    public void ResetToStartingAmounts()
    {
        counts[(int)ResourceType.Coins] = startingCoins;
        counts[(int)ResourceType.CarrotSeeds] = startingCarrotSeeds;
        counts[(int)ResourceType.TomatoSeeds] = startingTomatoSeeds;
        counts[(int)ResourceType.Carrots] = startingCarrots;
        counts[(int)ResourceType.Tomatoes] = startingTomatoes;
        counts[(int)ResourceType.Wood] = startingWood;
        counts[(int)ResourceType.Stone] = startingStone;
        Changed?.Invoke();
    }

    /// <summary>How many of this resource the player has right now.</summary>
    public int GetCount(ResourceType type) => counts[(int)type];

    /// <summary>
    /// Adds a positive amount. Returns false and changes nothing for zero, negative
    /// or too-large amounts. Example: inventory.Add(ResourceType.Wood, 3);
    /// </summary>
    public bool Add(ResourceType type, int amount)
    {
        if (!IsValidAmount(amount))
        {
            return false;
        }

        // Stops the count wrapping round to a negative number if it ever got huge.
        if (counts[(int)type] > int.MaxValue - amount)
        {
            Debug.LogWarning($"Can't add {amount} {type}: the count would be too large.", this);
            return false;
        }

        counts[(int)type] += amount;
        Changed?.Invoke();
        return true;
    }

    /// <summary>True when the player has at least this many. Invalid amounts return false.</summary>
    public bool CanAfford(ResourceType type, int amount)
    {
        return IsValidAmount(amount) && counts[(int)type] >= amount;
    }

    /// <summary>True when the player has everything in the cost list at once.</summary>
    public bool CanAfford(IReadOnlyList<ResourceAmount> cost)
    {
        long[] totals = TotalCost(cost);
        if (totals == null)
        {
            return false;
        }

        for (int i = 0; i < ResourceTypeCount; i++)
        {
            if (counts[i] < totals[i])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Spends one resource. Returns false and changes nothing if the amount is
    /// invalid or the player has too few. Example: if (inventory.TrySpend(ResourceType.Coins, 5)) { ... }
    /// </summary>
    public bool TrySpend(ResourceType type, int amount)
    {
        if (!CanAfford(type, amount))
        {
            return false;
        }

        counts[(int)type] -= amount;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Spends a cost made of several resources, such as 10 Wood and 5 Stone.
    /// The whole cost is checked before anything is taken, so either every
    /// resource is spent or none of them are.
    /// </summary>
    public bool TrySpend(IReadOnlyList<ResourceAmount> cost)
    {
        // Step 1: check everything. Nothing has been changed yet, so returning here is safe.
        if (!CanAfford(cost))
        {
            return false;
        }

        // Step 2: we know the player can pay for all of it, so take every line.
        foreach (ResourceAmount line in cost)
        {
            counts[(int)line.type] -= line.amount;
        }
        Changed?.Invoke();
        return true;
    }

    // Adds up a cost list per resource, so "3 Wood" listed twice counts as 6 Wood.
    // Returns null when the list is empty or any line has an invalid amount.
    // long (a bigger whole number) is used so very large costs can't overflow while adding.
    private long[] TotalCost(IReadOnlyList<ResourceAmount> cost)
    {
        if (cost == null || cost.Count == 0)
        {
            Debug.LogWarning("A cost needs at least one resource.", this);
            return null;
        }

        long[] totals = new long[ResourceTypeCount];
        foreach (ResourceAmount line in cost)
        {
            if (!IsValidAmount(line.amount))
            {
                return null;
            }
            totals[(int)line.type] += line.amount;
        }
        return totals;
    }

    // Amounts must be whole numbers above zero. Adding or spending 0 or -5 is a bug in the caller.
    private bool IsValidAmount(int amount)
    {
        if (amount > 0)
        {
            return true;
        }

        Debug.LogWarning($"Invalid resource amount {amount}. Amounts must be greater than zero.", this);
        return false;
    }
}
