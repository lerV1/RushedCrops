using UnityEngine;
using UnityEngine.UI;

// Makes the merchant react: he watches the player, greets them and comments on trades in a speech bubble.
// Every line can be changed in the Inspector.
[RequireComponent(typeof(Merchant))]
public class MerchantPersonality : MonoBehaviour
{
    private static readonly int GreetTrigger = Animator.StringToHash("Greet");
    private static readonly int RubHandsTrigger = Animator.StringToHash("RubHands");

    [Header("References")]
    [SerializeField] private Animator animator;
    [Tooltip("The player he watches and greets.")]
    [SerializeField] private Transform player;
    [Tooltip("The trading window, so he can talk while it is open.")]
    [SerializeField] private MerchantShopUI shop;
    [SerializeField] private GameObject speechBubble;
    [SerializeField] private Text speechText;

    [Header("Watching")]
    [Tooltip("Within this many metres he turns to watch the player.")]
    [SerializeField, Min(0f)] private float noticeDistance = 9f;
    [Tooltip("Within this many metres he greets the player.")]
    [SerializeField, Min(0f)] private float greetDistance = 5f;
    [Tooltip("Seconds before he greets the player again.")]
    [SerializeField, Min(0f)] private float greetCooldown = 30f;
    [Tooltip("How fast he turns, in degrees per second.")]
    [SerializeField, Min(1f)] private float turnSpeed = 160f;
    [Tooltip("How long a line stays in his speech bubble, in seconds.")]
    [SerializeField, Min(0.5f)] private float speechSeconds = 3.5f;

    [Header("Lines")]
    [SerializeField] private string[] greetings =
    {
        "Ah, a customer! Fresh seeds, fair prices... mostly.",
        "Welcome, friend! My prices are the talk of the valley.",
        "Back again? I knew you'd miss me.",
    };
    [SerializeField] private string[] openingLines =
    {
        "What'll it be?",
        "Take your time. Prices only go up.",
        "Everything you see is a bargain. For me.",
    };
    [SerializeField] private string[] boughtLines =
    {
        "A fine choice! Heh heh.",
        "Pleasure doing business.",
        "You won't find cheaper. Not around here, anyway.",
    };
    [SerializeField] private string[] soldLines =
    {
        "I'll take those off your hands.",
        "Hmm, a bit bruised... but I'm feeling generous.",
        "Lovely. These will fetch a fine... I mean, fair price.",
    };
    [SerializeField] private string[] cantAffordLines =
    {
        "No coin, no goods, friend.",
        "Come back when your purse is heavier.",
    };
    [SerializeField] private string[] noRoomLines =
    {
        "Your pockets are full! Make some room first.",
    };
    [SerializeField] private string[] farewells =
    {
        "Come back soon! Prices may change... in my favour.",
        "Off you go. Mind the evil vegetables.",
    };

    private Merchant merchant;
    private Quaternion restRotation;
    private float lastGreeting = float.NegativeInfinity;
    private float hideSpeechAt;
    private bool playerWasNear;

    private void Awake()
    {
        merchant = GetComponent<Merchant>();
        restRotation = transform.rotation;
        if (speechBubble != null)
        {
            speechBubble.SetActive(false);
        }
    }

    private void OnEnable()
    {
        merchant.Traded += OnTraded;
        merchant.TradeRefused += OnTradeRefused;
        if (shop != null)
        {
            shop.Opened += OnShopOpened;
            shop.Closed += OnShopClosed;
        }
    }

    private void OnDisable()
    {
        merchant.Traded -= OnTraded;
        merchant.TradeRefused -= OnTradeRefused;
        if (shop != null)
        {
            shop.Opened -= OnShopOpened;
            shop.Closed -= OnShopClosed;
        }
    }

    private void Update()
    {
        if (player != null)
        {
            Watch();
        }
        if (speechBubble != null && speechBubble.activeSelf && Time.time >= hideSpeechAt)
        {
            speechBubble.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        // The bubble always faces the camera so it can be read from any side.
        Camera view = Camera.main;
        if (speechBubble != null && speechBubble.activeSelf && view != null)
        {
            speechBubble.transform.rotation = view.transform.rotation;
        }
    }

    private void Watch()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        Quaternion facing = distance < noticeDistance && distance > 0.01f
            ? Quaternion.LookRotation(toPlayer, Vector3.up)
            : restRotation;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);

        bool near = distance < greetDistance;
        if (near && !playerWasNear && Time.time - lastGreeting >= greetCooldown)
        {
            lastGreeting = Time.time;
            Play(GreetTrigger);
            Say(greetings);
        }
        playerWasNear = near;
    }

    private void OnShopOpened(Merchant opened)
    {
        if (opened == merchant)
        {
            Say(openingLines);
        }
    }

    private void OnShopClosed(Merchant closed)
    {
        if (closed == merchant)
        {
            Say(farewells);
        }
    }

    private void OnTraded(ResourceType item, int amount, bool playerBought)
    {
        Play(RubHandsTrigger);
        Say(playerBought ? boughtLines : soldLines);
    }

    private void OnTradeRefused(TradeResult result)
    {
        if (result == TradeResult.NotEnoughCoins)
        {
            Say(cantAffordLines);
        }
        else if (result == TradeResult.NoRoom)
        {
            Say(noRoomLines);
        }
    }

    private void Play(int trigger)
    {
        if (animator != null)
        {
            animator.SetTrigger(trigger);
        }
    }

    private void Say(string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            return;
        }

        string line = lines[Random.Range(0, lines.Length)];
        if (speechText != null)
        {
            speechText.text = line;
            speechBubble.SetActive(true);
            hideSpeechAt = Time.time + speechSeconds;
        }
        if (shop != null && shop.IsOpen && shop.Merchant == merchant)
        {
            shop.ShowRemark(line);
        }
    }
}
