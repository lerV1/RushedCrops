using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Covers the game after player death and offers a checkpoint respawn.
public class DeathScreenHud : MonoBehaviour
{
    [Tooltip("The player whose death and respawn control this screen.")]
    [SerializeField] private RespawnController respawn;

    private Canvas canvas;
    private CanvasGroup screen;
    private RectTransform title;
    private Button respawnButton;
    private float pulseOffset;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("DeathScreenHud needs a Canvas on the same object.", this);
            enabled = false;
            return;
        }

        if (respawn == null)
        {
            Debug.LogError("DeathScreenHud needs the player's RespawnController assigned.", this);
            enabled = false;
            return;
        }

        BuildScreen();
        Hide();
    }

    private void OnEnable()
    {
        if (respawn == null) return;
        respawn.DeathStarted += Show;
        respawn.Respawned += Hide;
        if (respawn.IsAwaitingRespawn) Show();
    }

    private void OnDisable()
    {
        if (respawn == null) return;
        respawn.DeathStarted -= Show;
        respawn.Respawned -= Hide;
    }

    private void Update()
    {
        if (screen == null || !screen.gameObject.activeSelf || title == null) return;

        float pulse = 1f + Mathf.Sin((Time.unscaledTime + pulseOffset) * 5f) * 0.045f;
        title.localScale = Vector3.one * pulse;
        title.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 8f) * 1.2f);
    }

    private void BuildScreen()
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        GameObject root = new GameObject("Death Screen", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());
        screen = root.GetComponent<CanvasGroup>();

        Image background = MakeImage("Hot Pink Backdrop", root.transform,
            new Color(0.22f, 0.015f, 0.19f, 0.96f), Vector2.zero, Vector2.one);
        background.raycastTarget = true;

        MakeText("Status", root.transform, "FARM STATUS: COOKED",
            new Color(0.25f, 1f, 0.9f), 34, FontStyle.Bold,
            new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.9f));

        title = MakeText("Death Title", root.transform, "SKIBIDI\nDOWN BAD",
            new Color(1f, 0.93f, 0.12f), 82, FontStyle.Bold,
            new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.76f)).rectTransform;

        MakeText("Meme Caption", root.transform, "NO RIZZ  •  L + RATIO",
            Color.white, 38, FontStyle.Bold,
            new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.42f));

        MakeText("Respawn Hint", root.transform, "YOUR CHECKPOINT IS STILL COOKING.",
            new Color(1f, 0.65f, 0.95f), 20, FontStyle.Bold,
            new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.32f));

        GameObject buttonObject = new GameObject("Respawn Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.27f, 0.1f);
        buttonRect.anchorMax = new Vector2(0.73f, 0.22f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.25f, 1f, 0.2f);
        respawnButton = buttonObject.GetComponent<Button>();
        respawnButton.targetGraphic = buttonImage;
        respawnButton.onClick.AddListener(RespawnAtCheckpoint);
        ColorBlock colors = respawnButton.colors;
        colors.highlightedColor = new Color(0.65f, 1f, 0.35f);
        colors.pressedColor = new Color(0.8f, 0.9f, 0.15f);
        respawnButton.colors = colors;

        Text buttonText = MakeText("Button Label", buttonObject.transform, "RESPAWN AT CHECKPOINT",
            new Color(0.12f, 0.03f, 0.16f), 24, FontStyle.Bold, Vector2.zero, Vector2.one);
        buttonText.raycastTarget = false;
    }

    private void Show()
    {
        if (screen == null) return;
        screen.gameObject.SetActive(true);
        screen.alpha = 1f;
        screen.interactable = true;
        screen.blocksRaycasts = true;
        pulseOffset = Random.Range(0f, 2f);
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

    private static Image MakeImage(string name, Transform parent, Color color, Vector2 min, Vector2 max)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = item.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text MakeText(string name, Transform parent, string value, Color color, int size,
        FontStyle style, Vector2 min, Vector2 max)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Text));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = item.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(12, size - 10);
        text.resizeTextMaxSize = size;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
