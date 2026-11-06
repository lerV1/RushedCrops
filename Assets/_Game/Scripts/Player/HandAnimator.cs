using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Plays the first-person arm animations: the right arm holds, swings and pours the tool in hand (or punches),
// the left arm grabs on Interact. Changing tools drops the arm out of view to swap the model.
public class HandAnimator : MonoBehaviour
{
    private static readonly int EquippedParam = Animator.StringToHash("Equipped");
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int SwapTrigger = Animator.StringToHash("Swap");
    private static readonly int PourTrigger = Animator.StringToHash("Pour");
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

    [Header("Watering can")]
    [Tooltip("Drops falling from the can's spout while it pours.")]
    [SerializeField] private ParticleSystem waterStream;
    [Tooltip("Seconds into the pour animation when the can is tipped far enough for water to come out.")]
    [SerializeField, Min(0f)] private float pourStart = 0.27f;
    [Tooltip("How long the water runs.")]
    [SerializeField, Min(0f)] private float pourLength = 0.5f;

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

    public void PlaySwing() => PlayRight(AttackTrigger);

    public void PlayPunch() => PlayRight(AttackTrigger);

    public void PlayPour(bool withWater)
    {
        if (!IsResting(rightArm))
        {
            return;
        }
        rightArm.SetTrigger(PourTrigger);
        if (withWater && waterStream != null)
        {
            StartCoroutine(RunWater());
        }
    }

    private IEnumerator RunWater()
    {
        yield return new WaitForSeconds(pourStart);
        waterStream.Play();
        yield return new WaitForSeconds(pourLength);
        waterStream.Stop(); // drops already falling finish their fall
    }

    public void PlayInteract()
    {
        if (IsResting(leftArm))
        {
            leftArm.SetTrigger(InteractTrigger);
        }
    }

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
