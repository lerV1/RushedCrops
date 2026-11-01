using NUnit.Framework;
using UnityEngine;

// Edit Mode tests for the Inventory's hotbar: empty start, filling slots, stacking to 99,
// selecting slots and spending.
public class InventoryTests
{
    private GameObject gameObject;
    private Inventory inventory;

    [SetUp]
    public void SetUp()
    {
        gameObject = new GameObject("Inventory test");
        inventory = gameObject.AddComponent<Inventory>();
        // Unity doesn't run Awake in Edit Mode, so call it the way Unity would at startup.
        TestMessages.Send(inventory, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gameObject);
    }

    [Test]
    public void StartsEmptyWithFirstSlotSelected()
    {
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            Assert.IsTrue(inventory.IsSlotEmpty(i));
        }
        Assert.AreEqual(0, inventory.GetCount(ResourceType.Coins));
        Assert.AreEqual(0, inventory.SelectedSlot);
        Assert.IsFalse(inventory.TryGetSelectedItem(out _));
    }

    [Test]
    public void NewItemsGoToTheFirstEmptySlot()
    {
        Assert.IsTrue(inventory.Add(ResourceType.Wood, 3));
        Assert.IsTrue(inventory.Add(ResourceType.Stone, 2));

        Assert.AreEqual(ResourceType.Wood, inventory.GetSlotItem(0));
        Assert.AreEqual(3, inventory.GetSlotCount(0));
        Assert.AreEqual(ResourceType.Stone, inventory.GetSlotItem(1));
        Assert.AreEqual(2, inventory.GetSlotCount(1));
        Assert.IsTrue(inventory.IsSlotEmpty(2));
    }

    [Test]
    public void SameItemStacksUpTo99ThenSpillsIntoTheNextEmptySlot()
    {
        inventory.Add(ResourceType.Wood, 90);
        inventory.Add(ResourceType.Stone, 1);
        inventory.Add(ResourceType.Wood, 15);

        Assert.AreEqual(99, inventory.GetSlotCount(0));
        Assert.AreEqual(ResourceType.Stone, inventory.GetSlotItem(1));
        Assert.AreEqual(ResourceType.Wood, inventory.GetSlotItem(2));
        Assert.AreEqual(6, inventory.GetSlotCount(2));
        Assert.AreEqual(105, inventory.GetCount(ResourceType.Wood));
    }

    [Test]
    public void EmptiedSlotIsReusedFirst()
    {
        inventory.Add(ResourceType.Wood, 1);
        inventory.Add(ResourceType.Stone, 1);
        Assert.IsTrue(inventory.TrySpend(ResourceType.Wood, 1));

        inventory.Add(ResourceType.Carrots, 4);

        Assert.AreEqual(ResourceType.Carrots, inventory.GetSlotItem(0));
        Assert.AreEqual(ResourceType.Stone, inventory.GetSlotItem(1));
    }

    [Test]
    public void ToolsTakeOneSlotEachAndDontStack()
    {
        Assert.AreEqual(1, inventory.MaxStackFor(ResourceType.Hoe));
        inventory.Add(ResourceType.Hoe, 1);
        inventory.Add(ResourceType.Hoe, 1);

        Assert.AreEqual(1, inventory.GetSlotCount(0));
        Assert.AreEqual(1, inventory.GetSlotCount(1));
    }

    [Test]
    public void FullHotbarRejectsTheWholeAddAndChangesNothing()
    {
        inventory.Add(ResourceType.Wood, 99 * 4);
        inventory.Add(ResourceType.Stone, 98);

        Assert.IsTrue(inventory.CanAdd(ResourceType.Stone, 1));
        Assert.IsFalse(inventory.Add(ResourceType.Stone, 2), "Only 1 stone fits, so adding 2 must fail.");
        Assert.AreEqual(98, inventory.GetCount(ResourceType.Stone));
        Assert.IsFalse(inventory.Add(ResourceType.Carrots, 1), "No empty slot is left for a new item.");
        Assert.AreEqual(0, inventory.GetCount(ResourceType.Carrots));
    }

    [Test]
    public void CoinsDontUseHotbarSlots()
    {
        inventory.Add(ResourceType.Coins, 500);

        Assert.AreEqual(500, inventory.GetCount(ResourceType.Coins));
        Assert.IsTrue(inventory.IsSlotEmpty(0));
    }

    [Test]
    public void SelectingASlotChangesTheHeldItem()
    {
        inventory.Add(ResourceType.Wood, 1);
        inventory.Add(ResourceType.CarrotSeeds, 5);

        Assert.IsTrue(inventory.SelectSlot(1));
        Assert.IsTrue(inventory.TryGetSelectedItem(out ResourceType held));
        Assert.AreEqual(ResourceType.CarrotSeeds, held);

        Assert.IsTrue(inventory.SelectSlot(4));
        Assert.IsFalse(inventory.TryGetSelectedItem(out _));
    }

    [Test]
    public void SelectingASlotOutsideTheHotbarIsRejected()
    {
        Assert.IsFalse(inventory.SelectSlot(5));
        Assert.IsFalse(inventory.SelectSlot(-1));
        Assert.AreEqual(0, inventory.SelectedSlot);
    }

    [Test]
    public void SpendingTakesFromTheSelectedSlotFirst()
    {
        inventory.Add(ResourceType.CarrotSeeds, 99);
        inventory.Add(ResourceType.CarrotSeeds, 10); // spills into slot 1
        inventory.SelectSlot(0);

        Assert.IsTrue(inventory.TrySpend(ResourceType.CarrotSeeds, 1));

        Assert.AreEqual(98, inventory.GetSlotCount(0));
        Assert.AreEqual(10, inventory.GetSlotCount(1));
    }

    [Test]
    public void SpendingMoreThanTheSelectedSlotHoldsUsesOtherStacks()
    {
        inventory.Add(ResourceType.Wood, 105); // 99 in slot 0, 6 in slot 1
        inventory.SelectSlot(1);

        Assert.IsTrue(inventory.TrySpend(ResourceType.Wood, 10));

        Assert.IsTrue(inventory.IsSlotEmpty(1));
        Assert.AreEqual(95, inventory.GetSlotCount(0));
    }

    [Test]
    public void SpendingWithoutEnoughChangesNothing()
    {
        inventory.Add(ResourceType.Wood, 3);

        Assert.IsFalse(inventory.TrySpend(ResourceType.Wood, 4));
        Assert.AreEqual(3, inventory.GetCount(ResourceType.Wood));
    }

    [Test]
    public void InvalidAmountsAreRejected()
    {
        Assert.IsFalse(inventory.Add(ResourceType.Wood, 0));
        Assert.IsFalse(inventory.Add(ResourceType.Wood, -5));
        Assert.IsFalse(inventory.TrySpend(ResourceType.Wood, -1));
        Assert.IsTrue(inventory.IsSlotEmpty(0));
    }

    [Test]
    public void MultiItemCostIsAllOrNothing()
    {
        inventory.Add(ResourceType.Wood, 10);
        inventory.Add(ResourceType.Stone, 2);
        var cost = new[] { new ResourceAmount(ResourceType.Wood, 10), new ResourceAmount(ResourceType.Stone, 5) };

        Assert.IsFalse(inventory.TrySpend(cost));
        Assert.AreEqual(10, inventory.GetCount(ResourceType.Wood));
        Assert.AreEqual(2, inventory.GetCount(ResourceType.Stone));

        inventory.Add(ResourceType.Stone, 3);
        Assert.IsTrue(inventory.TrySpend(cost));
        Assert.AreEqual(0, inventory.GetCount(ResourceType.Wood));
        Assert.AreEqual(0, inventory.GetCount(ResourceType.Stone));
    }
}
