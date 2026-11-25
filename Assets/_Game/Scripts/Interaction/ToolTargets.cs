using UnityEngine;

// The four kinds of component that a work tool can use.
public static class ToolTargets
{
    public static Component Find(Collider hit)
    {
        if (hit == null) return null;
        for (Transform parent = hit.transform; parent != null; parent = parent.parent)
        {
            foreach (MonoBehaviour component in parent.GetComponents<MonoBehaviour>())
                if (component is FarmField || component is FarmTile || component is ResourceNode || component is WaterSource)
                    return component;
        }
        return null;
    }

    public static string GetPrompt(Component target, PlayerTools tools)
    {
        if (target is FarmField field) return field.GetToolPrompt(tools);
        if (target is FarmTile tile) return tile.GetToolPrompt(tools);
        if (target is ResourceNode node) return node.GetToolPrompt(tools);
        if (target is WaterSource water) return water.GetToolPrompt(tools);
        return null;
    }

    public static bool CanUse(Component target, PlayerTools tools)
    {
        if (target is FarmField field) return field.CanUseTool(tools);
        if (target is FarmTile tile) return tile.CanUseTool(tools);
        if (target is ResourceNode node) return node.CanUseTool(tools);
        if (target is WaterSource water) return water.CanUseTool(tools);
        return false;
    }

    public static void Use(Component target, PlayerTools tools)
    {
        if (target is FarmField field) field.UseTool(tools);
        else if (target is FarmTile tile) tile.UseTool(tools);
        else if (target is ResourceNode node) node.UseTool(tools);
        else if (target is WaterSource water) water.UseTool(tools);
    }
}
