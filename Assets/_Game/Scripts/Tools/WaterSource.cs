using UnityEngine;

// Water the player can fill the watering can from, like the river.
// Hold the can, aim at the water and press Use Tool. Needs a collider the look ray can hit.
public class WaterSource : MonoBehaviour, IToolTarget
{
    public string GetToolPrompt(PlayerTools tools)
    {
        if (tools.Equipped != ToolType.WateringCan)
        {
            return null;
        }
        return tools.WaterLeft >= tools.CanCapacity ? "Watering can is full" : "Fill watering can";
    }

    public bool CanUseTool(PlayerTools tools) =>
        tools.Equipped == ToolType.WateringCan && tools.WaterLeft < tools.CanCapacity;

    public void UseTool(PlayerTools tools) => tools.FillCan();
}
