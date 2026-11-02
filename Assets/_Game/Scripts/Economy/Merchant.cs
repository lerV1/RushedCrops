using System;
using UnityEngine;

// What happened when the player tried to trade. Done means the trade went through;
// anything else means nothing changed, and says why.
public enum TradeResult
{
    Done,
    InvalidAmount,
    NotForSale,      // the merchant doesn't sell that
    NotWanted,       // the merchant doesn't buy that
    NotEnoughCoins,
    NotEnoughItems,
    NoRoom           // the hotbar has no space for what was bought
}

// The scheming merchant at the market stall. He sells seeds and building supplies and buys crops,
// at prices (MerchantPrices) that change every day. Prices here are from the player's side:
//   PriceToBuy  - what the player pays him for one.
//   PriceToSell - what he pays the player for one.
// Every trade goes through the shared Inventory and is all or nothing: coins are only taken if the
// goods fit in the hotbar, and crops are only taken if he pays for them.
// Look at him and press Interact to open the trading window (MerchantShopUI).
public class Merchant : MonoBehaviour, IInteractable
{
    [Tooltip("The scene's shared Inventory. Coins and goods move in and out of it.")]
    [SerializeField] private Inventory inventory;

    [SerializeField] private MerchantPrices prices;

    [Tooltip("The day/night cycle. Prices change when a new day starts. Without it, every day is day 1.")]
    [SerializeField] private EnvironmentCycle dayCycle;

    [Tooltip("The trading window. Talking to the merchant opens it.")]
    [SerializeField] private MerchantShopUI shop;

    public Inventory Inventory => inventory;
    public MerchantPrices Prices => prices;

    // Today's day number, starting at 1.
    public int Day => dayCycle != null ? dayCycle.Day : 1;

    // Raised after every trade that went through: (item, amount, the player bought it).
    public event Action<ResourceType, int, bool> Traded;

    private void Awake()
    {
        if (inventory == null || prices == null)
        {
            Debug.LogError($"{name} needs the Inventory and a price list assigned.", this);
            enabled = false;
        }
    }

    // ---------- Talking to him ----------

    public bool CanInteract => shop != null && !shop.IsOpen;

    public string GetPrompt() => shop != null ? "Trade with the merchant" : null;

    public void Interact(PlayerInteractor player) => shop.Open(this);

    // ---------- Trading ----------

    public int PriceToBuy(ResourceType item) => prices.ChargeFor(item, Day);

    public int PriceToSell(ResourceType item) => prices.PayFor(item, Day);

    // The player buys from the merchant.
    public TradeResult TryBuy(ResourceType item, int amount)
    {
        if (amount <= 0)
        {
            return TradeResult.InvalidAmount;
        }
        int price = PriceToBuy(item);
        if (price == 0)
        {
            return TradeResult.NotForSale;
        }
        long cost = (long)price * amount;
        if (cost > int.MaxValue || !inventory.CanAfford(ResourceType.Coins, (int)cost))
        {
            return TradeResult.NotEnoughCoins;
        }
        if (!inventory.CanAdd(item, amount))
        {
            return TradeResult.NoRoom;
        }

        inventory.TrySpend(ResourceType.Coins, (int)cost);
        inventory.Add(item, amount);
        Traded?.Invoke(item, amount, true);
        return TradeResult.Done;
    }

    // The player sells to the merchant.
    public TradeResult TrySell(ResourceType item, int amount)
    {
        if (amount <= 0)
        {
            return TradeResult.InvalidAmount;
        }
        int price = PriceToSell(item);
        if (price == 0)
        {
            return TradeResult.NotWanted;
        }
        if (!inventory.CanAfford(item, amount))
        {
            return TradeResult.NotEnoughItems;
        }
        long earned = (long)price * amount;
        if (earned > int.MaxValue || !inventory.CanAdd(ResourceType.Coins, (int)earned))
        {
            return TradeResult.NoRoom;
        }

        inventory.TrySpend(item, amount);
        inventory.Add(ResourceType.Coins, (int)earned);
        Traded?.Invoke(item, amount, false);
        return TradeResult.Done;
    }
}
