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
    Hatchet,
    Pickaxe,
    // Rare boss seeds dropped by evil mini crops. Planting one grows that boss.
    CarrotKingSeed,
    TomatoBruteSeed,
    OnionOverlordSeed,
    BeetBaronSeed,
    CabbageColossusSeed,
    WheatWarlordSeed,
    // A captured boss can be placed into one of the barn's seed tubes.
    CapturedBoss,
    Sword,
    CaptureJar,
    CapturedCarrotKing,
    CapturedTomatoBrute,
    CapturedOnionOverlord,
    CapturedBeetBaron,
    CapturedCabbageColossus,
    CapturedWheatWarlord
}

public static class ResourceTypes
{
    /// <summary>The seed belonging to a captured boss; -1 for other items.</summary>
    public static ResourceType SeedForCapturedBoss(ResourceType item)
    {
        switch (item)
        {
            // Old saves did not record a species. Keep their unnamed captures usable.
            case ResourceType.CapturedBoss:
            case ResourceType.CapturedCarrotKing: return ResourceType.CarrotKingSeed;
            case ResourceType.CapturedTomatoBrute: return ResourceType.TomatoBruteSeed;
            case ResourceType.CapturedOnionOverlord: return ResourceType.OnionOverlordSeed;
            case ResourceType.CapturedBeetBaron: return ResourceType.BeetBaronSeed;
            case ResourceType.CapturedCabbageColossus: return ResourceType.CabbageColossusSeed;
            case ResourceType.CapturedWheatWarlord: return ResourceType.WheatWarlordSeed;
            default: return (ResourceType)(-1);
        }
    }

    /// <summary>The inventory item preserving this boss seed's species.</summary>
    public static ResourceType CapturedBossForSeed(ResourceType seed)
    {
        switch (seed)
        {
            case ResourceType.CarrotKingSeed: return ResourceType.CapturedCarrotKing;
            case ResourceType.TomatoBruteSeed: return ResourceType.CapturedTomatoBrute;
            case ResourceType.OnionOverlordSeed: return ResourceType.CapturedOnionOverlord;
            case ResourceType.BeetBaronSeed: return ResourceType.CapturedBeetBaron;
            case ResourceType.CabbageColossusSeed: return ResourceType.CapturedCabbageColossus;
            case ResourceType.WheatWarlordSeed: return ResourceType.CapturedWheatWarlord;
            default: return (ResourceType)(-1);
        }
    }

    /// <summary>Resolves preplaced bosses, which were not grown from a seed.</summary>
    public static ResourceType BossSeedForName(string bossName)
    {
        switch (bossName)
        {
            case "Carrot King": return ResourceType.CarrotKingSeed;
            case "Tomato Brute": return ResourceType.TomatoBruteSeed;
            case "Onion Overlord": return ResourceType.OnionOverlordSeed;
            case "Beet Baron": return ResourceType.BeetBaronSeed;
            case "Cabbage Colossus": return ResourceType.CabbageColossusSeed;
            case "Wheat Warlord": return ResourceType.WheatWarlordSeed;
            default: return (ResourceType)(-1);
        }
    }

    /// <summary>Includes named captures and unnamed captures from older saves.</summary>
    public static bool IsCapturedBoss(ResourceType item) => (int)SeedForCapturedBoss(item) >= 0;

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
            case ResourceType.Pickaxe:
                return ToolType.Pickaxe;
            case ResourceType.Sword:
                return ToolType.Sword;
            case ResourceType.CaptureJar:
                return ToolType.CaptureJar;
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
            case ToolType.Pickaxe:
                return ResourceType.Pickaxe;
            case ToolType.Sword:
                return ResourceType.Sword;
            case ToolType.CaptureJar:
                return ResourceType.CaptureJar;
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
