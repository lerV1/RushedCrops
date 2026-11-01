using System;
using System.Collections.Generic;
using UnityEngine;

// The one shared store of the player's things: a coin wallet and a 5-slot hotbar.
// Farming, the shop and barn repairs should all hold a reference to this component
// (drag it into their Inspector field) and change counts only through the methods below.
//
// Hotbar rules:
//   - Something picked up first tops up slots that already hold the same item,
//     then goes into the first empty slot.
//   - A slot holds up to 99 of one item (Max Stack). Tools don't stack: one per slot.
//   - The selected slot (keys 1-5) is what the player holds.
//
// Every method that changes a count checks its input first and returns false
// if the change is not allowed, so a count can never go below zero and
// nothing is half added or half spent.
//
// DefaultExecutionOrder(-100) runs this Awake before other scripts,
// so the inventory is ready when they first read it.
[DefaultExecutionOrder(-100)]
public class Inventory : MonoBehaviour
{
    public const int SlotCount = 5;

    [Tooltip("Coins the player starts with.")]
    [SerializeField, Min(0)] private int startingCoins = 0;

    [Tooltip("The most of one item a hotbar slot can hold.")]
    [SerializeField, Min(1)] private int maxStack = 99;

    private readonly ResourceType[] slotItems = new ResourceType[SlotCount];
    // A count of 0 means the slot is empty; its item is then ignored.
    private readonly int[] slotCounts = new int[SlotCount];
    private int coins;

    /// <summary>The hotbar slot the player is holding, 0 to 4.</summary>
    public int SelectedSlot { get; private set; }

    public int MaxStack => maxStack;

    // Raised after any count or the selected slot changes. A HUD can listen to this instead of checking every frame.
    public event Action Changed;

    private void Awake()
    {
        ResetToStartingAmounts();
    }

    /// <summary>Empties the hotbar, selects the first slot and sets coins back to the starting amount.</summary>
    public void ResetToStartingAmounts()
    {
        Array.Clear(slotCounts, 0, SlotCount);
        coins = startingCoins;
        SelectedSlot = 0;
        Changed?.Invoke();
    }

    // ---------- Reading ----------

    /// <summary>How many of this item the player has right now, across every slot.</summary>
    public int GetCount(ResourceType type)
    {
        if (type == ResourceType.Coins)
        {
            return coins;
        }

        int total = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotCounts[i] > 0 && slotItems[i] == type)
            {
                total += slotCounts[i];
            }
        }
        return total;
    }

    public bool IsSlotEmpty(int slot) => slotCounts[slot] == 0;

    public int GetSlotCount(int slot) => slotCounts[slot];

    /// <summary>The item in a slot. Check IsSlotEmpty first: an empty slot has no item.</summary>
    public ResourceType GetSlotItem(int slot) => slotItems[slot];

    /// <summary>
    /// Gives the item in the selected slot. Returns false when that slot is empty.
    /// Example: if (inventory.TryGetSelectedItem(out ResourceType item)) { ... }
    /// </summary>
    public bool TryGetSelectedItem(out ResourceType item)
    {
        item = slotItems[SelectedSlot];
        return slotCounts[SelectedSlot] > 0;
    }

    /// <summary>How many of this item fit in one slot. Tools don't stack.</summary>
    public int MaxStackFor(ResourceType type) => ResourceTypes.IsTool(type) ? 1 : maxStack;

    // ---------- Selecting ----------

    /// <summary>Selects a hotbar slot, 0 to 4. Returns false for a slot that doesn't exist.</summary>
    public bool SelectSlot(int slot)
    {
        if (slot < 0 || slot >= SlotCount)
        {
            Debug.LogWarning($"There is no hotbar slot {slot}. Slots go from 0 to {SlotCount - 1}.", this);
            return false;
        }
        if (slot == SelectedSlot)
        {
            return true;
        }

        SelectedSlot = slot;
        Changed?.Invoke();
        return true;
    }

    // ---------- Adding ----------

    /// <summary>True when this many fit right now. Coins always fit; other items need hotbar room.</summary>
    public bool CanAdd(ResourceType type, int amount)
    {
        if (!IsValidAmount(amount))
        {
            return false;
        }
        if (type == ResourceType.Coins)
        {
            // Stops the count wrapping round to a negative number if it ever got huge.
            return coins <= int.MaxValue - amount;
        }
        return RoomFor(type) >= amount;
    }

    /// <summary>
    /// Adds a positive amount. All of it goes in or none of it does: returns false and
    /// changes nothing if the hotbar is too full or the amount is invalid.
    /// Example: inventory.Add(ResourceType.Wood, 3);
    /// </summary>
    public bool Add(ResourceType type, int amount)
    {
        if (!CanAdd(type, amount))
        {
            return false;
        }

        if (type == ResourceType.Coins)
        {
            coins += amount;
            Changed?.Invoke();
            return true;
        }

        int left = amount;
        int stackLimit = MaxStackFor(type);

        // First top up slots that already hold this item...
        for (int i = 0; i < SlotCount && left > 0; i++)
        {
            if (slotCounts[i] > 0 && slotItems[i] == type)
            {
                int moved = Mathf.Min(left, stackLimit - slotCounts[i]);
                slotCounts[i] += moved;
                left -= moved;
            }
        }

        // ...then fill empty slots, starting from the first.
        for (int i = 0; i < SlotCount && left > 0; i++)
        {
            if (slotCounts[i] == 0)
            {
                int moved = Mathf.Min(left, stackLimit);
                slotItems[i] = type;
                slotCounts[i] = moved;
                left -= moved;
            }
        }

        Changed?.Invoke();
        return true;
    }

    // How many more of this item the hotbar can take.
    private int RoomFor(ResourceType type)
    {
        int stackLimit = MaxStackFor(type);
        int room = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotCounts[i] == 0)
            {
                room += stackLimit;
            }
            else if (slotItems[i] == type)
            {
                room += Mathf.Max(0, stackLimit - slotCounts[i]);
            }
        }
        return room;
    }

    // ---------- Spending ----------

    /// <summary>True when the player has at least this many. Invalid amounts return false.</summary>
    public bool CanAfford(ResourceType type, int amount)
    {
        return IsValidAmount(amount) && GetCount(type) >= amount;
    }

    /// <summary>True when the player has everything in the cost list at once.</summary>
    public bool CanAfford(IReadOnlyList<ResourceAmount> cost)
    {
        Dictionary<ResourceType, long> totals = TotalCost(cost);
        if (totals == null)
        {
            return false;
        }

        foreach (KeyValuePair<ResourceType, long> line in totals)
        {
            if (GetCount(line.Key) < line.Value)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Spends one item. Returns false and changes nothing if the amount is
    /// invalid or the player has too few. Example: if (inventory.TrySpend(ResourceType.Coins, 5)) { ... }
    /// </summary>
    public bool TrySpend(ResourceType type, int amount)
    {
        if (!CanAfford(type, amount))
        {
            return false;
        }

        Remove(type, amount);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Spends a cost made of several items, such as 10 Wood and 5 Stone.
    /// The whole cost is checked before anything is taken, so either every
    /// item is spent or none of them are.
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
            Remove(line.type, line.amount);
        }
        Changed?.Invoke();
        return true;
    }

    // Takes items the caller has already checked are there.
    // The selected slot goes first (planting uses the seeds in hand), then the last slots first,
    // so the stacks near the start of the hotbar stay full.
    private void Remove(ResourceType type, int amount)
    {
        if (type == ResourceType.Coins)
        {
            coins -= amount;
            return;
        }

        int left = TakeFromSlot(SelectedSlot, type, amount);
        for (int i = SlotCount - 1; i >= 0 && left > 0; i--)
        {
            left = TakeFromSlot(i, type, left);
        }
    }

    // Takes up to amount from one slot if it holds this item, and returns how many are still to take.
    private int TakeFromSlot(int slot, ResourceType type, int amount)
    {
        if (slotCounts[slot] == 0 || slotItems[slot] != type)
        {
            return amount;
        }

        int taken = Mathf.Min(amount, slotCounts[slot]);
        slotCounts[slot] -= taken;
        return amount - taken;
    }

    // Adds up a cost list per item, so "3 Wood" listed twice counts as 6 Wood.
    // Returns null when the list is empty or any line has an invalid amount.
    // long (a bigger whole number) is used so very large costs can't overflow while adding.
    private Dictionary<ResourceType, long> TotalCost(IReadOnlyList<ResourceAmount> cost)
    {
        if (cost == null || cost.Count == 0)
        {
            Debug.LogWarning("A cost needs at least one item.", this);
            return null;
        }

        var totals = new Dictionary<ResourceType, long>();
        foreach (ResourceAmount line in cost)
        {
            if (!IsValidAmount(line.amount))
            {
                return null;
            }
            totals.TryGetValue(line.type, out long sum);
            totals[line.type] = sum + line.amount;
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

        Debug.LogWarning($"Invalid amount {amount}. Amounts must be greater than zero.", this);
        return false;
    }
}
