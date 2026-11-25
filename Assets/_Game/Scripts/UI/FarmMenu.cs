using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Saves the farm before returning to the title menu.</summary>
[DefaultExecutionOrder(-100)]
public class FarmMenu : MonoBehaviour
{
    [SerializeField] private InputActionReference openMenuAction;
    [SerializeField] private SaveSystem saveSystem;
    [SerializeField] private MerchantShopUI shop;
    [SerializeField] private string menuScene = "SC_Menu";

    private bool returning;

    private void OnEnable()
    {
        if (openMenuAction != null) openMenuAction.action.Enable();
    }

    private void OnDisable()
    {
        if (openMenuAction != null) openMenuAction.action.Disable();
    }

    private void Update()
    {
        // Run before the shop closes, so one Escape press only closes that window.
        if (returning || openMenuAction == null || saveSystem == null ||
            (shop != null && shop.IsOpen) || Time.timeScale <= 0f) return;
        if (!openMenuAction.action.WasPressedThisFrame()) return;
        if (!Application.CanStreamedLevelBeLoaded(menuScene))
        {
            Debug.LogError("The menu scene is missing from the build.", this);
            return;
        }

        returning = true;
        saveSystem.SaveNow();
        SceneManager.LoadSceneAsync(menuScene);
    }
}
