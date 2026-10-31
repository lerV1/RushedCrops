using UnityEngine;
using UnityEngine.InputSystem;

// Plays the first-person hand animations when the player presses Attack or Interact.
// The clips are in AC_Hands, the Animator Controller on HandsRoot. Gameplay scripts can
// also call PlayAttack or PlayInteract directly, for example when planting or harvesting.
[RequireComponent(typeof(Animator))]
public class HandAnimator : MonoBehaviour
{
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int InteractTrigger = Animator.StringToHash("Interact");
    private static readonly int IdleState = Animator.StringToHash("Idle");

    [Header("Input")]
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private InputActionReference interactAction;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        attackAction.action.Enable();
        interactAction.action.Enable();
        attackAction.action.performed += OnAttack;
        interactAction.action.performed += OnInteract;
    }

    private void OnDisable()
    {
        attackAction.action.performed -= OnAttack;
        interactAction.action.performed -= OnInteract;
        attackAction.action.Disable();
        interactAction.action.Disable();
    }

    public void PlayAttack() => Play(AttackTrigger);

    public void PlayInteract() => Play(InteractTrigger);

    private void OnAttack(InputAction.CallbackContext context) => PlayAttack();

    private void OnInteract(InputAction.CallbackContext context) => PlayInteract();

    private void Play(int trigger)
    {
        // One hand animation at a time, so mashing a button doesn't stack swings.
        if (IsBusy())
        {
            return;
        }

        animator.SetTrigger(trigger);
    }

    private bool IsBusy()
    {
        return animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash != IdleState;
    }
}
