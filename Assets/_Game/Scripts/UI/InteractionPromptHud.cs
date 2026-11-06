using UnityEngine;
using UnityEngine.UI;

// The prompt under the crosshair, like "[E] Plant carrot seed". PlayerInteractor calls Show and Hide.
public class InteractionPromptHud : MonoBehaviour
{
    [Tooltip("The prompt's background. It is hidden when there is nothing to use.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Text label;

    private void Awake()
    {
        Hide();
    }

    public void Show(string text)
    {
        // Only touch the Text when it changes, so the UI isn't rebuilt every frame.
        if (label != null && label.text != text)
        {
            label.text = text;
        }
        if (panel != null && !panel.activeSelf)
        {
            panel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (panel != null && panel.activeSelf)
        {
            panel.SetActive(false);
        }
    }
}
