using UnityEngine;
using UnityEngine.InputSystem;

// Finds what the player is looking at and uses it when Interact is pressed.
// Sits on the Player. A ray goes out from the camera; the first collider it hits counts if it
// (or a parent) has a component that implements IInteractable, IToolTarget or IHighlightable.
// The prompt under the crosshair shows what the tool in hand would do there ("[LMB] Till soil"),
// and otherwise what Interact would do ("[E] Plant carrot seed").
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private InputActionReference interactAction;

    [Tooltip("Only used to show the right key in tool prompts.")]
    [SerializeField] private InputActionReference useToolAction;

    [Tooltip("The camera the player looks through. The look ray starts here.")]
    [SerializeField] private Transform viewPoint;

    [Tooltip("How far away, in metres, the player can use things.")]
    [SerializeField, Min(0.5f)] private float reach = 3f;

    [Tooltip("Layers the look ray can hit.")]
    [SerializeField] private LayerMask lookMask = ~0;

    [Tooltip("Shows the prompt under the crosshair. Optional.")]
    [SerializeField] private InteractionPromptHud prompt;

    [Tooltip("The player's tools, for tool prompts. Optional.")]
    [SerializeField] private PlayerTools tools;

    private IHighlightable highlighted;

    // What the player is looking at this frame, or null.
    public IInteractable Target { get; private set; }
    public IToolTarget ToolTarget { get; private set; }

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
        ToolTarget = null;
        SetHighlight(null);
        if (prompt != null)
        {
            prompt.Hide();
        }
    }

    private void Update()
    {
        Look();
        UpdatePrompt();
    }

    // Looks again right now and returns what the tool in hand would be used on.
    public IToolTarget FindToolTarget()
    {
        Look();
        return ToolTarget;
    }

    private void Look()
    {
        Collider hit = null;
        // Starting inside the player's own capsule is fine: a ray never hits the collider it starts in.
        if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit info, reach, lookMask, QueryTriggerInteraction.Collide))
        {
            hit = info.collider;
        }

        Target = hit != null ? hit.GetComponentInParent<IInteractable>() : null;
        ToolTarget = hit != null ? hit.GetComponentInParent<IToolTarget>() : null;
        SetHighlight(hit != null ? hit.GetComponentInParent<IHighlightable>() : null);
    }

    private void SetHighlight(IHighlightable next)
    {
        if (next == highlighted)
        {
            return;
        }
        // "as Object" catches a highlight that was destroyed while aimed at.
        if (highlighted != null && highlighted as Object != null)
        {
            highlighted.SetHighlighted(false);
        }
        highlighted = next;
        highlighted?.SetHighlighted(true);
    }

    private void UpdatePrompt()
    {
        if (prompt == null)
        {
            return;
        }

        if (tools != null && tools.Equipped != ToolType.None && ToolTarget != null)
        {
            string toolText = ToolTarget.GetToolPrompt(tools);
            if (!string.IsNullOrEmpty(toolText))
            {
                prompt.Show(ToolTarget.CanUseTool(tools) ? $"[{KeyName(useToolAction)}] {toolText}" : toolText);
                return;
            }
        }

        string text = Target != null ? Target.GetPrompt() : null;
        if (string.IsNullOrEmpty(text))
        {
            prompt.Hide();
        }
        else
        {
            prompt.Show(Target.CanInteract ? $"[{KeyName(interactAction)}] {text}" : text);
        }
    }

    // GetBindingDisplayString(0) is the first binding: E for Interact, LMB for Use Tool.
    private static string KeyName(InputActionReference action) =>
        action != null ? action.action.GetBindingDisplayString(0) : "?";

    private void OnInteract(InputAction.CallbackContext context)
    {
        // Look again so the press always uses what is under the crosshair right now.
        Look();
        if (Target != null && Target.CanInteract)
        {
            Target.Interact(this);
            UpdatePrompt();
        }
    }
}
