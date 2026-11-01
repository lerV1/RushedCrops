using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Plays the first-person arm animations. Each arm has its own Animator:
//   right arm (AC_HandRight): holds the tool. Attack swings it, or punches when the tool is put away.
//   left arm  (AC_HandLeft):  reaches out and grabs when the player presses Interact.
// Toggle Tool (F / Button North) puts the tool away or takes it out again. The arm drops below the
// screen, the tool is shown or hidden while it's out of view, and the arm comes back up.
// Gameplay scripts can also call PlayAttack, PlayInteract or SetEquipped directly.
public class HandAnimator : MonoBehaviour
{
    private static readonly int EquippedParam = Animator.StringToHash("Equipped");
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int InteractTrigger = Animator.StringToHash("Interact");
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int GripIdleState = Animator.StringToHash("GripIdle");

    [Header("Arms")]
    [SerializeField] private Animator rightArm;
    [SerializeField] private Animator leftArm;

    [Header("Tool")]
    [Tooltip("The tool in the right hand.")]
    [SerializeField] private GameObject tool;
    [SerializeField] private bool startEquipped = true;
    [Tooltip("Seconds into the equip and unequip animations when the arm is below the screen.")]
    [SerializeField, Min(0f)] private float toolSwapDelay = 0.3f;

    [Header("Input")]
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference toggleToolAction;

    private Coroutine toolSwap;

    // True while the right hand holds the tool (or is taking it out).
    public bool IsEquipped { get; private set; }

    private void Awake()
    {
        if (rightArm == null || leftArm == null || tool == null)
        {
            Debug.LogError("HandAnimator needs both arm Animators and the tool assigned.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        // Begin already holding (or not holding) the tool, without playing the equip animation.
        IsEquipped = startEquipped;
        rightArm.SetBool(EquippedParam, startEquipped);
        rightArm.Play(startEquipped ? GripIdleState : IdleState, 0, 0f);
        tool.SetActive(startEquipped);
    }

    private void OnEnable()
    {
        Subscribe(attackAction, OnAttack);
        Subscribe(interactAction, OnInteract);
        Subscribe(toggleToolAction, OnToggleTool);
    }

    private void OnDisable()
    {
        // Only unsubscribe. Interact is shared with PlayerInteractor, so the actions stay enabled.
        Unsubscribe(attackAction, OnAttack);
        Unsubscribe(interactAction, OnInteract);
        Unsubscribe(toggleToolAction, OnToggleTool);
    }

    public void PlayAttack()
    {
        // One swing at a time, so mashing the button doesn't stack animations.
        if (!IsResting(rightArm))
        {
            return;
        }
        rightArm.SetTrigger(AttackTrigger);
    }

    public void PlayInteract()
    {
        if (!IsResting(leftArm))
        {
            return;
        }
        leftArm.SetTrigger(InteractTrigger);
    }

    public void ToggleTool() => SetEquipped(!IsEquipped);

    public void SetEquipped(bool equipped)
    {
        if (equipped == IsEquipped || !IsResting(rightArm))
        {
            return;
        }

        IsEquipped = equipped;
        rightArm.SetBool(EquippedParam, equipped);
        if (toolSwap != null)
        {
            StopCoroutine(toolSwap);
        }
        toolSwap = StartCoroutine(SwapToolWhileHidden(equipped));
    }

    private IEnumerator SwapToolWhileHidden(bool show)
    {
        yield return new WaitForSeconds(toolSwapDelay);
        tool.SetActive(show);
        toolSwap = null;
    }

    // True when the arm is standing still in its idle loop (open hand or gripping).
    private static bool IsResting(Animator arm)
    {
        if (arm.IsInTransition(0))
        {
            return false;
        }
        int state = arm.GetCurrentAnimatorStateInfo(0).shortNameHash;
        return state == IdleState || state == GripIdleState;
    }

    private void OnAttack(InputAction.CallbackContext context) => PlayAttack();

    private void OnInteract(InputAction.CallbackContext context) => PlayInteract();

    private void OnToggleTool(InputAction.CallbackContext context) => ToggleTool();

    private static void Subscribe(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
    {
        if (reference == null)
        {
            return;
        }
        reference.action.Enable();
        reference.action.performed += handler;
    }

    private static void Unsubscribe(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
    {
        if (reference != null)
        {
            reference.action.performed -= handler;
        }
    }
}
