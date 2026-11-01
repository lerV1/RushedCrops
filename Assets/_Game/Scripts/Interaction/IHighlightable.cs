// Optional: something that shows when the player aims at it, like the outline on a farm square.
// PlayerInteractor turns the highlight on while the crosshair is on it and off when it moves away.
public interface IHighlightable
{
    void SetHighlighted(bool on);
}
