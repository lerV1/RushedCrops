using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Behaviour checks for Inventory. Each test builds a fresh inventory, so order doesn't matter.
public static class InventoryTests
{
    public static void Register(TestRunner run)
    {
        run.Test("StartingAmounts_MatchDefaults", StartingAmounts_MatchDefaults);
        run.Test("StartingAmounts_UseInspectorValues", StartingAmounts_UseInspectorValues);
        run.Test("Add_PositiveAmount_IncreasesCount", Add_PositiveAmount_IncreasesCount);
        run.Test("Add_ZeroOrNegative_IsRejected", Add_ZeroOrNegative_IsRejected);
        run.Test("Add_PastIntMax_IsRejected", Add_PastIntMax_IsRejected);
        run.Test("CanAfford_ChecksSingleResource", CanAfford_ChecksSingleResource);
        run.Test("TrySpend_Affordable_Deducts", TrySpend_Affordable_Deducts);
        run.Test("TrySpend_ExactBalance_ReachesZero", TrySpend_ExactBalance_ReachesZero);
        run.Test("TrySpend_Insufficient_ChangesNothing", TrySpend_Insufficient_ChangesNothing);
        run.Test("TrySpend_ZeroOrNegative_IsRejected", TrySpend_ZeroOrNegative_IsRejected);
        run.Test("MultiCost_Affordable_DeductsEveryLine", MultiCost_Affordable_DeductsEveryLine);
        run.Test("MultiCost_OneLineShort_ChangesNothing", MultiCost_OneLineShort_ChangesNothing);
        run.Test("MultiCost_RepeatedType_IsAddedUpFirst", MultiCost_RepeatedType_IsAddedUpFirst);
        run.Test("MultiCost_InvalidLine_ChangesNothing", MultiCost_InvalidLine_ChangesNothing);
        run.Test("MultiCost_NullOrEmpty_IsRejected", MultiCost_NullOrEmpty_IsRejected);
        run.Test("MultiCost_HugeTotal_DoesNotOverflow", MultiCost_HugeTotal_DoesNotOverflow);
        run.Test("Changed_FiresOnlyOnSuccess", Changed_FiresOnlyOnSuccess);
    }

    private static Inventory NewInventory()
    {
        var inventory = new Inventory();
        inventory.ResetToStartingAmounts();
        return inventory;
    }

    private static Dictionary<ResourceType, int> Snapshot(Inventory inventory)
    {
        var counts = new Dictionary<ResourceType, int>();
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            counts[type] = inventory.GetCount(type);
        }
        return counts;
    }

    private static void AssertUnchanged(Dictionary<ResourceType, int> before, Inventory inventory)
    {
        foreach (var pair in before)
        {
            Assert.Equal(pair.Value, inventory.GetCount(pair.Key), $"{pair.Key} changed");
        }
    }

    private static void StartingAmounts_MatchDefaults()
    {
        var inventory = NewInventory();

        Assert.Equal(20, inventory.GetCount(ResourceType.Coins));
        Assert.Equal(5, inventory.GetCount(ResourceType.CarrotSeeds));
        Assert.Equal(5, inventory.GetCount(ResourceType.TomatoSeeds));
        Assert.Equal(0, inventory.GetCount(ResourceType.Carrots));
        Assert.Equal(0, inventory.GetCount(ResourceType.Tomatoes));
        Assert.Equal(0, inventory.GetCount(ResourceType.Wood));
        Assert.Equal(0, inventory.GetCount(ResourceType.Stone));
    }

    private static void StartingAmounts_UseInspectorValues()
    {
        var inventory = new Inventory();
        // Stand-in for typing a value into the Inspector field.
        typeof(Inventory)
            .GetField("startingStone", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(inventory, 7);

        inventory.ResetToStartingAmounts();

        Assert.Equal(7, inventory.GetCount(ResourceType.Stone));
    }

    private static void Add_PositiveAmount_IncreasesCount()
    {
        var inventory = NewInventory();

        bool added = inventory.Add(ResourceType.Wood, 3);

        Assert.True(added);
        Assert.Equal(3, inventory.GetCount(ResourceType.Wood));
    }

    private static void Add_ZeroOrNegative_IsRejected()
    {
        var inventory = NewInventory();
        int warningsBefore = Debug.WarningCount;

        Assert.False(inventory.Add(ResourceType.Coins, 0));
        Assert.False(inventory.Add(ResourceType.Coins, -5));

        Assert.Equal(20, inventory.GetCount(ResourceType.Coins));
        Assert.Equal(warningsBefore + 2, Debug.WarningCount, "each invalid amount should log a warning");
    }

    private static void Add_PastIntMax_IsRejected()
    {
        var inventory = NewInventory();
        inventory.Add(ResourceType.Stone, int.MaxValue);

        Assert.False(inventory.Add(ResourceType.Stone, 1));
        Assert.Equal(int.MaxValue, inventory.GetCount(ResourceType.Stone));
    }

    private static void CanAfford_ChecksSingleResource()
    {
        var inventory = NewInventory();

        Assert.True(inventory.CanAfford(ResourceType.Coins, 20));
        Assert.False(inventory.CanAfford(ResourceType.Coins, 21));
        Assert.False(inventory.CanAfford(ResourceType.Coins, 0));
        Assert.False(inventory.CanAfford(ResourceType.Coins, -1));
    }

    private static void TrySpend_Affordable_Deducts()
    {
        var inventory = NewInventory();

        bool spent = inventory.TrySpend(ResourceType.Coins, 8);

        Assert.True(spent);
        Assert.Equal(12, inventory.GetCount(ResourceType.Coins));
    }

    private static void TrySpend_ExactBalance_ReachesZero()
    {
        var inventory = NewInventory();

        Assert.True(inventory.TrySpend(ResourceType.CarrotSeeds, 5));
        Assert.Equal(0, inventory.GetCount(ResourceType.CarrotSeeds));
    }

    private static void TrySpend_Insufficient_ChangesNothing()
    {
        var inventory = NewInventory();

        Assert.False(inventory.TrySpend(ResourceType.Coins, 21));
        Assert.False(inventory.TrySpend(ResourceType.Wood, 1));

        Assert.Equal(20, inventory.GetCount(ResourceType.Coins));
        Assert.Equal(0, inventory.GetCount(ResourceType.Wood));
    }

    private static void TrySpend_ZeroOrNegative_IsRejected()
    {
        var inventory = NewInventory();

        Assert.False(inventory.TrySpend(ResourceType.Coins, 0));
        // Spending -5 must not quietly add 5.
        Assert.False(inventory.TrySpend(ResourceType.Coins, -5));

        Assert.Equal(20, inventory.GetCount(ResourceType.Coins));
    }

    private static void MultiCost_Affordable_DeductsEveryLine()
    {
        var inventory = NewInventory();
        inventory.Add(ResourceType.Wood, 10);
        inventory.Add(ResourceType.Stone, 4);
        var repair = new[]
        {
            new ResourceAmount(ResourceType.Wood, 6),
            new ResourceAmount(ResourceType.Stone, 4),
            new ResourceAmount(ResourceType.Coins, 15),
        };

        Assert.True(inventory.CanAfford(repair));
        Assert.True(inventory.TrySpend(repair));

        Assert.Equal(4, inventory.GetCount(ResourceType.Wood));
        Assert.Equal(0, inventory.GetCount(ResourceType.Stone));
        Assert.Equal(5, inventory.GetCount(ResourceType.Coins));
    }

    private static void MultiCost_OneLineShort_ChangesNothing()
    {
        var inventory = NewInventory();
        inventory.Add(ResourceType.Wood, 10);
        var before = Snapshot(inventory);
        // Wood and coins are affordable; stone is not. The last line fails, so the first must not be taken.
        var repair = new[]
        {
            new ResourceAmount(ResourceType.Wood, 6),
            new ResourceAmount(ResourceType.Coins, 15),
            new ResourceAmount(ResourceType.Stone, 1),
        };

        Assert.False(inventory.CanAfford(repair));
        Assert.False(inventory.TrySpend(repair));

        AssertUnchanged(before, inventory);
    }

    private static void MultiCost_RepeatedType_IsAddedUpFirst()
    {
        var inventory = NewInventory();
        inventory.Add(ResourceType.Wood, 5);
        var before = Snapshot(inventory);
        // Each line alone fits in 5 wood, but together they need 6.
        var cost = new[]
        {
            new ResourceAmount(ResourceType.Wood, 3),
            new ResourceAmount(ResourceType.Wood, 3),
        };

        Assert.False(inventory.TrySpend(cost));

        AssertUnchanged(before, inventory);
    }

    private static void MultiCost_InvalidLine_ChangesNothing()
    {
        var inventory = NewInventory();
        var before = Snapshot(inventory);
        var cost = new[]
        {
            new ResourceAmount(ResourceType.Coins, 5),
            new ResourceAmount(ResourceType.CarrotSeeds, -2),
        };

        Assert.False(inventory.CanAfford(cost));
        Assert.False(inventory.TrySpend(cost));

        AssertUnchanged(before, inventory);
    }

    private static void MultiCost_NullOrEmpty_IsRejected()
    {
        var inventory = NewInventory();

        Assert.False(inventory.TrySpend((IReadOnlyList<ResourceAmount>)null));
        Assert.False(inventory.TrySpend(new ResourceAmount[0]));
    }

    private static void MultiCost_HugeTotal_DoesNotOverflow()
    {
        var inventory = NewInventory();
        inventory.Add(ResourceType.Coins, int.MaxValue - 20);
        var before = Snapshot(inventory);
        // Added as ints these two lines would wrap round to a negative number and look affordable.
        var cost = new[]
        {
            new ResourceAmount(ResourceType.Coins, int.MaxValue),
            new ResourceAmount(ResourceType.Coins, int.MaxValue),
        };

        Assert.False(inventory.TrySpend(cost));

        AssertUnchanged(before, inventory);
    }

    private static void Changed_FiresOnlyOnSuccess()
    {
        var inventory = NewInventory();
        int changes = 0;
        inventory.Changed += () => changes++;

        inventory.Add(ResourceType.Wood, 1);
        inventory.TrySpend(ResourceType.Wood, 1);
        inventory.Add(ResourceType.Wood, -1);
        inventory.TrySpend(ResourceType.Wood, 1);

        Assert.Equal(2, changes);
    }
}
