using UnityEngine;

// A tool lying in the world, like the hoe in the stump. Press Interact to put it in the hotbar.
public class ToolPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private ToolType tool = ToolType.Hoe;
    [SerializeField] private string displayName = "hoe";

    public ToolType Tool => tool;

    public bool CanInteract => true;

    public string GetPrompt() => $"Pick up {displayName}";

    public void Interact(PlayerInteractor player)
    {
        var tools = player.GetComponent<PlayerTools>();
        if (tools == null)
        {
            Debug.LogError("The player has no PlayerTools to hold the tool.", player);
            return;
        }

        if (tools.GiveTool(tool))
        {
            gameObject.SetActive(false);
        }
        else
        {
            Debug.Log($"No room for the {displayName}: the hotbar is full.", this);
        }
    }
}
