using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// The player's on-screen inventory: a coin counter and a 5-slot hotbar.
// Each slot shows one resource from the shared Inventory. Pick which resource
// goes in which slot in the Inspector.
//
// It only redraws when the Inventory raises Changed, instead of every frame.
public class InventoryHud : MonoBehaviour
{
    // One hotbar slot: which resource it shows and the two texts it writes to.
    // [Serializable] makes each slot appear as a foldout in the Inspector.
    [Serializable]
    private class HotbarSlot
    {
        public ResourceType resource;
        public Text nameText;
        public Text countText;
    }

    [Tooltip("The scene's shared Inventory.")]
    [SerializeField] private Inventory inventory;

    [Header("Coins")]
    [SerializeField] private Text coinsText;

    [Header("Hotbar")]
    [SerializeField] private HotbarSlot[] slots = new HotbarSlot[5];
    [Tooltip("Count color when the player has none of that resource.")]
    [SerializeField] private Color emptyCountColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color countColor = Color.white;

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

        foreach (HotbarSlot slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            int count = inventory.GetCount(slot.resource);

            if (slot.nameText != null)
            {
                slot.nameText.text = DisplayName(slot.resource);
            }

            if (slot.countText != null)
            {
                slot.countText.text = count.ToString();
                slot.countText.color = count > 0 ? countColor : emptyCountColor;
            }
        }
    }

    // Turns "CarrotSeeds" into "Carrot Seeds" by adding a space before each capital letter.
    private static string DisplayName(ResourceType type)
    {
        string name = type.ToString();
        var result = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                result.Append(' ');
            }
            result.Append(name[i]);
        }
        return result.ToString();
    }
}
