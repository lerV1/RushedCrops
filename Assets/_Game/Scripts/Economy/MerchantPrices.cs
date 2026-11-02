using System;
using System.Collections.Generic;
using UnityEngine;

// The merchant's price list: what he sells (seeds and building supplies), what he buys (crops),
// and the usual price of each. Prices move a little every day, so it pays to check before trading.
// One asset (DA_MerchantPrices) holds the whole list. Change prices there, not in code.
// Create one with Assets > Create > Rushed Crops > Merchant Prices.
[CreateAssetMenu(fileName = "DA_MerchantPrices", menuName = "Rushed Crops/Merchant Prices")]
public class MerchantPrices : ScriptableObject
{
    // One line of the price list, such as "Carrot Seeds, usually 3 coins".
    [Serializable]
    public struct Offer
    {
        public ResourceType item;
        [Min(1)] public int basePrice;
    }

    [Tooltip("What the merchant sells to the player, and his usual price for one.")]
    [SerializeField] private Offer[] forSale = new Offer[0];

    [Tooltip("What the merchant buys from the player, and what he usually pays for one.")]
    [SerializeField] private Offer[] wanted = new Offer[0];

    [Tooltip("How far his selling prices move from day to day. 0.2 means up to 20% up or down.")]
    [SerializeField, Range(0f, 0.9f)] private float forSaleSwing = 0.2f;

    [Tooltip("How far what he pays moves from day to day. 0.3 means up to 30% up or down.")]
    [SerializeField, Range(0f, 0.9f)] private float wantedSwing = 0.3f;

    [Tooltip("Picks which way prices move each day. The same number always gives the same prices.")]
    [SerializeField] private int seed = 451;

    public IReadOnlyList<Offer> ForSale => forSale;
    public IReadOnlyList<Offer> Wanted => wanted;

    /// <summary>What the merchant charges for one of this item on a day, or 0 if he doesn't sell it.</summary>
    public int ChargeFor(ResourceType item, int day) => Price(forSale, item, day, forSaleSwing, 1);

    /// <summary>What the merchant pays for one of this item on a day, or 0 if he doesn't want it.</summary>
    public int PayFor(ResourceType item, int day) => Price(wanted, item, day, wantedSwing, 2);

    private int Price(Offer[] offers, ResourceType item, int day, float swing, int list)
    {
        foreach (Offer offer in offers)
        {
            if (offer.item != item)
            {
                continue;
            }
            // A number from -1 to 1 for this day and item, turned into "up to swing% up or down".
            float change = (float)(DailyRoll(day, (int)item, list) * 2.0 - 1.0) * swing;
            return Mathf.Max(1, Mathf.RoundToInt(offer.basePrice * (1f + change)));
        }
        return 0;
    }

    // A repeatable number from 0 to 1: the same day, item and list always give the same price,
    // so prices hold all day and only change when a new day starts.
    private double DailyRoll(int day, int item, int list)
    {
        unchecked
        {
            int key = seed * 486187739 + day * 7919 + item * 104729 + list * 15485863;
            return new System.Random(key).NextDouble();
        }
    }
}
