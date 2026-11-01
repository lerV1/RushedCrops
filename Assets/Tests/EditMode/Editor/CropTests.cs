using System;
using System.Collections.Generic;
using NUnit.Framework;
using RushedCrops.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Edit Mode tests for the crop assets the farm plots use, and the seed crates that hand out their seeds.
public class CropTests
{
    private static List<CropData> LoadCrops()
    {
        var crops = new List<CropData>();
        foreach (string path in FarmPlotSetup.CropAssets)
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(path);
            Assert.IsNotNull(crop, "Missing crop asset " + path);
            crops.Add(crop);
        }
        return crops;
    }

    [Test]
    public void EveryCropTypeHasOneCropAsset()
    {
        var seen = new HashSet<CropType>();
        foreach (CropData crop in LoadCrops())
        {
            Assert.IsTrue(seen.Add(crop.CropType), crop.name + " repeats the crop type " + crop.CropType);
        }
        foreach (CropType type in Enum.GetValues(typeof(CropType)))
        {
            Assert.IsTrue(seen.Contains(type), "No crop asset grows " + type);
        }
    }

    [Test]
    public void EachCropHasItsOwnSeedsAndHarvest()
    {
        var used = new HashSet<ResourceType>();
        foreach (CropData crop in LoadCrops())
        {
            Assert.AreNotEqual(crop.SeedResource, crop.HarvestResource, crop.name);
            Assert.IsTrue(used.Add(crop.SeedResource), crop.name + " shares its seeds with another crop.");
            Assert.IsTrue(used.Add(crop.HarvestResource), crop.name + " shares its harvest with another crop.");
            Assert.IsFalse(ResourceTypes.IsTool(crop.SeedResource) || crop.SeedResource == ResourceType.Coins, crop.name);
            StringAssert.Contains(crop.CropType.ToString(), crop.SeedResource.ToString(), crop.name + " plants the wrong seeds.");
            StringAssert.StartsWith(crop.CropType.ToString(), crop.HarvestResource.ToString(), crop.name + " harvests the wrong item.");
        }
    }

    [Test]
    public void EachCropHasEveryGrowthModel()
    {
        foreach (CropData crop in LoadCrops())
        {
            Assert.GreaterOrEqual(crop.StageCount, 2, crop.name);
            for (int stage = 0; stage < crop.StageCount; stage++)
            {
                // A broken reference is a Unity "fake null" that NUnit's IsNotNull accepts, so compare with Unity's != null.
                Assert.IsTrue(crop.GetStageModel(stage) != null, crop.name + " is missing the model for stage " + (stage + 1));
            }
            Assert.Greater(crop.HarvestAmount, 0, crop.name);
            Assert.Greater(crop.WaterNeeded, 0, crop.name);
        }
    }

    // ---------- Seed crate ----------

    private GameObject inventoryObject;
    private GameObject crateObject;
    private Inventory inventory;
    private SeedCrate crate;
    private CropData beet;

    [SetUp]
    public void SetUp()
    {
        inventoryObject = new GameObject("Inventory test");
        inventory = inventoryObject.AddComponent<Inventory>();
        TestMessages.Send(inventory, "Awake");

        beet = AssetDatabase.LoadAssetAtPath<CropData>("Assets/_Game/Data/DA_Beet.asset");
        crateObject = new GameObject("Seed crate test");
        crate = crateObject.AddComponent<SeedCrate>();
        var settings = new SerializedObject(crate);
        settings.FindProperty("inventory").objectReferenceValue = inventory;
        settings.FindProperty("crop").objectReferenceValue = beet;
        settings.FindProperty("seedsPerTake").intValue = 5;
        settings.ApplyModifiedPropertiesWithoutUndo();
        TestMessages.Send(crate, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(crateObject);
        Object.DestroyImmediate(inventoryObject);
    }

    [Test]
    public void SeedCrateGivesSeedsEveryTime()
    {
        Assert.IsTrue(crate.CanInteract);
        crate.Interact(null);
        crate.Interact(null);

        Assert.AreEqual(10, inventory.GetCount(ResourceType.BeetSeeds));
        Assert.AreEqual(ResourceType.BeetSeeds, inventory.GetSlotItem(0), "The seeds go into the first slot.");
        Assert.AreEqual(10, inventory.GetSlotCount(0), "The second handful stacks on the first.");
    }

    [Test]
    public void SeedCrateGivesNothingWhenTheHotbarIsFull()
    {
        inventory.Add(ResourceType.Wood, inventory.MaxStack * Inventory.SlotCount);

        Assert.IsFalse(crate.CanInteract);
        Assert.AreEqual("Hotbar is full", crate.GetPrompt());
        crate.Interact(null);
        Assert.AreEqual(0, inventory.GetCount(ResourceType.BeetSeeds));
    }

    [Test]
    public void PlantedSeedsComeFromTheCrate()
    {
        crate.Interact(null);

        Assert.IsTrue(inventory.TryGetSelectedItem(out ResourceType held));
        Assert.AreEqual(beet.SeedResource, held, "Holding the crate's seeds selects the beet crop for planting.");
        Assert.IsTrue(inventory.TrySpend(beet.SeedResource, 1));
        Assert.AreEqual(4, inventory.GetCount(ResourceType.BeetSeeds));
    }
}
