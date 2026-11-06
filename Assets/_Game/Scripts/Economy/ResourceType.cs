using System;
using System.Text;

// Every kind of item the inventory holds. Coins go in the wallet; everything else takes hotbar slots.
// Add new items at the end: Unity saves these as numbers.
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
    WateringCan,
    BeetSeeds,
    CabbageSeeds,
    OnionSeeds,
    WheatSeeds,
    Beets,
    Cabbages,
    Onions,
    Wheat,
    Hatchet
}

public static class ResourceTypes
{
    public static ToolType ToTool(ResourceType item)
    {
        switch (item)
        {
            case ResourceType.Hoe:
                return ToolType.Hoe;
            case ResourceType.WateringCan:
                return ToolType.WateringCan;
            case ResourceType.Hatchet:
                return ToolType.Hatchet;
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
            case ToolType.Hatchet:
                return ResourceType.Hatchet;
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
