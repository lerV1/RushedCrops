using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// What the player holds, chosen from the hotbar.
//   Keys 1-5 select a hotbar slot. A tool in that slot goes into the hand;
//   anything else (seeds, crops, an empty slot) leaves the hand empty.
//   Use Tool (left click / right trigger) uses the tool in hand on whatever is under the crosshair.
//   With empty hands it throws a punch.
// Tools live in the Inventory's hotbar like every other item; this script only reads them.
// Sits on the Player, next to PlayerInteractor.
public class PlayerTools : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference useAction;

    [Header("References")]
    [Tooltip("The scene's shared Inventory. Its selected hotbar slot is what the player holds.")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private HandAnimator hands;

    [Header("Timing")]
    [Tooltip("Seconds into the hoe swing when the blade hits the ground.")]
    [SerializeField, Min(0f)] private float hoeHitDelay = 0.3f;
    [Tooltip("Seconds into the pour when the water reaches the ground.")]
    [SerializeField, Min(0f)] private float pourDelay = 0.4f;

    [Header("Watering can")]
    [Tooltip("How many squares one full can waters. It starts empty; fill it at the river.")]
    [SerializeField, Min(1)] private int canCapacity = 6;

    // Keys 1-5 pick hotbar slots 0-4.
    private static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

    public ToolType Equipped { get; private set; } = ToolType.None;

    public Inventory Inventory => inventory;

    // Waterings left in the can.
    public int WaterLeft { get; private set; }
    public int CanCapacity => canCapacity;

    // Raised when the tool in hand changes or the can's water changes.
    public event Action Changed;

    private void OnEnable()
    {
        if (useAction == null || inventory == null || interactor == null || hands == null)
        {
            Debug.LogError("PlayerTools needs the Use Tool action, the Inventory, the PlayerInteractor and the HandAnimator assigned.", this);
            enabled = false;
            return;
        }

        useAction.action.Enable();
        useAction.action.performed += OnUse;
    }

    private void OnDisable()
    {
        if (useAction != null)
        {
            useAction.action.performed -= OnUse;
        }
    }

    private void Update()
    {
        ReadSlotKeys();
        HoldSelectedTool();
    }

    private void ReadSlotKeys()
    {
        // Keyboard.current is null when no keyboard is connected.
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        for (int i = 0; i < SlotKeys.Length; i++)
        {
            if (keyboard[SlotKeys[i]].wasPressedThisFrame)
            {
                inventory.SelectSlot(i);
            }
        }
    }

    // Keeps the hand matching the selected slot. While the arm is busy (mid-swing)
    // the switch waits, and this tries again next frame.
    private void HoldSelectedTool()
    {
        ToolType wanted = inventory.TryGetSelectedItem(out ResourceType item) ? ResourceTypes.ToTool(item) : ToolType.None;
        if (wanted != Equipped && hands.SwitchTool(wanted))
        {
            Equipped = wanted;
            Changed?.Invoke();
        }
    }

    public bool Has(ToolType tool) => tool == ToolType.None || inventory.GetCount(ResourceTypes.FromTool(tool)) > 0;

    // Called by a ToolPickup. The tool goes into the hotbar like any other item.
    // Returns false if the player already has it or the hotbar is full.
    public bool GiveTool(ToolType tool)
    {
        if (Has(tool))
        {
            return false;
        }
        return inventory.Add(ResourceTypes.FromTool(tool), 1);
    }

    // Takes one watering from the can. Returns false if it is empty.
    public bool UseWater()
    {
        if (WaterLeft <= 0)
        {
            return false;
        }
        WaterLeft--;
        Changed?.Invoke();
        return true;
    }

    public void FillCan()
    {
        WaterLeft = canCapacity;
        Changed?.Invoke();
    }

    private void OnUse(InputAction.CallbackContext context)
    {
        // One action at a time, so mashing the button doesn't stack swings.
        if (!hands.IsRightArmResting)
        {
            return;
        }

        switch (Equipped)
        {
            case ToolType.None:
                hands.PlayPunch();
                break;
            case ToolType.Hoe:
                hands.PlaySwing();
                UseOnTarget(hoeHitDelay);
                break;
            case ToolType.WateringCan:
                // Water only runs out when it goes on something; filling at the river just dips the can.
                IToolTarget target = interactor.FindToolTarget();
                bool watering = WaterLeft > 0 && target != null && !(target is WaterSource) && target.CanUseTool(this);
                hands.PlayPour(watering);
                UseOnTarget(pourDelay);
                break;
        }
    }

    // If the crosshair is on something this tool works on, use it when the tool gets there.
    private void UseOnTarget(float delay)
    {
        IToolTarget target = interactor.FindToolTarget();
        if (target != null && target.CanUseTool(this))
        {
            StartCoroutine(UseAfter(target, delay));
        }
    }

    private IEnumerator UseAfter(IToolTarget target, float delay)
    {
        yield return new WaitForSeconds(delay);
        // Check again: the target could have been removed, or changed, during the swing.
        if (target as UnityEngine.Object != null && target.CanUseTool(this))
        {
            target.UseTool(this);
        }
    }
}
