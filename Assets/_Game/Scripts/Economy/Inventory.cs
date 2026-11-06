using System;
using System.Collections.Generic;
using UnityEngine;

// The coin wallet and 5-slot hotbar shared by farming, the merchant and the barn.
// Every change goes through these methods, which refuse bad amounts, so nothing is half added or spent.
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

    public int SelectedSlot { get; private set; }

    public int MaxStack => maxStack;

    // Raised after any change, so the HUD only redraws then.
    public event Action Changed;

    private void Awake()
    {
        ResetToStartingAmounts();
    }

    public void ResetToStartingAmounts()
    {
        Array.Clear(slotCounts, 0, SlotCount);
        coins = startingCoins;
        SelectedSlot = 0;
        Changed?.Invoke();
    }

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

    // Check IsSlotEmpty first: an empty slot has no item.
    public ResourceType GetSlotItem(int slot) => slotItems[slot];

    public bool TryGetSelectedItem(out ResourceType item)
    {
        item = slotItems[SelectedSlot];
        return slotCounts[SelectedSlot] > 0;
    }

    public int MaxStackFor(ResourceType type) => ResourceTypes.IsTool(type) ? 1 : maxStack;

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

    // Coins always fit; other items need hotbar room.
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

    // All of it goes in or none of it does.
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

    public bool CanAfford(ResourceType type, int amount)
    {
        return IsValidAmount(amount) && GetCount(type) >= amount;
    }

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

    // Either the whole cost is spent or none of it.
    public bool TrySpend(IReadOnlyList<ResourceAmount> cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }

        foreach (ResourceAmount line in cost)
        {
            Remove(line.type, line.amount);
        }
        Changed?.Invoke();
        return true;
    }

    // The selected slot pays first (planting uses the seeds in hand), then the last slots.
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

    // Totals per item, so "3 Wood" listed twice is 6. Null when the list is empty or an amount is bad.
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
