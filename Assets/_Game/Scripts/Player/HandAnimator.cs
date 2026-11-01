using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Plays the first-person arm animations. Each arm has its own Animator:
//   right arm (AC_HandRight): holds the tool in hand, swings it, or punches with empty hands.
//   left arm  (AC_HandLeft):  reaches out and grabs when the player presses Interact.
// PlayerTools decides what the right arm does; this script only animates and shows the right tool.
// Changing tools drops the arm below the screen, swaps the tool model while it's out of view,
// and brings the arm back up.
public class HandAnimator : MonoBehaviour
{
    private static readonly int EquippedParam = Animator.StringToHash("Equipped");
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int SwapTrigger = Animator.StringToHash("Swap");
    private static readonly int InteractTrigger = Animator.StringToHash("Interact");
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int GripIdleState = Animator.StringToHash("GripIdle");

    [Header("Arms")]
    [SerializeField] private Animator rightArm;
    [SerializeField] private Animator leftArm;

    [Header("Tools")]
    [Tooltip("The tool models in the right hand, in ToolType order. Leave element 0 (None) empty.")]
    [SerializeField] private GameObject[] toolModels = new GameObject[3];
    [Tooltip("Seconds into the equip, unequip and swap animations when the arm is below the screen.")]
    [SerializeField, Min(0f)] private float toolSwapDelay = 0.3f;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    private ToolType shownTool = ToolType.None;
    private Coroutine toolSwap;

    public bool IsRightArmResting => IsResting(rightArm);

    private void Awake()
    {
        if (rightArm == null || leftArm == null)
        {
            Debug.LogError("HandAnimator needs both arm Animators assigned.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        // Start with empty hands.
        rightArm.SetBool(EquippedParam, false);
        rightArm.Play(IdleState, 0, 0f);
        ShowTool(ToolType.None);
    }

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteract;
        }
    }

    private void OnDisable()
    {
        // Only unsubscribe. Interact is shared with PlayerInteractor, so the action stays enabled.
        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteract;
        }
    }

    // Swings the tool in hand (the arm's state picks the swing), or punches with empty hands.
    public void PlaySwing() => PlayRight(AttackTrigger);

    public void PlayPunch() => PlayRight(AttackTrigger);

    public void PlayInteract()
    {
        if (IsResting(leftArm))
        {
            leftArm.SetTrigger(InteractTrigger);
        }
    }

    // Starts the animation for changing what is in the right hand. Returns false if the arm is busy.
    public bool SwitchTool(ToolType next)
    {
        if (next == shownTool || !IsResting(rightArm))
        {
            return false;
        }

        if (shownTool == ToolType.None)
        {
            rightArm.SetBool(EquippedParam, true);      // open hand -> gripping
        }
        else if (next == ToolType.None)
        {
            rightArm.SetBool(EquippedParam, false);     // gripping -> open hand
        }
        else
        {
            rightArm.SetTrigger(SwapTrigger);           // one tool for another
        }

        shownTool = next;
        if (toolSwap != null)
        {
            StopCoroutine(toolSwap);
        }
        toolSwap = StartCoroutine(ShowToolWhileHidden(next));
        return true;
    }

    private IEnumerator ShowToolWhileHidden(ToolType tool)
    {
        yield return new WaitForSeconds(toolSwapDelay);
        ShowTool(tool);
        toolSwap = null;
    }

    private void ShowTool(ToolType tool)
    {
        for (int i = 0; i < toolModels.Length; i++)
        {
            if (toolModels[i] != null)
            {
                toolModels[i].SetActive(i == (int)tool);
            }
        }
    }

    private void PlayRight(int trigger)
    {
        if (IsResting(rightArm))
        {
            rightArm.SetTrigger(trigger);
        }
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

    private void OnInteract(InputAction.CallbackContext context) => PlayInteract();
}
