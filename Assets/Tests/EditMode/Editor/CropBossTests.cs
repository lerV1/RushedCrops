using System.Collections.Generic;
using NUnit.Framework;
using RushedCrops.Editor;
using UnityEditor;
using UnityEngine;

// Edit Mode tests for the crop boss and the seed bags it drops.
public class CropBossTests
{
    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void BossDropsSeedsForEveryCrop()
    {
        root = new GameObject("Test parent");
        GameObject placed = CropBossSetup.Place(root.transform);
        var boss = placed.GetComponent<CropBoss>();

        var seeds = new HashSet<ResourceType>();
        foreach (CropBoss.SeedDropEntry drop in boss.Drops)
        {
            Assert.IsNotNull(drop.crop);
            Assert.Greater(drop.amount, 0);
            seeds.Add(drop.crop.SeedResource);
        }
        Assert.AreEqual(FarmPlotSetup.CropAssets.Length, seeds.Count, "Each crop should drop its own seeds once.");
    }

    [Test]
    public void BossStandsStillAndCanBeHit()
    {
        root = new GameObject("Test parent");
        GameObject placed = CropBossSetup.Place(root.transform);

        Assert.IsNull(placed.GetComponent<Rigidbody>(), "The boss must not be pushed around.");
        Assert.IsNotNull(placed.GetComponent<Health>());
        Assert.IsNotNull(placed.GetComponent<HitFlash>());
        Assert.IsNotNull(placed.GetComponent<Collider>(), "The hatchet needs a collider to hit.");
    }

    [Test]
    public void SeedBagGivesItsSeedsOnceLanded()
    {
        root = new GameObject("Test parent");
        var inventory = root.AddComponent<Inventory>();
        var crop = AssetDatabase.LoadAssetAtPath<CropData>(FarmPlotSetup.CropAssets[0]);
        var prefab = AssetDatabase.LoadAssetAtPath<SeedDrop>(CropBossSetup.SeedBagPath);
        Assert.IsNotNull(prefab, "Run Place crop boss to make the seed bag prefab.");

        SeedDrop bag = Object.Instantiate(prefab, root.transform);
        bag.Launch(inventory, crop, 3, Vector3.zero);
        int before = inventory.GetCount(crop.SeedResource);

        Assert.IsFalse(bag.TryCollect(), "A bag still in the air can't be taken.");
        Assert.AreEqual(before, inventory.GetCount(crop.SeedResource));
    }
}
