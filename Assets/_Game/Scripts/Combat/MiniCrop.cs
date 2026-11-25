using System.Collections;
using UnityEngine;

// A small evil crop that climbs out of the crop field. Aggressive ones run at the player as soon as they see
// them. Grumpy ones leave the player alone until they are hit, then fight back.
// It thinks with a small state machine: Wander -> Chase -> Attack, and back to Wander when the player gets
// away or dies. The hatchet hurts it through its Health; when it dies it drops some of its crop.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
public class MiniCrop : MonoBehaviour, IInteractable
{
    public enum Temper
    {
        Aggressive, // attacks the player on sight
        Grumpy      // only attacks after being hit
    }

    public enum State
    {
        Wander,
        Chase,
        Attack,
        Dead
    }

    [SerializeField] private string displayName = "Evil Carrot";
    [SerializeField] private Temper temper = Temper.Aggressive;
    [Tooltip("The crop this is. When it dies it drops this crop's harvest.")]
    [SerializeField] private CropData crop;
    [SerializeField, Min(1)] private int dropAmount = 1;
    [Tooltip("The model, which hops while walking and squashes before a bite.")]
    [SerializeField] private Transform model;
    [Tooltip("What is thrown out when it dies.")]
    [SerializeField] private ItemDrop dropPrefab;
    [Tooltip("The rare boss seed it can drop instead of its crop. Leave empty if its crop has no boss.")]
    [SerializeField] private CropData bossSeed;
    [Tooltip("Chance, from 0 to 1, of dropping the boss seed instead of its crop. 0.05 is 5%.")]
    [SerializeField, Range(0f, 1f)] private float bossSeedChance = 0.05f;

    [Header("Moving (metres per second)")]
    [SerializeField, Min(0f)] private float wanderSpeed = 1.2f;
    [SerializeField, Min(0f)] private float chaseSpeed = 3f;
    [Tooltip("Degrees per second it turns.")]
    [SerializeField, Min(10f)] private float turnSpeed = 360f;
    [Tooltip("How far from where it climbed out it wanders, in metres.")]
    [SerializeField, Min(0f)] private float wanderRadius = 3f;

    [Header("Senses (metres)")]
    [Tooltip("An aggressive crop starts chasing when the player is this close.")]
    [SerializeField, Min(1f)] private float sightRange = 8f;
    [Tooltip("It gives up when the player gets this far away.")]
    [SerializeField, Min(1f)] private float giveUpRange = 14f;

    [Header("Attack")]
    [Tooltip("It starts a bite when the player is this close. It goes back to chasing at this plus 0.5, so it doesn't flicker between the two.")]
    [SerializeField, Min(0.5f)] private float attackRange = 1.4f;
    [SerializeField, Min(1)] private int damage = 6;
    [Tooltip("Seconds it squashes down before the bite lands: the player's warning.")]
    [SerializeField, Min(0f)] private float windUp = 0.45f;
    [Tooltip("Seconds between bites.")]
    [SerializeField, Min(0.1f)] private float cooldown = 1.2f;
    [Tooltip("How far above or below its feet, in metres, the player's feet can be and still get bitten. Jumping higher dodges a bite.")]
    [SerializeField, Min(0f)] private float biteHeight = 0.8f;
    [Tooltip("Layers that stop a bite, like fences and walls, so it can't bite through them.")]
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;

    // Given by the MiniCropSpawner when the crop climbs out, because a prefab can't point at scene objects.
    private Inventory inventory;
    private Health playerHealth;
    private Collider playerBody;

    private State state = State.Wander;
    private bool angry;
    private bool biting;
    private Health health;
    private Rigidbody body;
    private Knockback knockback;
    private Vector3 home;
    private Vector3 wanderTarget;
    private float nextWanderTime;
    private float nextBiteTime;
    private float riseLeft;
    private Vector3 modelScale;
    private Vector3 modelPosition;

    private const float RiseTime = 0.6f;
    private const float LeaveAttackGap = 0.5f;

    public State CurrentState => state;
    public Temper Temperament => temper;
    public string DisplayName => displayName;
    public CropData Crop => crop;
    public CropData BossSeed => bossSeed;
    public float BossSeedChance => bossSeedChance;
    public bool IsAngry => angry;
    public Health Health => health;

    private void Awake()
    {
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody>();
        knockback = GetComponent<Knockback>();
        if (model == null)
        {
            model = transform;
        }
        modelScale = model.localScale;
        modelPosition = model.localPosition;
        home = transform.position;
        wanderTarget = home;
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    // Called by the spawner right after it makes the crop.
    public void Setup(Inventory sharedInventory, Health player)
    {
        inventory = sharedInventory;
        playerHealth = player;
        playerBody = player != null ? player.GetComponent<Collider>() : null;
        home = transform.position;
        wanderTarget = home;
        // Climb out of the soil: start tiny and grow to full size.
        riseLeft = RiseTime;
        model.localScale = Vector3.zero;
    }

    public bool CanInteract => false;

    public string GetPrompt()
    {
        if (state == State.Dead)
        {
            return null;
        }
        string mood = temper == Temper.Aggressive || angry ? "hostile" : "leave it alone";
        return $"{displayName}, {mood} ({health.CurrentHealth}/{health.MaxHealth} HP)";
    }

    public void Interact(PlayerInteractor player) { }

    // ---------- Think: choose the state ----------

    private void Update()
    {
        if (state == State.Dead)
        {
            return;
        }
        UpdateModel();
        if (playerHealth == null || riseLeft > 0f)
        {
            return;
        }

        float distance = FlatDistance(playerHealth.transform.position);
        bool playerAlive = !playerHealth.IsDead;

        switch (state)
        {
            case State.Wander:
                if (playerAlive && WantsToFight() && distance <= sightRange)
                {
                    state = State.Chase;
                }
                else if (Time.time >= nextWanderTime)
                {
                    PickWanderSpot();
                }
                break;

            case State.Chase:
                if (!playerAlive || distance > giveUpRange)
                {
                    CalmDown();
                }
                else if (CanBite(attackRange))
                {
                    state = State.Attack;
                }
                break;

            case State.Attack:
                if (!playerAlive)
                {
                    CalmDown();
                }
                else if (!biting && !CanBite(attackRange + LeaveAttackGap))
                {
                    state = State.Chase;
                }
                else if (Time.time >= nextBiteTime && !biting)
                {
                    StartCoroutine(Bite());
                }
                break;
        }
    }

    private bool WantsToFight()
    {
        return temper == Temper.Aggressive || angry;
    }

    private void CalmDown()
    {
        angry = false;
        state = State.Wander;
        PickWanderSpot();
    }

    private void PickWanderSpot()
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        wanderTarget = home + new Vector3(offset.x, 0f, offset.y);
        nextWanderTime = Time.time + Random.Range(2f, 4f);
    }

    // ---------- Act: move the body ----------

    private void FixedUpdate()
    {
        // A hit's knockback moves it for a moment, so leave the velocity alone.
        if (state == State.Dead || (knockback != null && knockback.IsActive))
        {
            return;
        }

        Vector3 target = transform.position;
        float speed = 0f;
        if (riseLeft <= 0f && !biting)
        {
            if (state == State.Wander)
            {
                target = wanderTarget;
                speed = wanderSpeed;
            }
            else if (state == State.Chase)
            {
                target = playerHealth.transform.position;
                speed = chaseSpeed;
            }
        }

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < 0.2f)
        {
            speed = 0f;
        }
        Vector3 move = toTarget.normalized * speed;
        // Keep the up and down speed, so gravity still works.
        body.linearVelocity = new Vector3(move.x, body.linearVelocity.y, move.z);

        // Face where it is going, or face the player while attacking.
        Vector3 look = state == State.Attack && playerHealth != null ? playerHealth.transform.position - transform.position : move;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
        {
            Quaternion facing = Quaternion.LookRotation(look);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, facing, turnSpeed * Time.fixedDeltaTime));
        }
    }

    // Squashes down as a warning, then lunges. The player is hurt only if still close when it lands.
    private IEnumerator Bite()
    {
        biting = true;
        nextBiteTime = Time.time + windUp + cooldown;
        GameAudio.Play(Sfx.MiniBite, transform.position);
        yield return new WaitForSeconds(windUp);

        if (state != State.Dead && !playerHealth.IsDead
            && CanBite(attackRange + LeaveAttackGap))
        {
            playerHealth.TakeDamage(damage, displayName);
        }
        biting = false;
    }

    // Grows in while rising, hops while walking and squashes while winding up a bite.
    private void UpdateModel()
    {
        if (riseLeft > 0f)
        {
            riseLeft = Mathf.Max(riseLeft - Time.deltaTime, 0f);
            model.localScale = modelScale * (1f - riseLeft / RiseTime);
            return;
        }

        Vector3 scale = Vector3.one;
        Vector3 offset = Vector3.zero;
        if (biting)
        {
            scale = new Vector3(1.15f, 0.8f, 1.15f);
        }
        else if (new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z).magnitude > 0.3f)
        {
            offset = Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.08f;
        }
        model.localScale = Vector3.Scale(modelScale, scale);
        model.localPosition = modelPosition + offset;
    }

    // ---------- Getting hurt ----------

    private void OnDamaged(int amount)
    {
        // Any hit makes it angry, so a grumpy crop now fights back.
        angry = true;
        GameEffects.Play(Effect.Juice, transform.position + Vector3.up * 0.5f, 0.6f);
        GameAudio.Play(Sfx.MiniHurt, transform.position);
        if (state == State.Wander)
        {
            state = State.Chase;
        }
    }

    private void OnDied()
    {
        state = State.Dead;
        StopAllCoroutines();
        biting = false;
        foreach (Collider part in GetComponentsInChildren<Collider>())
        {
            part.enabled = false;
        }
        // Turn the push off first: a kinematic body can't be given a velocity, and the hatchet pushes
        // right after the killing hit.
        if (knockback != null)
        {
            knockback.Stop();
            knockback.enabled = false;
        }
        body.isKinematic = true;
        GameEffects.Play(Effect.Juice, transform.position + Vector3.up * 0.4f, 1.4f);
        GameAudio.Play(Sfx.MiniDie, transform.position);
        GameEffects.Play(Effect.Dust, transform.position + Vector3.up * 0.2f, 0.5f);
        DropCrop();
        StartCoroutine(ShrinkAway());
    }

    private void DropCrop()
    {
        if (dropPrefab == null || inventory == null || crop == null)
        {
            Debug.LogError($"{name} needs a drop prefab, a crop and the Inventory to drop its crop.", this);
            return;
        }
        // Roll once: a number from 0 to 1 picks the boss seed or the normal crop.
        ResourceType item = ChooseDrop(Random.value);
        int amount = item == crop.HarvestResource ? dropAmount : 1;

        Vector2 offset = Random.insideUnitCircle.normalized * 1.2f;
        Vector3 landing = transform.position + new Vector3(offset.x, 0f, offset.y);
        ItemDrop drop = Instantiate(dropPrefab, transform.position + Vector3.up, Quaternion.identity, transform.parent);
        drop.Launch(inventory, item, amount, landing);
    }

    // What to drop for a roll between 0 and 1. A roll under the chance (under 0.05 for 5%) gives the boss seed;
    // any other roll, or a crop with no boss seed, gives the normal crop.
    public ResourceType ChooseDrop(float roll)
    {
        if (bossSeed != null && roll < bossSeedChance)
        {
            return bossSeed.SeedResource;
        }
        return crop.HarvestResource;
    }

    private IEnumerator ShrinkAway()
    {
        const float shrinkTime = 0.4f;
        for (float t = 0f; t < shrinkTime; t += Time.deltaTime)
        {
            model.localScale = modelScale * (1f - t / shrinkTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    // Sense: is the player close enough along the ground, at about the same height, and not behind a fence or wall?
    private bool CanBite(float reach)
    {
        if (FlatDistance(playerHealth.transform.position) > reach)
        {
            return false;
        }

        // Compare feet: a player standing on something high, or in the air, is out of reach.
        float playerFeet = playerBody != null ? playerBody.bounds.min.y : playerHealth.transform.position.y;
        if (Mathf.Abs(playerFeet - transform.position.y) > biteHeight)
        {
            return false;
        }

        return !IsBlocked();
    }

    // True when something solid sits on the line from this crop to the middle of the player.
    private bool IsBlocked()
    {
        Vector3 from = transform.position + Vector3.up * 0.5f;
        Vector3 to = playerBody != null ? playerBody.bounds.center : playerHealth.transform.position + Vector3.up;
        if (!Physics.Linecast(from, to, out RaycastHit hit, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }
        // Reaching the player, or passing through this crop itself, is not blocked.
        Transform hitObject = hit.collider.transform;
        return !hitObject.IsChildOf(playerHealth.transform) && !hitObject.IsChildOf(transform);
    }

    private float FlatDistance(Vector3 point)
    {
        Vector3 between = point - transform.position;
        between.y = 0f;
        return between.magnitude;
    }
}
