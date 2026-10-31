using UnityEngine;
using UnityEngine.InputSystem;

// Debug keys for trying the inventory in Play mode. Remove this component before release.
//   1-7  add resources (Coins, Carrot Seeds, Tomato Seeds, Carrots, Tomatoes, Wood, Stone)
//   8    try to spend the single test cost
//   9    try to spend the multi-resource test cost (all or nothing)
//   0    try to add and spend an invalid amount (should be rejected)
// The number keys aren't used by the player controls, so movement and looking are unaffected.
public class InventoryDebugControls : MonoBehaviour
{
    [Tooltip("The scene's shared Inventory.")]
    [SerializeField] private Inventory inventory;

    [Header("Test Values")]
    [Tooltip("How much keys 1-7 add.")]
    [SerializeField, Min(1)] private int grantAmount = 5;
    [Tooltip("Spent with key 8.")]
    [SerializeField] private ResourceAmount singleCost = new ResourceAmount(ResourceType.Coins, 5);
    [Tooltip("Spent with key 9. Like a barn repair: every line must be affordable or nothing is taken.")]
    [SerializeField] private ResourceAmount[] multiCost =
    {
        new ResourceAmount(ResourceType.Wood, 10),
        new ResourceAmount(ResourceType.Stone, 5),
        new ResourceAmount(ResourceType.Coins, 15),
    };
    [Tooltip("Used with key 0. Should be zero or negative so the inventory rejects it.")]
    [SerializeField] private int invalidAmount = -5;

    // Keys 1-7 match the order of ResourceType: key 1 is Coins, key 7 is Stone.
    private static readonly Key[] GrantKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7
    };

    /// <summary>What the last key press did, for the debug display.</summary>
    public string LastResult { get; private set; } = "Press 1-7 to add, 8/9 to spend, 0 for an invalid amount.";

    public string HelpText =>
        $"1-7: +{grantAmount} of each resource\n" +
        $"8: spend {singleCost}\n" +
        $"9: spend {string.Join(" + ", multiCost)}\n" +
        $"0: try amount {invalidAmount}";

    private void Awake()
    {
        if (inventory == null)
        {
            Debug.LogError("InventoryDebugControls needs the Inventory dragged into its Inventory field.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // Keyboard.current is null when no keyboard is connected.
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        for (int i = 0; i < GrantKeys.Length; i++)
        {
            if (keyboard[GrantKeys[i]].wasPressedThisFrame)
            {
                Grant((ResourceType)i);
            }
        }

        if (keyboard[Key.Digit8].wasPressedThisFrame)
        {
            bool spent = inventory.TrySpend(singleCost.type, singleCost.amount);
            Report(spent ? $"Spent {singleCost}." : $"Couldn't spend {singleCost}: not enough.");
        }

        if (keyboard[Key.Digit9].wasPressedThisFrame)
        {
            string cost = string.Join(" + ", multiCost);
            bool spent = inventory.TrySpend(multiCost);
            Report(spent ? $"Spent {cost}." : $"Couldn't spend {cost}. Nothing was taken.");
        }

        if (keyboard[Key.Digit0].wasPressedThisFrame)
        {
            bool added = inventory.Add(ResourceType.Coins, invalidAmount);
            bool spent = inventory.TrySpend(ResourceType.Coins, invalidAmount);
            Report(added || spent
                ? $"Amount {invalidAmount} was accepted. That's a bug."
                : $"Amount {invalidAmount} was rejected, as it should be.");
        }
    }

    private void Grant(ResourceType type)
    {
        bool added = inventory.Add(type, grantAmount);
        Report(added ? $"Added {grantAmount} {type}." : $"Couldn't add {grantAmount} {type}.");
    }

    private void Report(string message)
    {
        LastResult = message;
        Debug.Log($"[Inventory debug] {message}", this);
    }
}
