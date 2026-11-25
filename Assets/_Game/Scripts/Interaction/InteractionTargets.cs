using UnityEngine;

// Calls the ordinary component that the player is looking at.
public static class InteractionTargets
{
    public static Component Find(Collider hit)
    {
        if (hit == null) return null;
        for (Transform parent = hit.transform; parent != null; parent = parent.parent)
        {
            foreach (MonoBehaviour component in parent.GetComponents<MonoBehaviour>())
            {
                if (component is ToolPickup || component is ItemDrop || component is ResourceNode ||
                    component is FarmTile || component is FarmField || component is Merchant ||
                    component is BarnRepair || component is BossTube || component is CropBoss || component is MiniCrop)
                    return component;
            }
        }
        return null;
    }

    public static string GetPrompt(Component target)
    {
        if (target is ToolPickup pickup) return pickup.GetPrompt();
        if (target is ItemDrop drop) return drop.GetPrompt();
        if (target is ResourceNode node) return node.GetPrompt();
        if (target is FarmTile tile) return tile.GetPrompt();
        if (target is FarmField field) return field.GetPrompt();
        if (target is Merchant merchant) return merchant.GetPrompt();
        if (target is BarnRepair barn) return barn.GetPrompt();
        if (target is BossTube tube) return tube.GetPrompt();
        if (target is CropBoss boss) return boss.GetPrompt();
        if (target is MiniCrop crop) return crop.GetPrompt();
        return null;
    }

    public static bool CanInteract(Component target)
    {
        if (target is ToolPickup pickup) return pickup.CanInteract;
        if (target is ItemDrop drop) return drop.CanInteract;
        if (target is ResourceNode node) return node.CanInteract;
        if (target is FarmTile tile) return tile.CanInteract;
        if (target is FarmField field) return field.CanInteract;
        if (target is Merchant merchant) return merchant.CanInteract;
        if (target is BarnRepair barn) return barn.CanInteract;
        if (target is BossTube tube) return tube.CanInteract;
        if (target is CropBoss boss) return boss.CanInteract;
        if (target is MiniCrop crop) return crop.CanInteract;
        return false;
    }

    public static void Interact(Component target, PlayerInteractor player)
    {
        if (target is ToolPickup pickup) pickup.Interact(player);
        else if (target is ItemDrop drop) drop.Interact(player);
        else if (target is ResourceNode node) node.Interact(player);
        else if (target is FarmTile tile) tile.Interact(player);
        else if (target is FarmField field) field.Interact(player);
        else if (target is Merchant merchant) merchant.Interact(player);
        else if (target is BarnRepair barn) barn.Interact(player);
        else if (target is BossTube tube) tube.Interact(player);
        else if (target is CropBoss boss) boss.Interact(player);
        else if (target is MiniCrop crop) crop.Interact(player);
    }
}
