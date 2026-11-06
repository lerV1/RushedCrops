// Anything the player can use by looking at it and pressing Interact. Put it on the collider or a parent.
public interface IInteractable
{
    // The prompt, such as "Plant carrot seed". Null shows nothing.
    string GetPrompt();

    // When false the prompt is only information and is shown without the key.
    bool CanInteract { get; }

    void Interact(PlayerInteractor player);
}
