// Something the tool in hand can be used on with Use Tool, like a square the hoe tills.
public interface IToolTarget
{
    // Null when the tool does nothing here; the Interact prompt shows instead.
    string GetToolPrompt(PlayerTools tools);

    // When false the prompt is only information and is shown without the key.
    bool CanUseTool(PlayerTools tools);

    // Called when the tool reaches the target, partway through the swing or pour.
    void UseTool(PlayerTools tools);
}
