// Anything the player can use by looking at it and pressing Interact (E / Button West):
// a farm plot, the merchant, a barn repair point and so on.
// Put the component on the object that has the collider, or on one of its parents.
public interface IInteractable
{
    // What pressing Interact would do right now, such as "Plant carrot seed".
    // Return null or an empty string when there is nothing to do, and no prompt is shown.
    string GetPrompt();

    // Called once when the player presses Interact while looking at this object.
    void Interact(PlayerInteractor player);
}
