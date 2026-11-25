using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shows the saved Canvas objects when the player dies.
public class DeathScreenHud : MonoBehaviour
{
    [SerializeField] private RespawnController respawn;
    [SerializeField] private CanvasGroup screen;
    [SerializeField] private RectTransform title;
    [SerializeField] private Button respawnButton;
    [SerializeField] private Text causeText;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (respawn == null || screen == null || title == null || respawnButton == null
            || causeText == null || quitButton == null)
        {
            Debug.LogError("DeathScreenHud needs the player, screen, title, cause and both buttons assigned.", this);
            enabled = false;
            return;
        }
        Hide();
    }

    private void OnEnable()
    {
        if (respawn == null || respawnButton == null) return;
        respawn.DeathStarted += Show;
        respawn.Respawned += Hide;
        respawnButton.onClick.AddListener(RespawnAtCheckpoint);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (respawn.IsAwaitingRespawn) Show();
    }

    private void OnDisable()
    {
        if (respawn != null)
        {
            respawn.DeathStarted -= Show;
            respawn.Respawned -= Hide;
        }
        if (respawnButton != null) respawnButton.onClick.RemoveListener(RespawnAtCheckpoint);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
    }

    private void Show()
    {
        screen.gameObject.SetActive(true);
        screen.alpha = 1f;
        screen.interactable = true;
        screen.blocksRaycasts = true;
        causeText.text = "Cause of death: " + respawn.GetComponent<Health>().LastDamageCause;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(respawnButton.gameObject);
        }
    }

    private void Hide()
    {
        if (screen == null) return;
        screen.interactable = false;
        screen.blocksRaycasts = false;
        screen.gameObject.SetActive(false);
    }

    private void RespawnAtCheckpoint()
    {
        respawn.Respawn();
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
