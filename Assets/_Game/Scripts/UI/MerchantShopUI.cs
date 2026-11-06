using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The merchant's trading window: crops he buys on the left, what he sells on the right, with today's prices.
// While it is open the player can't move and the cursor is free. Esc, E or the gamepad's East button closes it.
public class MerchantShopUI : MonoBehaviour
{
    [Header("Window")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Text title;
    [SerializeField] private Text coins;
    [Tooltip("The result of the last click, like \"Sold 4 Carrots for 16 coins\".")]
    [SerializeField] private Text message;
    [Tooltip("What the merchant says while the window is open. Optional.")]
    [SerializeField] private Text remark;
    [SerializeField] private Button closeButton;

    [Header("Rows")]
    [Tooltip("Where the crops he buys are listed.")]
    [SerializeField] private RectTransform sellList;
    [Tooltip("Where the things he sells are listed.")]
    [SerializeField] private RectTransform buyList;
    [Tooltip("A hidden row that is copied once per item: Name, Info, Button1 and Button2 children.")]
    [SerializeField] private GameObject rowTemplate;

    [Header("Player")]
    [Tooltip("Player scripts switched off while the window is open (movement, looking, tools...).")]
    [SerializeField] private Behaviour[] pauseWhileOpen = new Behaviour[0];

    private readonly List<Row> rows = new List<Row>();
    private Merchant merchant;
    private int openedOnFrame;
    private int shownDay;

    public bool IsOpen => panel != null && panel.activeSelf;
    public Merchant Merchant => merchant;

    public event System.Action<Merchant> Opened;
    public event System.Action<Merchant> Closed;

    private class Row
    {
        public ResourceType item;
        public bool playerSells;
        public Text info;
        public Button one;
        public Button more;
    }

    private void Awake()
    {
        if (panel == null || rowTemplate == null || sellList == null || buyList == null)
        {
            Debug.LogError("MerchantShopUI needs its panel, lists and row template assigned.", this);
            enabled = false;
            return;
        }
        rowTemplate.SetActive(false);
        panel.SetActive(false);
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // The key press that opened the window must not close it again in the same frame.
        if (Time.frameCount > openedOnFrame && ClosePressed())
        {
            Close();
            return;
        }

        // Prices change at midnight even while the window is open.
        if (merchant.Day != shownDay)
        {
            Refresh();
        }
    }

    public void Open(Merchant trader)
    {
        if (IsOpen || trader == null)
        {
            return;
        }

        merchant = trader;
        openedOnFrame = Time.frameCount;
        BuildRows();
        merchant.Inventory.Changed += Refresh;
        message.text = "";
        ShowRemark("");
        panel.SetActive(true);
        SetPlayerPaused(true);
        Refresh();
        Opened?.Invoke(merchant);

        // Gamepad players start on the first button so they can move round the window.
        if (EventSystem.current != null && rows.Count > 0)
        {
            EventSystem.current.SetSelectedGameObject(rows[0].one.gameObject);
        }
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        merchant.Inventory.Changed -= Refresh;
        panel.SetActive(false);
        SetPlayerPaused(false);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        Closed?.Invoke(merchant);
    }

    public void ShowRemark(string text)
    {
        if (remark != null)
        {
            remark.text = string.IsNullOrEmpty(text) ? "" : $"\"{text}\"";
        }
    }

    public void ShowMessage(string text)
    {
        if (message != null)
        {
            message.text = text;
        }
    }

    private void SetPlayerPaused(bool paused)
    {
        // Turning PlayerLook off also frees the mouse cursor; turning it back on locks it again.
        foreach (Behaviour behaviour in pauseWhileOpen)
        {
            if (behaviour != null)
            {
                behaviour.enabled = !paused;
            }
        }
    }

    private static bool ClosePressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        return (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
            || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
    }

    private void BuildRows()
    {
        foreach (Row row in rows)
        {
            Destroy(row.info.transform.parent.gameObject);
        }
        rows.Clear();

        foreach (MerchantPrices.Offer offer in merchant.Prices.Wanted)
        {
            AddRow(sellList, offer.item, true);
        }
        foreach (MerchantPrices.Offer offer in merchant.Prices.ForSale)
        {
            AddRow(buyList, offer.item, false);
        }
    }

    private void AddRow(RectTransform list, ResourceType item, bool playerSells)
    {
        GameObject copy = Instantiate(rowTemplate, list);
        copy.name = ResourcesName(item);
        copy.SetActive(true);
        var row = new Row
        {
            item = item,
            playerSells = playerSells,
            info = copy.transform.Find("Info").GetComponent<Text>(),
            one = copy.transform.Find("Button1").GetComponent<Button>(),
            more = copy.transform.Find("Button2").GetComponent<Button>(),
        };
        copy.transform.Find("Name").GetComponent<Text>().text = ResourcesName(item);
        row.one.GetComponentInChildren<Text>().text = playerSells ? "Sell 1" : "Buy 1";
        row.more.GetComponentInChildren<Text>().text = playerSells ? "Sell all" : "Buy 5";
        row.one.onClick.AddListener(() => Trade(row, 1));
        row.more.onClick.AddListener(() => Trade(row, playerSells ? merchant.Inventory.GetCount(item) : 5));
        rows.Add(row);
    }

    private static string ResourcesName(ResourceType item) => ResourceTypes.DisplayName(item);

    private void Trade(Row row, int amount)
    {
        TradeResult result;
        int price;
        if (row.playerSells)
        {
            price = merchant.PriceToSell(row.item);
            result = merchant.TrySell(row.item, amount);
        }
        else
        {
            price = merchant.PriceToBuy(row.item);
            result = merchant.TryBuy(row.item, amount);
        }
        ShowMessage(Describe(result, row, amount, price * amount));
        Refresh();
    }

    private static string Describe(TradeResult result, Row row, int amount, int total)
    {
        string item = ResourcesName(row.item);
        switch (result)
        {
            case TradeResult.Done:
                return row.playerSells ? $"Sold {amount} {item} for {total} coins." : $"Bought {amount} {item} for {total} coins.";
            case TradeResult.NotEnoughCoins:
                return $"Not enough coins: {amount} {item} cost {total}.";
            case TradeResult.NoRoom:
                return "Your hotbar has no room for that.";
            case TradeResult.NotEnoughItems:
            case TradeResult.InvalidAmount:
                return $"You have no {item} to sell.";
            default:
                return "He won't trade that.";
        }
    }

    private void Refresh()
    {
        if (merchant == null)
        {
            return;
        }

        shownDay = merchant.Day;
        title.text = $"Merchant's stall - day {shownDay} prices";
        coins.text = $"Coins: {merchant.Inventory.GetCount(ResourceType.Coins)}";
        int wallet = merchant.Inventory.GetCount(ResourceType.Coins);
        foreach (Row row in rows)
        {
            if (row.playerSells)
            {
                int have = merchant.Inventory.GetCount(row.item);
                row.info.text = $"have {have}  |  {merchant.PriceToSell(row.item)} each";
                row.one.interactable = have > 0;
                row.more.interactable = have > 0;
            }
            else
            {
                int price = merchant.PriceToBuy(row.item);
                row.info.text = $"{price} each";
                row.one.interactable = wallet >= price;
                row.more.interactable = wallet >= price * 5;
            }
        }
    }
}
