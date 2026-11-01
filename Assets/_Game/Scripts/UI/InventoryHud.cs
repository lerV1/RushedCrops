using System;
using UnityEngine;
using UnityEngine.UI;

// The player's on-screen inventory: a coin counter and the 5-slot hotbar.
// Each slot shows what is in the matching Inventory slot (name and count), or nothing
// when it is empty. The selected slot lights up and its item's name shows above the hotbar.
//
// It only redraws when the Inventory raises Changed, instead of every frame.
public class InventoryHud : MonoBehaviour
{
    // The UI for one hotbar slot. Slot 1 in the list shows Inventory slot 1, and so on.
    // [Serializable] makes each slot appear as a foldout in the Inspector.
    [Serializable]
    private class HotbarSlot
    {
        public Image background;
        public Text nameText;
        public Text countText;
    }

    [Tooltip("The scene's shared Inventory.")]
    [SerializeField] private Inventory inventory;

    [Header("Coins")]
    [SerializeField] private Text coinsText;

    [Header("Hotbar")]
    [SerializeField] private HotbarSlot[] slots = new HotbarSlot[Inventory.SlotCount];
    [SerializeField] private Color normalColor = new Color(0.08f, 0.07f, 0.05f, 0.72f);
    [SerializeField] private Color selectedColor = new Color(0.62f, 0.43f, 0.12f, 0.92f);
    [Tooltip("Optional text above the hotbar with the name of the selected item.")]
    [SerializeField] private Text selectedItemText;

    private void OnEnable()
    {
        if (inventory == null)
        {
            Debug.LogError("InventoryHud needs the Inventory dragged into its Inventory field.", this);
            enabled = false;
            return;
        }

        // Listen for changes, then draw once so the HUD is right from the first frame.
        inventory.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        // Always unsubscribe what you subscribed, or the Inventory keeps calling a disabled HUD.
        if (inventory != null)
        {
            inventory.Changed -= Refresh;
        }
    }

    private void Refresh()
    {
        if (coinsText != null)
        {
            coinsText.text = inventory.GetCount(ResourceType.Coins).ToString();
        }

        int shown = Mathf.Min(slots.Length, Inventory.SlotCount);
        for (int i = 0; i < shown; i++)
        {
            if (slots[i] != null)
            {
                DrawSlot(slots[i], i);
            }
        }

        if (selectedItemText != null)
        {
            selectedItemText.text = inventory.TryGetSelectedItem(out ResourceType item)
                ? ResourceTypes.DisplayName(item)
                : "";
        }
    }

    private void DrawSlot(HotbarSlot slot, int index)
    {
        bool empty = inventory.IsSlotEmpty(index);
        ResourceType item = inventory.GetSlotItem(index);

        if (slot.background != null)
        {
            slot.background.color = index == inventory.SelectedSlot ? selectedColor : normalColor;
        }

        if (slot.nameText != null)
        {
            slot.nameText.text = empty ? "" : ResourceTypes.DisplayName(item);
        }

        if (slot.countText != null)
        {
            // Tools don't stack, so "1" under a hoe would only be noise.
            bool showCount = !empty && inventory.MaxStackFor(item) > 1;
            slot.countText.text = showCount ? inventory.GetSlotCount(index).ToString() : "";
        }
    }
}
