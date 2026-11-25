using UnityEngine;

[DefaultExecutionOrder(1200)]
public class VictoryHud : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject panel;
    [SerializeField, Min(0f)] private float showSeconds = 8f;

    private float timeLeft;

    private void Awake()
    {
        if (gameManager == null || panel == null)
        {
            Debug.LogError("VictoryHud needs its GameManager and victory panel assigned.", this);
            enabled = false;
            return;
        }

        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.VictoryReached += Show;
        }
    }

    private void Start()
    {
        if (gameManager.HasWon)
        {
            Show();
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.VictoryReached -= Show;
        }
    }

    private void Update()
    {
        if (!panel.activeSelf)
        {
            return;
        }

        timeLeft -= Time.unscaledDeltaTime;
        if (timeLeft <= 0f)
        {
            panel.SetActive(false);
        }
    }

    private void Show()
    {
        timeLeft = showSeconds;
        panel.SetActive(true);
    }
}
