using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the separate title scene. The farm owns loading and saving gameplay.</summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string farmScene = "SC_Farm";
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject newGamePanel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button cancelResetButton;
    [SerializeField] private Text status;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValue;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Text fullscreenHint;

    private bool loading;

    private void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        volumeSlider.SetValueWithoutNotify(MenuSettings.Volume);
        ShowVolume(MenuSettings.Volume);
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
#if UNITY_EDITOR || UNITY_WEBGL
        fullscreenToggle.interactable = false;
        fullscreenHint.text = "";
#endif
        ShowHome();
    }

    private void Update()
    {
        bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (back && !loading) ShowHome();
    }

    /// <summary>Continues saved progress, or starts the first session when no save exists.</summary>
    public void Play()
    {
        if (!loading) StartCoroutine(LoadFarm(false));
    }

    /// <summary>Asks before replacing an existing session.</summary>
    public void NewGame()
    {
        if (loading) return;
        if (SaveSystem.HasSavedProgress)
            ShowPanel(newGamePanel, cancelResetButton);
        else
            StartCoroutine(LoadFarm(true));
    }

    /// <summary>Clears only gameplay progress and starts from the authored farm state.</summary>
    public void ConfirmNewGame()
    {
        if (!loading) StartCoroutine(LoadFarm(true));
    }

    /// <summary>Opens audio and display settings.</summary>
    public void ShowSettings()
    {
        if (!loading) ShowPanel(settingsPanel, volumeSlider);
    }

    /// <summary>Closes settings or cancels a reset without changing game progress.</summary>
    public void ShowHome()
    {
        if (loading) return;
        PlayerPrefs.Save();
        ShowPanel(homePanel, playButton);
    }

    /// <summary>Updates master volume immediately.</summary>
    public void ChangeVolume(float value)
    {
        MenuSettings.SetVolume(value);
        ShowVolume(value);
    }

    /// <summary>Updates fullscreen on supported standalone builds.</summary>
    public void ChangeFullscreen(bool value) => MenuSettings.SetFullscreen(value);

    /// <summary>Closes the player; stops Play Mode when testing in Unity.</summary>
    public void Quit()
    {
        if (loading) return;
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        status.text = "You can close this browser tab to quit.";
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadFarm(bool reset)
    {
        // Validate the destination before deleting progress.
        if (!Application.CanStreamedLevelBeLoaded(farmScene))
        {
            status.text = "The farm scene is missing from the build.";
            yield break;
        }
        loading = true;
        status.text = "Loading your farm...";
        GetComponent<CanvasGroup>().interactable = false;
        if (reset) SaveSystem.ClearSavedProgress();
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        yield return null;
        yield return SceneManager.LoadSceneAsync(farmScene);
    }

    private void ShowPanel(GameObject panel, Selectable selected)
    {
        homePanel.SetActive(panel == homePanel);
        settingsPanel.SetActive(panel == settingsPanel);
        newGamePanel.SetActive(panel == newGamePanel);
        status.text = "";
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selected.gameObject);
    }

    private void ShowVolume(float value) => volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
}
