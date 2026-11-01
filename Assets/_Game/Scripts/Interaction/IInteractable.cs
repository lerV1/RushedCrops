// Anything the player can use by looking at it and pressing Interact (E / Button West):
// a farm plot, the merchant, a barn repair point and so on.
// Put the component on the object that has the collider, or on one of its parents.
public interface IInteractable
{
    // What the player sees while looking at this, such as "Plant carrot seed".
    // Return null or an empty string to show nothing.
    string GetPrompt();

    // True when pressing Interact does something right now. When false the prompt is only
    // information (like "Carrots growing: 40%") and is shown without the key.
    bool CanInteract { get; }

    // Called once when the player presses Interact while looking at this and CanInteract is true.
    void Interact(PlayerInteractor player);
}
