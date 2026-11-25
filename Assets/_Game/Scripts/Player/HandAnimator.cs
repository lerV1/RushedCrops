using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Plays the first-person arm animations, made in Blender and played straight from the arm models (ClipPlayer):
// the right arm holds, swings and pours the tool in hand (or punches), the left arm grabs on Interact.
// Changing what is held drops the arm out of view to swap the model. Each move blends back into its idle when it
// ends. Tools have their own fitted models; anything else in the selected slot (seeds, crops, wood, stone, boss
// seeds...) shows its item model held in the palm.
public class HandAnimator : MonoBehaviour
{
    private const string Idle = "Idle", GripIdle = "GripIdle";

    [Header("Arms")]
    [SerializeField] private ClipPlayer rightArm;
    [SerializeField] private ClipPlayer leftArm;

    [Header("Tools")]
    [Tooltip("The tool models in the right hand, in ToolType order. Leave element 0 (None) empty.")]
    [SerializeField] private GameObject[] toolModels = new GameObject[3];
    [Tooltip("Seconds into the equip, unequip and swap animations when the arm is below the screen.")]
    [SerializeField, Min(0f)] private float toolSwapDelay = 0.3f;

    [Header("Other held items")]
    [Tooltip("The model for each item (DA_ItemModels, the same ones dropped items show).")]
    [SerializeField] private ItemModels itemModels;
    [Tooltip("Where a held item sits in the right hand.")]
    [SerializeField] private Transform heldItemAnchor;
    [Tooltip("How big, in metres, a held item is at its longest side.")]
    [SerializeField, Min(0.02f)] private float heldItemSize = 0.15f;

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
    private ResourceType? shownItem;
    private GameObject heldModel;
    private Coroutine toolSwap;

    public bool IsRightArmResting => IsResting(rightArm);

    private void Awake()
    {
        if (rightArm == null || leftArm == null)
        {
            Debug.LogError("HandAnimator needs both arms' ClipPlayers assigned.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        rightArm.Play(Idle, 0f);
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

    // A tool in hand swings; an empty hand punches.
    public void PlaySwing()
    {
        if (!IsResting(rightArm)) return;
        bool holding = rightArm.Current == GripIdle;
        rightArm.Play(holding ? "ToolSwing" : "Punch", 0.04f, holding ? GripIdle : Idle, 0.1f);
        GameAudio.Play2D(Sfx.Swing);
    }

    public void PlayPunch() => PlaySwing();

    public void PlayPour(bool withWater)
    {
        if (!IsResting(rightArm) || rightArm.Current != GripIdle)
        {
            return;
        }
        rightArm.Play("Pour", 0.05f, GripIdle, 0.1f);
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
            leftArm.Play("Interact", 0.04f, Idle, 0.1f);
        }
    }

    public bool SwitchTool(ToolType next) => SwitchHeld(next, null);

    // Puts a tool, or else an item (null for nothing), in the right hand. False while the arm is busy.
    public bool SwitchHeld(ToolType tool, ResourceType? item)
    {
        if (tool != ToolType.None) item = null;
        if ((tool == shownTool && Nullable.Equals(item, shownItem)) || !IsResting(rightArm))
        {
            return false;
        }

        bool wasHolding = shownTool != ToolType.None || shownItem.HasValue;
        bool willHold = tool != ToolType.None || item.HasValue;
        if (!wasHolding)
        {
            rightArm.Play("Equip", 0.05f, GripIdle, 0.05f);     // open hand -> gripping
        }
        else if (!willHold)
        {
            rightArm.Play("Unequip", 0.05f, Idle, 0.05f);       // gripping -> open hand
        }
        else
        {
            rightArm.Play("Swap", 0.05f, GripIdle, 0.05f);      // one thing for another
        }

        shownTool = tool;
        shownItem = item;
        if (toolSwap != null)
        {
            StopCoroutine(toolSwap);
        }
        toolSwap = StartCoroutine(ShowWhileHidden(tool, item));
        return true;
    }

    private IEnumerator ShowWhileHidden(ToolType tool, ResourceType? item)
    {
        yield return new WaitForSeconds(toolSwapDelay);
        ShowTool(tool);
        ShowItem(item);
        toolSwap = null;
    }

    // The item's model, sized to fit the palm, with its colliders off so it never gets in the way.
    private void ShowItem(ResourceType? item)
    {
        if (heldModel != null)
        {
            Destroy(heldModel);
            heldModel = null;
        }
        GameObject source = item.HasValue && itemModels != null ? itemModels.Get(item.Value) : null;
        if (source == null || heldItemAnchor == null)
        {
            return;
        }
        heldModel = Instantiate(source, heldItemAnchor);
        heldModel.name = "Held " + item.Value;
        heldModel.transform.localRotation = Quaternion.Euler(itemModels.TurnFor(item.Value));
        foreach (Collider part in heldModel.GetComponentsInChildren<Collider>(true)) part.enabled = false;
        foreach (Renderer part in heldModel.GetComponentsInChildren<Renderer>(true))
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Renderer[] parts = heldModel.GetComponentsInChildren<Renderer>();
        if (parts.Length == 0) return;
        Bounds bounds = parts[0].bounds;
        foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longest > 0.0001f) heldModel.transform.localScale *= heldItemSize / longest;
        // Centre it on the anchor.
        bounds = parts[0].bounds;
        foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
        heldModel.transform.position += heldItemAnchor.position - bounds.center;
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

    // Resting: settled in an idle, not partway through a move or a blend.
    private static bool IsResting(ClipPlayer arm) =>
        !arm.IsBlending && (arm.Current == Idle || arm.Current == GripIdle);

    private void OnInteract(InputAction.CallbackContext context) => PlayInteract();
}
