using UnityEngine;
using UnityEngine.InputSystem;

// Finds what the player is looking at and uses it when Interact is pressed.
// Sits on the Player. A ray goes out from the camera; the first collider it hits
// counts if it (or a parent) has a component that implements IInteractable.
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private InputActionReference interactAction;

    [Tooltip("The camera the player looks through. The look ray starts here.")]
    [SerializeField] private Transform viewPoint;

    [Tooltip("How far away, in metres, the player can use things.")]
    [SerializeField, Min(0.5f)] private float reach = 3f;

    [Tooltip("Layers the look ray can hit.")]
    [SerializeField] private LayerMask lookMask = ~0;

    [Tooltip("Shows the prompt under the crosshair. Optional.")]
    [SerializeField] private InteractionPromptHud prompt;

    // What the player is looking at this frame, or null.
    public IInteractable Target { get; private set; }

    private void OnEnable()
    {
        if (interactAction == null || viewPoint == null)
        {
            Debug.LogError("PlayerInteractor needs the Interact action and the camera assigned.", this);
            enabled = false;
            return;
        }

        interactAction.action.Enable();
        interactAction.action.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteract;
        }
        Target = null;
        if (prompt != null)
        {
            prompt.Hide();
        }
    }

    private void Update()
    {
        Target = FindTarget();
        UpdatePrompt();
    }

    private IInteractable FindTarget()
    {
        // Starting inside the player's own capsule is fine: a ray never hits the collider it starts in.
        if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, reach, lookMask, QueryTriggerInteraction.Collide))
        {
            return hit.collider.GetComponentInParent<IInteractable>();
        }
        return null;
    }

    private void UpdatePrompt()
    {
        if (prompt == null)
        {
            return;
        }

        string text = Target != null ? Target.GetPrompt() : null;
        if (string.IsNullOrEmpty(text))
        {
            prompt.Hide();
        }
        else if (Target.CanInteract)
        {
            // GetBindingDisplayString(0) is the first binding, the E key.
            prompt.Show($"[{interactAction.action.GetBindingDisplayString(0)}] {text}");
        }
        else
        {
            prompt.Show(text);
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        // Look again so the press always uses what is under the crosshair right now.
        Target = FindTarget();
        if (Target != null && Target.CanInteract)
        {
            Target.Interact(this);
            UpdatePrompt();
        }
    }
}
