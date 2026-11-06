using UnityEngine;

// A crate of free seeds by the garden gate. Press Interact to take a handful; it never runs out.
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
