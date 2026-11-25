using UnityEngine;
using UnityEngine.UI;

// Connects one merchant row button to its item and trade action.
public class MerchantTradeButton : MonoBehaviour
{
    private Button button;
    private MerchantShopUI shop;
    private ResourceType item;
    private bool playerSells;
    private bool more;
    private bool configured;
    private bool listenerAdded;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void Configure(MerchantShopUI tradingWindow, ResourceType resource, bool isPlayerSelling, bool isMoreButton)
    {
        shop = tradingWindow;
        item = resource;
        playerSells = isPlayerSelling;
        more = isMoreButton;
        configured = true;
        AddListener();
    }

    private void OnEnable()
    {
        AddListener();
    }

    private void OnDisable()
    {
        RemoveListener();
    }

    private void OnDestroy()
    {
        RemoveListener();
    }

    private void AddListener()
    {
        if (!configured || listenerAdded || button == null)
        {
            return;
        }

        button.onClick.AddListener(OnClick);
        listenerAdded = true;
    }

    private void RemoveListener()
    {
        if (!listenerAdded)
        {
            return;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
        listenerAdded = false;
    }

    private void OnClick()
    {
        if (shop != null)
        {
            shop.TradeItem(item, playerSells, more);
        }
    }
}
