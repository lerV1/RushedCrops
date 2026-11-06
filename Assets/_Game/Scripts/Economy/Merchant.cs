using System;
using UnityEngine;

// The result of a trade. Anything but Done means nothing changed.
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

// The merchant at the market stall: he sells seeds and supplies and buys crops at daily prices.
// Trades are all or nothing. Look at him and press Interact to open the trading window.
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

    public int Day => dayCycle != null ? dayCycle.Day : 1;

    // Raised after every trade that went through: (item, amount, the player bought it).
    public event Action<ResourceType, int, bool> Traded;

    public event Action<TradeResult> TradeRefused;

    private void Awake()
    {
        if (inventory == null || prices == null)
        {
            Debug.LogError($"{name} needs the Inventory and a price list assigned.", this);
            enabled = false;
        }
    }

    public bool CanInteract => shop != null && !shop.IsOpen;

    public string GetPrompt() => shop != null ? "Trade with the merchant" : null;

    public void Interact(PlayerInteractor player) => shop.Open(this);

    public int PriceToBuy(ResourceType item) => prices.ChargeFor(item, Day);

    public int PriceToSell(ResourceType item) => prices.PayFor(item, Day);

    // The player buys from the merchant.
    public TradeResult TryBuy(ResourceType item, int amount) => Report(Buy(item, amount));

    // The player sells to the merchant.
    public TradeResult TrySell(ResourceType item, int amount) => Report(Sell(item, amount));

    private TradeResult Report(TradeResult result)
    {
        if (result != TradeResult.Done)
        {
            TradeRefused?.Invoke(result);
        }
        return result;
    }

    private TradeResult Buy(ResourceType item, int amount)
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

    private TradeResult Sell(ResourceType item, int amount)
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
