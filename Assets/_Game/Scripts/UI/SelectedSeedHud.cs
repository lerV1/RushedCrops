using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shows which seed will be planted: its hotbar slot lights up and a hint above the hotbar
// reads "Seed: Carrot   [Q] Switch". Redraws only when the GameManager's selected crop changes.
public class SelectedSeedHud : MonoBehaviour
{
    // One hotbar slot that holds a seed, and the crop it belongs to.
    [Serializable]
    private class SeedSlot
    {
        public CropType crop;
        public Image background;
    }

    [SerializeField] private SeedSlot[] slots;
    [SerializeField] private Color normalColor = new Color(0.08f, 0.07f, 0.05f, 0.72f);
    [SerializeField] private Color selectedColor = new Color(0.62f, 0.43f, 0.12f, 0.92f);

    [Tooltip("Optional text above the hotbar.")]
    [SerializeField] private Text hint;
    [Tooltip("Used to show the right key in the hint.")]
    [SerializeField] private InputActionReference switchSeedAction;

    private GameManager game;

    // Start, not OnEnable, so the GameManager has finished its Awake first.
    private void Start()
    {
        game = GameManager.Instance;
        if (game == null)
        {
            Debug.LogError("SelectedSeedHud needs a GameManager in the scene.", this);
            enabled = false;
            return;
        }

        game.SelectedCropChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (game != null)
        {
            game.SelectedCropChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        foreach (SeedSlot slot in slots)
        {
            if (slot != null && slot.background != null)
            {
                slot.background.color = slot.crop == game.SelectedCrop ? selectedColor : normalColor;
            }
        }

        if (hint != null)
        {
            string key = switchSeedAction != null ? switchSeedAction.action.GetBindingDisplayString(0) : "Q";
            hint.text = $"Seed: {game.SelectedCrop}   [{key}] Switch";
        }
    }
}
