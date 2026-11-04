using System;
using System.Collections.Generic;
using NUnit.Framework;
using RushedCrops.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Edit Mode tests for dropping hotbar items (ItemDropper) and the pickups they become (ItemDrop).
public class ItemDropTests
{
    private readonly List<Object> made = new List<Object>();
    private Inventory inventory;
    private ItemDropper dropper;

    [SetUp]
    public void SetUp()
    {
        var holder = new GameObject("Inventory test");
        made.Add(holder);
        inventory = holder.AddComponent<Inventory>();
        TestMessages.Send(inventory, "Awake");

        var player = new GameObject("Player test");
        made.Add(player);
        var view = new GameObject("View");
        view.transform.SetParent(player.transform, false);
        view.transform.localPosition = Vector3.up * 1.6f;

        dropper = player.AddComponent<ItemDropper>();
        var tools = player.GetComponent<PlayerTools>();
        var toolSettings = new SerializedObject(tools);
        toolSettings.FindProperty("inventory").objectReferenceValue = inventory;
        toolSettings.ApplyModifiedPropertiesWithoutUndo();

        var settings = new SerializedObject(dropper);
        settings.FindProperty("viewPoint").objectReferenceValue = view.transform;
        settings.FindProperty("dropPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDrop>(ItemDropSetup.DropPath);
        settings.ApplyModifiedPropertiesWithoutUndo();
        TestMessages.Send(dropper, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (ItemDrop drop in Object.FindObjectsByType<ItemDrop>())
        {
            if (!EditorUtility.IsPersistent(drop)) Object.DestroyImmediate(drop.gameObject);
        }
        foreach (Object thing in made)
        {
            if (thing != null) Object.DestroyImmediate(thing);
        }
        made.Clear();
    }

    [Test]
    public void DropTakesOneOfTheSelectedItem()
    {
        inventory.Add(ResourceType.CarrotSeeds, 5);

        ItemDrop drop = dropper.Drop(false);

        Assert.IsNotNull(drop);
        Assert.AreEqual(ResourceType.CarrotSeeds, drop.Item);
        Assert.AreEqual(1, drop.Amount);
        Assert.AreEqual(4, inventory.GetCount(ResourceType.CarrotSeeds));
    }

    [Test]
    public void DropWholeStackEmptiesTheSlot()
    {
        inventory.Add(ResourceType.Wood, 7);

        ItemDrop drop = dropper.Drop(true);

        Assert.AreEqual(7, drop.Amount);
        Assert.AreEqual(0, inventory.GetCount(ResourceType.Wood));
        Assert.IsTrue(inventory.IsSlotEmpty(0));
    }

    [Test]
    public void DroppingFromAnEmptySlotDoesNothing()
    {
        Assert.IsNull(dropper.Drop(true));
    }

    [Test]
    public void ADropInTheAirCantBeTaken()
    {
        inventory.Add(ResourceType.Hoe, 1);
        ItemDrop drop = dropper.Drop(false);

        Assert.IsFalse(drop.TryCollect(), "It has to land first.");
        Assert.AreEqual(0, inventory.GetCount(ResourceType.Hoe));
    }

    [Test]
    public void EveryItemExceptCoinsHasAGroundModel()
    {
        var models = AssetDatabase.LoadAssetAtPath<ItemModels>(ItemDropSetup.ModelsPath);
        Assert.IsNotNull(models, "Run Rushed Crops > Items > Set up item drops.");
        foreach (ResourceType item in Enum.GetValues(typeof(ResourceType)))
        {
            if (item == ResourceType.Coins) continue;
            Assert.IsNotNull(models.Get(item), item + " has no model to show on the ground.");
        }
    }
}
