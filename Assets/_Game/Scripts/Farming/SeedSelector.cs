using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Switches which seed the player plants (carrot, tomato, ...) with Switch Seed (Q / right bumper).
// The choice is stored in the GameManager, so the farm plots and the HUD all read the same value.
public class SeedSelector : MonoBehaviour
{
    [SerializeField] private InputActionReference switchSeedAction;

    private void OnEnable()
    {
        if (switchSeedAction == null)
        {
            Debug.LogError("SeedSelector needs the Switch Seed action assigned.", this);
            enabled = false;
            return;
        }

        switchSeedAction.action.Enable();
        switchSeedAction.action.performed += OnSwitchSeed;
    }

    private void OnDisable()
    {
        if (switchSeedAction != null)
        {
            switchSeedAction.action.performed -= OnSwitchSeed;
        }
    }

    private void OnSwitchSeed(InputAction.CallbackContext context) => SelectNext();

    // Moves to the next crop type, wrapping round from the last to the first.
    public void SelectNext()
    {
        GameManager game = GameManager.Instance;
        if (game == null)
        {
            return;
        }

        var crops = (CropType[])Enum.GetValues(typeof(CropType));
        int next = (Array.IndexOf(crops, game.SelectedCrop) + 1) % crops.Length;
        game.SelectCrop(crops[next]);
    }
}
