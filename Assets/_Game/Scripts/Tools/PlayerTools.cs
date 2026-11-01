using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// The tools the player has picked up and the one in their hand.
//   Switch Tool (F / Button North) cycles: empty hands -> hoe -> watering can -> empty hands,
//   skipping tools the player doesn't own yet.
//   Use Tool (left click / right trigger) uses the tool in hand on whatever is under the crosshair.
//   With empty hands it throws a punch.
// Sits on the Player, next to PlayerInteractor.
public class PlayerTools : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference useAction;
    [SerializeField] private InputActionReference switchAction;

    [Header("References")]
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

    private readonly bool[] owned = new bool[Enum.GetValues(typeof(ToolType)).Length];

    public ToolType Equipped { get; private set; } = ToolType.None;

    // Waterings left in the can.
    public int WaterLeft { get; private set; }
    public int CanCapacity => canCapacity;

    // Raised when a tool is picked up, the tool in hand changes or the can's water changes.
    public event Action Changed;

    private void OnEnable()
    {
        if (useAction == null || switchAction == null || interactor == null || hands == null)
        {
            Debug.LogError("PlayerTools needs its input actions, the PlayerInteractor and the HandAnimator assigned.", this);
            enabled = false;
            return;
        }

        useAction.action.Enable();
        switchAction.action.Enable();
        useAction.action.performed += OnUse;
        switchAction.action.performed += OnSwitch;
    }

    private void OnDisable()
    {
        if (useAction != null)
        {
            useAction.action.performed -= OnUse;
        }
        if (switchAction != null)
        {
            switchAction.action.performed -= OnSwitch;
        }
    }

    public bool Has(ToolType tool) => tool == ToolType.None || owned[(int)tool];

    // Called by a ToolPickup. A tool picked up with empty hands goes straight into the hand.
    public void GiveTool(ToolType tool)
    {
        if (tool == ToolType.None || owned[(int)tool])
        {
            return;
        }

        owned[(int)tool] = true;
        Changed?.Invoke();
        if (Equipped == ToolType.None)
        {
            Equip(tool);
        }
    }

    // The next owned tool after the one in hand, wrapping round to empty hands.
    public void CycleTool()
    {
        int count = owned.Length;
        for (int step = 1; step <= count; step++)
        {
            var next = (ToolType)(((int)Equipped + step) % count);
            if (Has(next))
            {
                Equip(next);
                return;
            }
        }
    }

    // Puts a tool in hand. Returns false if the player doesn't own it or the arm is busy.
    public bool Equip(ToolType tool)
    {
        if (!Has(tool) || tool == Equipped || !hands.SwitchTool(tool))
        {
            return false;
        }

        Equipped = tool;
        Changed?.Invoke();
        return true;
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

    private void OnSwitch(InputAction.CallbackContext context) => CycleTool();

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
                hands.PlayPour();
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
