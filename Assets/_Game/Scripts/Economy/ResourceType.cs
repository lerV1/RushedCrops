using System;
using System.Text;

// Every kind of item the shared inventory can hold.
// Coins are kept in a wallet; everything else takes up hotbar slots.
// Seeds and harvested crops are counted separately for each crop in CropType.
// New items go at the end of the list: Unity saves these as numbers, so inserting
// one in the middle would change what existing crop assets point to.
public enum ResourceType
{
    Coins,
    CarrotSeeds,
    TomatoSeeds,
    Carrots,
    Tomatoes,
    Wood,
    Stone,
    Hoe,
    WateringCan
}

// Small helpers for questions about an item that don't depend on any one inventory.
public static class ResourceTypes
{
    // The tool an item puts in the player's hand, or ToolType.None for things like seeds.
    public static ToolType ToTool(ResourceType item)
    {
        switch (item)
        {
            case ResourceType.Hoe:
                return ToolType.Hoe;
            case ResourceType.WateringCan:
                return ToolType.WateringCan;
            default:
                return ToolType.None;
        }
    }

    // The inventory item for a tool. Only call this with a real tool, not ToolType.None.
    public static ResourceType FromTool(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Hoe:
                return ResourceType.Hoe;
            case ToolType.WateringCan:
                return ResourceType.WateringCan;
            default:
                throw new ArgumentException("Empty hands aren't an item.", nameof(tool));
        }
    }

    public static bool IsTool(ResourceType item) => ToTool(item) != ToolType.None;

    // Turns "CarrotSeeds" into "Carrot Seeds" by adding a space before each capital letter.
    public static string DisplayName(ResourceType item)
    {
        string name = item.ToString();
        var result = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                result.Append(' ');
            }
            result.Append(name[i]);
        }
        return result.ToString();
    }
}

// One line of a price or cost, such as "10 Wood".
// [Serializable] lets Unity show lists of these in the Inspector,
// so shop prices and repair costs can be edited without changing code.
[Serializable]
public struct ResourceAmount
{
    public ResourceType type;
    public int amount;

    public ResourceAmount(ResourceType type, int amount)
    {
        this.type = type;
        this.amount = amount;
    }

    public override string ToString() => $"{amount} {type}";
}
