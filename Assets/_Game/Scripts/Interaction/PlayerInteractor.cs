using UnityEngine;
using UnityEngine.InputSystem;

// Finds what the player is looking at, shows its prompt under the crosshair and uses it on Interact.
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

    [Tooltip("Layers that can be used from further away, like the river lying below its bank.")]
    [SerializeField] private LayerMask longReachLayers = 1 << 4;

    [Tooltip("How far away, in metres, the long reach layers can be used.")]
    [SerializeField, Min(0.5f)] private float longReach = 6f;

    [Tooltip("Shows the prompt under the crosshair. Optional.")]
    [SerializeField] private InteractionPromptHud prompt;

    [Tooltip("The player's tools, for tool prompts. Optional.")]
    [SerializeField] private PlayerTools tools;

    private FarmField highlighted;

    public Component Target { get; private set; }
    public Component ToolTarget { get; private set; }
    // Whatever the crosshair rests on within reach, usable or not (null for nothing).
    public Collider LookedAt { get; private set; }

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

    public Component FindToolTarget()
    {
        Look();
        return ToolTarget;
    }

    private void Look()
    {
        Collider hit = null;
        // The first thing hit always blocks what is behind it; it only counts if it is within reach.
        float maxDistance = Mathf.Max(reach, longReach);
        if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit info, maxDistance, lookMask, QueryTriggerInteraction.Collide))
        {
            bool longReachLayer = (longReachLayers.value & (1 << info.collider.gameObject.layer)) != 0;
            if (info.distance <= reach || longReachLayer)
            {
                hit = info.collider;
            }
        }

        LookedAt = hit;
        Target = InteractionTargets.Find(hit);
        ToolTarget = ToolTargets.Find(hit);
        FarmField highlight = hit != null ? hit.GetComponentInParent<FarmField>() : null;

        // Plain ground (nothing usable was hit): it may lie inside an area like the crop field.
        if (hit != null && Target == null && ToolTarget == null)
        {
            foreach (FarmField area in AreaTargets.All)
            {
                if (area.TryAim(info.point))
                {
                    Target = area;
                    ToolTarget = area;
                    highlight = area;
                    break;
                }
            }
        }
        SetHighlight(highlight);
    }

    private void SetHighlight(FarmField next)
    {
        if (next == highlighted)
        {
            return;
        }
        if (highlighted != null)
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
            string toolText = ToolTargets.GetPrompt(ToolTarget, tools);
            if (!string.IsNullOrEmpty(toolText))
            {
                prompt.Show(ToolTargets.CanUse(ToolTarget, tools) ? $"[{KeyName(useToolAction)}] {toolText}" : toolText);
                return;
            }
        }

        string text = InteractionTargets.GetPrompt(Target);
        if (string.IsNullOrEmpty(text))
        {
            prompt.Hide();
        }
        else
        {
            prompt.Show(InteractionTargets.CanInteract(Target) ? $"[{KeyName(interactAction)}] {text}" : text);
        }
    }

    private static string KeyName(InputActionReference action) =>
        action != null ? action.action.GetBindingDisplayString(0) : "?";

    private void OnInteract(InputAction.CallbackContext context)
    {
        // Look again so the press always uses what is under the crosshair right now.
        Look();
        if (Target != null && InteractionTargets.CanInteract(Target))
        {
            InteractionTargets.Interact(Target, this);
            UpdatePrompt();
        }
    }
}
