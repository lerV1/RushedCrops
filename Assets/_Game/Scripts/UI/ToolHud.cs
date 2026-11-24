using UnityEngine;
using UnityEngine.UI;

// Shows the selected tool above the hotbar, with the watering can's remaining water.
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
        bool holdingTool = tools.Equipped != ToolType.None;
        label.enabled = holdingTool;
        if (holdingTool)
        {
            string name = ResourceTypes.DisplayName(ResourceTypes.FromTool(tools.Equipped));
            label.text = tools.Equipped == ToolType.WateringCan
                ? $"{name}: {tools.WaterLeft} / {tools.CanCapacity}"
                : name;
        }
    }
}
