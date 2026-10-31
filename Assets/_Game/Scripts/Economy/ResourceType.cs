using System;

// Every kind of resource the shared inventory can hold.
// Seeds and harvested crops are counted separately for each crop in CropType.
// To add a resource later, add it here and give it a starting amount in Inventory.
public enum ResourceType
{
    Coins,
    CarrotSeeds,
    TomatoSeeds,
    Carrots,
    Tomatoes,
    Wood,
    Stone
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
