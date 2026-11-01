// Something the player can use the tool in their hand on, with Use Tool (left click / right trigger):
// a farm square the hoe can till, a crop the watering can can water, the river that fills the can.
// Put the component on the object that has the collider, or on one of its parents.
public interface IToolTarget
{
    // What the tool in hand would do here, such as "Till soil". Return null when that tool does
    // nothing here; the Interact prompt is shown instead.
    string GetToolPrompt(PlayerTools tools);

    // True when using the tool here works right now. When false, the tool prompt is only
    // information (like "Watering can is empty") and is shown without the key.
    bool CanUseTool(PlayerTools tools);

    // Called when the tool reaches the target, partway through the swing or pour.
    void UseTool(PlayerTools tools);
}
