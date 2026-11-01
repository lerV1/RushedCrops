using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A line above the hotbar that says what is in the player's hand, like "Hoe   [F] Switch".
// Hidden until the player owns at least one tool. Redraws only when PlayerTools changes.
public class ToolHud : MonoBehaviour
{
    [SerializeField] private PlayerTools tools;
    [SerializeField] private Text label;
    [Tooltip("Used to show the right key.")]
    [SerializeField] private InputActionReference switchAction;

    private void OnEnable()
    {
        if (tools == null || label == null)
        {
            Debug.LogError("ToolHud needs PlayerTools and a Text assigned.", this);
            enabled = false;
            return;
        }

        tools.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (tools != null)
        {
            tools.Changed -= Refresh;
        }
    }

    private void Refresh()
    {
        bool ownsAny = tools.Has(ToolType.Hoe) || tools.Has(ToolType.WateringCan);
        label.enabled = ownsAny;
        if (!ownsAny)
        {
            return;
        }

        string key = switchAction != null ? switchAction.action.GetBindingDisplayString(0) : "F";
        label.text = $"{Describe(tools.Equipped)}   [{key}] Switch";
    }

    private string Describe(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Hoe:
                return "Hoe";
            case ToolType.WateringCan:
                return "Watering can";
            default:
                return "Empty hands";
        }
    }
}
