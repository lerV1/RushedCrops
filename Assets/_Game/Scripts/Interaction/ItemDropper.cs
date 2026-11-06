using UnityEngine;
using UnityEngine.InputSystem;

// Backspace drops one of the selected item in front of the player; Shift + Backspace drops the whole stack.
[RequireComponent(typeof(PlayerTools))]
public class ItemDropper : MonoBehaviour
{
    [Tooltip("The camera the player looks through. Items are thrown the way it faces.")]
    [SerializeField] private Transform viewPoint;
    [SerializeField] private ItemDrop dropPrefab;
    [Tooltip("How far in front of the player, in metres, a dropped item lands.")]
    [SerializeField, Min(0.5f)] private float throwDistance = 1.8f;
    [Tooltip("Seconds before walking into a dropped item picks it up again. Interact works straight away.")]
    [SerializeField, Min(0f)] private float walkPickupDelay = 1.5f;
    [Tooltip("Things that stop a throw, like walls, so items don't land behind them.")]
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;

    private PlayerTools tools;
    private Inventory inventory;

    private void Awake()
    {
        tools = GetComponent<PlayerTools>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !tools.isActiveAndEnabled || !keyboard.backspaceKey.wasPressedThisFrame)
        {
            return;
        }
        Drop(keyboard.shiftKey.isPressed);
    }

    public ItemDrop Drop(bool wholeStack)
    {
        if (inventory == null)
        {
            inventory = tools.Inventory;
        }
        if (inventory == null || dropPrefab == null || viewPoint == null)
        {
            Debug.LogError("ItemDropper needs the Inventory (on PlayerTools), the drop prefab and the camera.", this);
            return null;
        }
        if (!inventory.TryGetSelectedItem(out ResourceType item))
        {
            return null;
        }

        int amount = wholeStack ? inventory.GetSlotCount(inventory.SelectedSlot) : 1;
        // The selected slot is spent first, so this takes the items from the slot in hand.
        if (!inventory.TrySpend(item, amount))
        {
            return null;
        }

        Vector3 flat = new Vector3(viewPoint.forward.x, 0f, viewPoint.forward.z);
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : transform.forward;
        float distance = throwDistance;
        // Land short of a wall instead of behind it. The player's own colliders don't count.
        Vector3 chest = transform.position + Vector3.up * 0.5f;
        foreach (RaycastHit hit in Physics.RaycastAll(chest, flat, throwDistance, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
            {
                distance = Mathf.Min(distance, Mathf.Max(hit.distance - 0.4f, 0.3f));
            }
        }

        Vector3 start = viewPoint.position + flat * 0.5f - Vector3.up * 0.3f;
        ItemDrop drop = Instantiate(dropPrefab, start, Quaternion.identity);
        drop.Launch(inventory, item, amount, transform.position + flat * distance, walkPickupDelay);
        return drop;
    }
}
