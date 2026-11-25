using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Farm-only cheats, available in the editor and player with the C key.</summary>
public class CheatMenu : MonoBehaviour
{
    [SerializeField, Min(1)] private int coinGrant = 1000;

    private readonly ResourceType[] items = (ResourceType[])Enum.GetValues(typeof(ResourceType));
    private readonly int[] amounts = { 1, 10, 99 };
    private PlayerTools tools;
    private Inventory inventory;
    private Health health;
    private RespawnController respawn;
    private MerchantShopUI shop;
    private Behaviour[] controls;
    private bool[] previousEnabled;
    private GameObject panel;
    private Text itemLabel;
    private Text amountLabel;
    private Text message;
    private Font font;
    private int selectedItem = 1;
    private int selectedAmount;
    private bool isOpen;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // Remove first so entering Play Mode without domain reload never stacks callbacks.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SC_Farm") return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<CheatMenu>(true) != null) return;

        var menu = new GameObject("Cheat Menu", typeof(CheatMenu));
        SceneManager.MoveGameObjectToScene(menu, scene);
    }

    private void Start()
    {
        tools = FindFirstObjectByType<PlayerTools>();
        if (tools == null || tools.Inventory == null)
        {
            enabled = false;
            return;
        }
        inventory = tools.Inventory;
        health = tools.GetComponent<Health>();
        respawn = tools.GetComponent<RespawnController>();
        shop = FindFirstObjectByType<MerchantShopUI>();
        controls = new Behaviour[] { tools.GetComponent<PlayerController>(), tools.GetComponent<PlayerLook>(),
            tools.GetComponent<PlayerInteractor>(), tools, tools.GetComponent<MeleeAttack>() };
        BuildMenu();
    }

    private void Update()
    {
        if (Keyboard.current == null || panel == null) return;
        if (isOpen && (health == null || health.IsDead ||
            (respawn != null && respawn.IsAwaitingRespawn)))
        {
            Close();
            return;
        }
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (isOpen) Close();
            else Open();
        }
        else if (isOpen && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    private void Open()
    {
        // Another screen retains ownership of its controls and cursor.
        if (isOpen || panel == null || health == null || health.IsDead ||
            (respawn != null && respawn.IsAwaitingRespawn) ||
            (shop != null && shop.IsOpen) || Time.timeScale <= 0f) return;

        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        previousEnabled = new bool[controls.Length];
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] == null) continue;
            previousEnabled[i] = controls[i].enabled;
            controls[i].enabled = false;
        }
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isOpen = true;
        message.text = "";
        panel.SetActive(true);
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (panel != null) panel.SetActive(false);
        Time.timeScale = previousTimeScale;
        bool dead = health == null || health.IsDead || (respawn != null && respawn.IsAwaitingRespawn);
        if (controls != null && previousEnabled != null && !dead)
        {
            for (int i = 0; i < controls.Length; i++)
                if (controls[i] != null) controls[i].enabled = previousEnabled[i];
        }
        Cursor.lockState = dead ? CursorLockMode.None : previousCursorLock;
        Cursor.visible = dead || previousCursorVisible;
        previousEnabled = null;
    }

    private void OnDisable() => Close();

    private void GrantCoins()
    {
        if (!isOpen) return;
        message.text = inventory.Add(ResourceType.Coins, coinGrant)
            ? $"Added {coinGrant:N0} coins. Wallet: {inventory.GetCount(ResourceType.Coins):N0}"
            : "Wallet limit reached. No coins added.";
    }

    private void GrantItem()
    {
        if (!isOpen) return;
        ResourceType item = items[selectedItem];
        int count = ResourceTypes.IsTool(item) ? 1 : amounts[selectedAmount];
        message.text = inventory.Add(item, count)
            ? $"Added {count} {ResourceTypes.DisplayName(item)}."
            : "Not enough hotbar space. No items added.";
    }

    private void HealPlayer()
    {
        if (!isOpen || health == null || health.IsDead) return;
        health.Heal(health.MaxHealth - health.CurrentHealth);
        message.text = $"Health restored: {health.CurrentHealth}/{health.MaxHealth}.";
    }

    private void RefillWater()
    {
        if (!isOpen) return;
        tools.FillCan();
        message.text = $"Watering can refilled: {tools.WaterLeft}/{tools.CanCapacity}.";
    }

    private void SelectItem(int direction)
    {
        // Coins have their own button; every other resource can be selected here.
        selectedItem = 1 + (selectedItem - 1 + direction + items.Length - 1) % (items.Length - 1);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        itemLabel.text = ResourceTypes.DisplayName(items[selectedItem]);
        amountLabel.text = ResourceTypes.IsTool(items[selectedItem]) ? "1" : amounts[selectedAmount].ToString();
    }

    private void BuildMenu()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        var shade = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(transform, false);
        var shadeRect = shade.GetComponent<RectTransform>();
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
        shade.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        panel = shade;

        RectTransform card = Rect("Cheat panel", shade.transform, 0, 0, 460, 344);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.gameObject.AddComponent<Image>().color = Color.black;
        var title = Label(card, "Cheats", 24, 16, 320, 36, 24);
        title.alignment = TextAnchor.MiddleLeft;
        Button(card, "X", 392, 16, 44, 36, Close);
        Button(card, $"+{coinGrant:N0} coins", 24, 72, 412, 40, GrantCoins);
        Button(card, "Heal", 24, 124, 200, 40, HealPlayer);
        Button(card, "Refill water", 236, 124, 200, 40, RefillWater);
        Button(card, "<", 24, 184, 44, 40, () => SelectItem(-1));
        itemLabel = Label(card, "", 76, 184, 308, 40, 20);
        Button(card, ">", 392, 184, 44, 40, () => SelectItem(1));
        var amount = Button(card, "", 24, 236, 80, 40, () =>
        {
            selectedAmount = (selectedAmount + 1) % amounts.Length;
            UpdateSelection();
        });
        amountLabel = amount.GetComponentInChildren<Text>();
        Button(card, "Give item", 116, 236, 320, 40, GrantItem);
        message = Label(card, "", 24, 292, 412, 32, 15);
        UpdateSelection();
        panel.SetActive(false);
    }

    private RectTransform Rect(string objectName, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private Text Label(Transform parent, string text, float x, float y, float width, float height,
        int size, FontStyle style = FontStyle.Normal)
    {
        var label = Rect("Label", parent, x, y, width, height).gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        return label;
    }

    private Button Button(Transform parent, string title, float x, float y, float width, float height, UnityAction action)
    {
        var rect = Rect(title, parent, x, y, width, height);
        rect.gameObject.AddComponent<Image>().color = Color.white;
        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.selectedColor = Color.white;
        colors.highlightedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        button.colors = colors;
        button.onClick.AddListener(action);
        Label(rect, title, 0, 0, width, height, 18).color = Color.black;
        return button;
    }
}
