using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Plays the first-person arm animations, made in Blender and played straight from the arm models (ClipPlayer):
// the right arm holds, swings and pours the tool in hand (or punches), the left arm grabs on Interact.
// Changing tools drops the arm out of view to swap the model. Each move blends back into its idle when it ends.
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

    public bool SwitchTool(ToolType next)
    {
        if (next == shownTool || !IsResting(rightArm))
        {
            return false;
        }

        if (shownTool == ToolType.None)
        {
            rightArm.Play("Equip", 0.05f, GripIdle, 0.05f);     // open hand -> gripping
        }
        else if (next == ToolType.None)
        {
            rightArm.Play("Unequip", 0.05f, Idle, 0.05f);       // gripping -> open hand
        }
        else
        {
            rightArm.Play("Swap", 0.05f, GripIdle, 0.05f);      // one tool for another
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

    // Resting: settled in an idle, not partway through a move or a blend.
    private static bool IsResting(ClipPlayer arm) =>
        !arm.IsBlending && (arm.Current == Idle || arm.Current == GripIdle);

    private void OnInteract(InputAction.CallbackContext context) => PlayInteract();
}
