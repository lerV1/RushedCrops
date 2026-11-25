using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// What the player holds: the tool in the selected hotbar slot, used with Use Tool on whatever is aimed at.
// Empty hands punch and the watering can pours. Every other tool does its own work (the hoe tills, the hatchet chops,
// the pickaxe mines, the jar catches a weakened boss) and is also a weapon against mini crops and bosses.
public class PlayerTools : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference useAction;
    [SerializeField] private InputActionReference[] slotActions = new InputActionReference[Inventory.SlotCount];

    [Header("References")]
    [Tooltip("The scene's shared Inventory. Its selected hotbar slot is what the player holds.")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private HandAnimator hands;
    [Tooltip("The swing used when the hatchet is in hand.")]
    [SerializeField] private MeleeAttack melee;

    [Header("Timing")]
    [Tooltip("Seconds into the hoe swing when the blade hits the ground.")]
    [SerializeField, Min(0f)] private float hoeHitDelay = 0.3f;
    [Tooltip("Seconds into the hatchet or pickaxe swing when the head hits a tree or rock.")]
    [SerializeField, Min(0f)] private float swingHitDelay = 0.3f;
    [Tooltip("Seconds into the pour when the water reaches the ground.")]
    [SerializeField, Min(0f)] private float pourDelay = 0.4f;

    [Header("Watering can")]
    [Tooltip("How many squares one full can waters. It starts empty; fill it at the river.")]
    [SerializeField, Min(1)] private int canCapacity = 6;

    public ToolType Equipped { get; private set; } = ToolType.None;
    // The item shown in the hand when it isn't a tool; -1 means empty hands.
    public ResourceType HeldItem { get; private set; } = (ResourceType)(-1);

    public Inventory Inventory => inventory;

    public int WaterLeft { get; private set; }
    public int CanCapacity => canCapacity;

    public event Action Changed;

    private void OnEnable()
    {
        if (useAction == null || inventory == null || interactor == null || hands == null || melee == null)
        {
            Debug.LogError("PlayerTools needs the Use Tool action, the Inventory, the PlayerInteractor, the HandAnimator and the MeleeAttack assigned.", this);
            enabled = false;
            return;
        }

        if (slotActions == null || slotActions.Length != Inventory.SlotCount)
        {
            Debug.LogError("PlayerTools needs one Select Slot action per hotbar slot.", this);
            enabled = false;
            return;
        }
        foreach (InputActionReference action in slotActions)
        {
            if (action == null)
            {
                Debug.LogError("PlayerTools has a missing Select Slot action.", this);
                enabled = false;
                return;
            }
        }
        foreach (InputActionReference action in slotActions) action.action.Enable();
        useAction.action.Enable();
        useAction.action.performed += OnUse;
    }

    private void OnDisable()
    {
        // Death disables tools; a pending harvest or watering action must stop too.
        StopAllCoroutines();
        if (slotActions != null)
        {
            foreach (InputActionReference action in slotActions)
                if (action != null) action.action.Disable();
        }
        if (useAction != null)
        {
            useAction.action.performed -= OnUse;
        }
    }

    private void Update()
    {
        ReadSlotActions();
        HoldSelectedTool();
    }

    private void ReadSlotActions()
    {
        for (int i = 0; i < slotActions.Length; i++)
        {
            if (slotActions[i].action.WasPressedThisFrame()) inventory.SelectSlot(i);
        }
        // The mouse wheel steps through the hotbar too, wrapping round at either end.
        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            int step = wheel > 0f ? -1 : 1;
            inventory.SelectSlot((inventory.SelectedSlot + step + Inventory.SlotCount) % Inventory.SlotCount);
        }
    }

    // While the arm is mid-swing the switch waits for the next frame.
    private void HoldSelectedTool()
    {
        // Finish the whole swing, recovery included, before the weapon can be put away.
        if (melee.IsBusy)
        {
            return;
        }

        bool holding = inventory.TryGetSelectedItem(out ResourceType item);
        ToolType wanted = holding ? ResourceTypes.ToTool(item) : ToolType.None;
        ResourceType held = holding && wanted == ToolType.None ? item : (ResourceType)(-1);
        if ((wanted != Equipped || held != HeldItem) && hands.SwitchHeld(wanted, held))
        {
            Equipped = wanted;
            HeldItem = held;
            Changed?.Invoke();
        }
    }

    public bool Has(ToolType tool) => tool == ToolType.None || inventory.GetCount(ResourceTypes.FromTool(tool)) > 0;

    public bool GiveTool(ToolType tool)
    {
        if (Has(tool))
        {
            return false;
        }
        return inventory.Add(ResourceTypes.FromTool(tool), 1);
    }

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
        GameAudio.Play2D(Sfx.FillCan);
        Changed?.Invoke();
    }

    public void RestoreWater(int amount)
    {
        WaterLeft = Mathf.Clamp(amount, 0, canCapacity);
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
                UseOrStrike(hoeHitDelay);
                break;
            case ToolType.WateringCan:
                // Water only runs out when it goes on something; filling at the river just dips the can.
                Component target = interactor.FindToolTarget();
                bool watering = WaterLeft > 0 && target != null && !(target is WaterSource) && ToolTargets.CanUse(target, this);
                hands.PlayPour(watering);
                UseOnTarget(pourDelay);
                break;
            case ToolType.Hatchet:
            case ToolType.Pickaxe:
                UseOrStrike(swingHitDelay);
                break;
            case ToolType.Sword:
                Strike();
                break;
            case ToolType.CaptureJar:
                // A boss worn down far enough is caught in the jar; anything else just gets hit with it.
                interactor.FindToolTarget();
                if (interactor.Target is CropBoss boss && boss.TryCapture())
                {
                    hands.PlaySwing();
                }
                else
                {
                    Strike();
                }
                break;
        }
    }

    // Every tool but the watering can is also a weapon. Aimed at a mini crop or a boss it hits it; aimed at its
    // own work (soil for the hoe, a tree for the hatchet, a rock for the pickaxe) it does that work; at
    // anything else it swings and hits whatever is in front.
    private void UseOrStrike(float delay)
    {
        Component target = interactor.FindToolTarget();
        if (!AimingAtFoe() && target != null && ToolTargets.CanUse(target, this))
        {
            hands.PlaySwing();
            StartCoroutine(UseAfter(target, delay));
        }
        else
        {
            Strike();
        }
    }

    private void Strike()
    {
        if (melee.TryAttack())
        {
            hands.PlaySwing();
        }
    }

    // A living mini crop or boss under the crosshair: anything with Health that isn't the player.
    private bool AimingAtFoe()
    {
        Collider looked = interactor.LookedAt;
        Health foe = looked != null ? looked.GetComponentInParent<Health>() : null;
        return foe != null && !foe.IsDead && !foe.transform.IsChildOf(transform);
    }

    private void UseOnTarget(float delay)
    {
        Component target = interactor.FindToolTarget();
        if (target != null && ToolTargets.CanUse(target, this))
        {
            StartCoroutine(UseAfter(target, delay));
        }
    }

    private IEnumerator UseAfter(Component target, float delay)
    {
        yield return new WaitForSeconds(delay);
        // Check again: the target could have been removed, or changed, during the swing.
        if (target != null && ToolTargets.CanUse(target, this))
        {
            ToolTargets.Use(target, this);
        }
    }
}
