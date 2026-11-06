using UnityEngine;
using UnityEngine.UI;

// Shows the watering can's water above the hotbar, like "Water 3 / 6", while the can is in hand.
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
