using UnityEngine;

// A tool lying in the world, like the hoe stuck in the stump by the garden gate.
// Look at it and press Interact to pick it up; it then disappears from the world.
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

        tools.GiveTool(tool);
        gameObject.SetActive(false);
    }
}
