using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Edit Mode tests for the merchant: buying and selling through the Inventory,
// nothing changing when a trade can't go through, and prices that move by day.
public class MerchantTests
{
    private GameObject gameObject;
    private Inventory inventory;
    private Merchant merchant;
    private MerchantPrices prices;

    [SetUp]
    public void SetUp()
    {
        gameObject = new GameObject("Merchant test");
        inventory = gameObject.AddComponent<Inventory>();
        TestMessages.Send(inventory, "Awake");

        // Fixed prices (no daily swing) so the sums below are exact.
        prices = MakePrices(0f, 0f,
            new[] { (ResourceType.CarrotSeeds, 3), (ResourceType.Wood, 8) },
            new[] { (ResourceType.Carrots, 4) });

        merchant = gameObject.AddComponent<Merchant>();
        var settings = new SerializedObject(merchant);
        settings.FindProperty("inventory").objectReferenceValue = inventory;
        settings.FindProperty("prices").objectReferenceValue = prices;
        settings.ApplyModifiedPropertiesWithoutUndo();
        TestMessages.Send(merchant, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gameObject);
        Object.DestroyImmediate(prices);
    }

    private static MerchantPrices MakePrices(float forSaleSwing, float wantedSwing,
        (ResourceType item, int price)[] forSale, (ResourceType item, int price)[] wanted)
    {
        var list = ScriptableObject.CreateInstance<MerchantPrices>();
        var settings = new SerializedObject(list);
        Fill(settings.FindProperty("forSale"), forSale);
        Fill(settings.FindProperty("wanted"), wanted);
        settings.FindProperty("forSaleSwing").floatValue = forSaleSwing;
        settings.FindProperty("wantedSwing").floatValue = wantedSwing;
        settings.ApplyModifiedPropertiesWithoutUndo();
        return list;
    }

    private static void Fill(SerializedProperty array, (ResourceType item, int price)[] lines)
    {
        array.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            array.GetArrayElementAtIndex(i).FindPropertyRelative("item").enumValueIndex = (int)lines[i].item;
            array.GetArrayElementAtIndex(i).FindPropertyRelative("basePrice").intValue = lines[i].price;
        }
    }

    // ---------- Buying ----------

    [Test]
    public void BuyingTakesCoinsAndAddsTheGoods()
    {
        inventory.Add(ResourceType.Coins, 20);

        Assert.AreEqual(TradeResult.Done, merchant.TryBuy(ResourceType.CarrotSeeds, 5));

        Assert.AreEqual(5, inventory.GetCount(ResourceType.Coins));
        Assert.AreEqual(5, inventory.GetCount(ResourceType.CarrotSeeds));
    }

    [Test]
    public void BuyingWithoutEnoughCoinsChangesNothing()
    {
        inventory.Add(ResourceType.Coins, 7);

        Assert.AreEqual(TradeResult.NotEnoughCoins, merchant.TryBuy(ResourceType.Wood, 1));

        Assert.AreEqual(7, inventory.GetCount(ResourceType.Coins));
        Assert.AreEqual(0, inventory.GetCount(ResourceType.Wood));
    }

    [Test]
    public void BuyingWithAFullHotbarKeepsTheCoins()
    {
        inventory.Add(ResourceType.Coins, 50);
        // Tools don't stack, so five different items fill every slot.
        foreach (ResourceType item in new[] { ResourceType.Hoe, ResourceType.WateringCan, ResourceType.Hatchet, ResourceType.Stone, ResourceType.Beets })
        {
            inventory.Add(item, 1);
        }

        Assert.AreEqual(TradeResult.NoRoom, merchant.TryBuy(ResourceType.CarrotSeeds, 1));

        Assert.AreEqual(50, inventory.GetCount(ResourceType.Coins));
    }

    [Test]
    public void ThingsHeDoesntSellCantBeBought()
    {
        inventory.Add(ResourceType.Coins, 50);

        Assert.AreEqual(0, merchant.PriceToBuy(ResourceType.Hoe));
        Assert.AreEqual(TradeResult.NotForSale, merchant.TryBuy(ResourceType.Hoe, 1));
        Assert.AreEqual(50, inventory.GetCount(ResourceType.Coins));
    }

    // ---------- Selling ----------

    [Test]
    public void SellingGivesCoinsAndTakesTheCrops()
    {
        inventory.Add(ResourceType.Carrots, 6);

        Assert.AreEqual(TradeResult.Done, merchant.TrySell(ResourceType.Carrots, 4));

        Assert.AreEqual(16, inventory.GetCount(ResourceType.Coins));
        Assert.AreEqual(2, inventory.GetCount(ResourceType.Carrots));
    }

    [Test]
    public void SellingMoreThanYouHaveChangesNothing()
    {
        inventory.Add(ResourceType.Carrots, 2);

        Assert.AreEqual(TradeResult.NotEnoughItems, merchant.TrySell(ResourceType.Carrots, 3));

        Assert.AreEqual(2, inventory.GetCount(ResourceType.Carrots));
        Assert.AreEqual(0, inventory.GetCount(ResourceType.Coins));
    }

    [Test]
    public void ThingsHeDoesntWantCantBeSold()
    {
        inventory.Add(ResourceType.Hoe, 1);

        Assert.AreEqual(TradeResult.NotWanted, merchant.TrySell(ResourceType.Hoe, 1));
        Assert.AreEqual(1, inventory.GetCount(ResourceType.Hoe));
    }

    [Test]
    public void ZeroOrNegativeAmountsAreRejected()
    {
        inventory.Add(ResourceType.Coins, 50);
        inventory.Add(ResourceType.Carrots, 5);

        Assert.AreEqual(TradeResult.InvalidAmount, merchant.TryBuy(ResourceType.CarrotSeeds, 0));
        Assert.AreEqual(TradeResult.InvalidAmount, merchant.TrySell(ResourceType.Carrots, -2));
        Assert.AreEqual(50, inventory.GetCount(ResourceType.Coins));
        Assert.AreEqual(5, inventory.GetCount(ResourceType.Carrots));
    }

    [Test]
    public void TradedIsRaisedOnlyForTradesThatWentThrough()
    {
        var trades = new List<(ResourceType, int, bool)>();
        merchant.Traded += (item, amount, bought) => trades.Add((item, amount, bought));
        inventory.Add(ResourceType.Coins, 3);
        inventory.Add(ResourceType.Carrots, 1);

        merchant.TryBuy(ResourceType.Wood, 1);        // can't afford
        merchant.TryBuy(ResourceType.CarrotSeeds, 1); // goes through
        merchant.TrySell(ResourceType.Carrots, 1);    // goes through

        CollectionAssert.AreEqual(new[] { (ResourceType.CarrotSeeds, 1, true), (ResourceType.Carrots, 1, false) }, trades);
    }

    [Test]
    public void RefusedTradesSayWhy()
    {
        var reasons = new List<TradeResult>();
        merchant.TradeRefused += reasons.Add;
        inventory.Add(ResourceType.Coins, 1);

        merchant.TryBuy(ResourceType.Wood, 1);      // costs 8
        merchant.TrySell(ResourceType.Carrots, 1);  // has none
        merchant.TryBuy(ResourceType.Hoe, 1);       // not for sale

        CollectionAssert.AreEqual(new[] { TradeResult.NotEnoughCoins, TradeResult.NotEnoughItems, TradeResult.NotForSale }, reasons);
    }

    // ---------- Daily prices ----------

    [Test]
    public void PricesHoldAllDayAndChangeBetweenDays()
    {
        MerchantPrices moving = MakePrices(0.2f, 0.3f,
            new[] { (ResourceType.CabbageSeeds, 50) }, new[] { (ResourceType.Cabbages, 100) });
        try
        {
            var seen = new HashSet<int>();
            for (int day = 1; day <= 20; day++)
            {
                int pay = moving.PayFor(ResourceType.Cabbages, day);
                Assert.AreEqual(pay, moving.PayFor(ResourceType.Cabbages, day), "the same day always gives the same price");
                Assert.That(pay, Is.InRange(70, 130), "what he pays moves at most 30%");
                Assert.That(moving.ChargeFor(ResourceType.CabbageSeeds, day), Is.InRange(40, 60), "what he charges moves at most 20%");
                seen.Add(pay);
            }
            Assert.Greater(seen.Count, 3, "prices should change from day to day");
        }
        finally
        {
            Object.DestroyImmediate(moving);
        }
    }

    [Test]
    public void PricesNeverDropBelowOneCoin()
    {
        MerchantPrices cheap = MakePrices(0.9f, 0.9f,
            new[] { (ResourceType.WheatSeeds, 1) }, new[] { (ResourceType.Wheat, 1) });
        try
        {
            for (int day = 1; day <= 30; day++)
            {
                Assert.GreaterOrEqual(cheap.ChargeFor(ResourceType.WheatSeeds, day), 1);
                Assert.GreaterOrEqual(cheap.PayFor(ResourceType.Wheat, day), 1);
            }
        }
        finally
        {
            Object.DestroyImmediate(cheap);
        }
    }
}
