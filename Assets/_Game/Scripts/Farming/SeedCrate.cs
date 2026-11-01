using UnityEngine;

// An open crate of one crop's seeds by the garden gate. Look at it and press Interact to
// put a handful of seeds into the hotbar. The crate never runs out, so it stands in for
// buying seeds until the market sells them.
// With a full hotbar nothing is taken and the prompt says so.
public class SeedCrate : MonoBehaviour, IInteractable
{
    [Tooltip("The scene's shared Inventory. The seeds go into it.")]
    [SerializeField] private Inventory inventory;

    [Tooltip("The crop whose seeds this crate holds.")]
    [SerializeField] private CropData crop;

    [Tooltip("How many seeds one press of Interact gives.")]
    [SerializeField, Min(1)] private int seedsPerTake = 5;

    public CropData Crop => crop;

    private void Awake()
    {
        if (inventory == null || crop == null)
        {
            Debug.LogError($"{name} needs the Inventory and a crop assigned.", this);
            enabled = false;
        }
    }

    public bool CanInteract => enabled && inventory.CanAdd(crop.SeedResource, seedsPerTake);

    public string GetPrompt()
    {
        if (!enabled)
        {
            return null;
        }
        return CanInteract
            ? $"Take {seedsPerTake} {crop.DisplayName.ToLower()} seeds"
            : "Hotbar is full";
    }

    public void Interact(PlayerInteractor player)
    {
        if (enabled)
        {
            inventory.Add(crop.SeedResource, seedsPerTake);
        }
    }
}
