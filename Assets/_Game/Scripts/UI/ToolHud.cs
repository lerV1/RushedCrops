using UnityEngine;
using UnityEngine.UI;

// A line above the hotbar showing the watering can's water, like "Water 3 / 6".
// Hidden unless the can is in hand. Redraws only when PlayerTools changes.
// The name of whatever is held is shown by InventoryHud.
public class ToolHud : MonoBehaviour
{
    [SerializeField] private PlayerTools tools;
    [SerializeField] private Text label;

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
        bool holdingCan = tools.Equipped == ToolType.WateringCan;
        label.enabled = holdingCan;
        if (holdingCan)
        {
            label.text = $"Water {tools.WaterLeft} / {tools.CanCapacity}";
        }
    }
}
