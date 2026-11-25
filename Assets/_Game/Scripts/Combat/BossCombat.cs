using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Makes a crop boss fight back. While the player is close it turns to face them and keeps attacking. Every
// attack is shown before it lands: the boss winds up and a red patch fills on the ground where it will hit,
// so a player who watches can step out of the way, or jump over a shockwave. Each boss's attacks and their
// numbers are set in the Inspector, and that is what makes one boss easy and another hard.
// If the player dies, the boss heals and goes back to where it started.
[RequireComponent(typeof(CropBoss))]
public class BossCombat : MonoBehaviour
{
    public enum AttackKind
    {
        Slam,      // hits everything round the boss
        Spit,      // throws shots in a fan at the player
        Charge,    // runs at the player in a straight line
        Shockwave  // a ring that spreads out along the ground; jump over it
    }

    [Serializable]
    public class Attack
    {
        public AttackKind kind = AttackKind.Slam;
        [Tooltip("Only used when the player is at least this far away, in metres.")]
        public float minRange;
        [Tooltip("Only used when the player is at most this far away, in metres.")]
        public float maxRange = 4f;
        [Tooltip("Seconds of warning before it hits.")]
        public float windUp = 1.2f;
        public int damage = 10;
        [Tooltip("Slam and Shockwave: how far it reaches. Charge: how far the boss runs. Spit: how far apart the shots land.")]
        public float reach = 3.5f;
        [Tooltip("Spit: how many shots in the fan.")]
        [Min(1)] public int shots = 1;
        [Tooltip("Seconds the boss rests afterwards: the player's chance to hit back.")]
        public float rest = 2f;
    }

    [Tooltip("The attacks it chooses from. It picks one that fits how far away the player is.")]
    [SerializeField] private Attack[] attacks = Array.Empty<Attack>();
    [Tooltip("It starts fighting when the player comes this close, in metres, and stops at one and a half times this.")]
    [SerializeField, Min(1f)] private float aggroRange = 9f;
    [Tooltip("Metres per second it walks towards the player between attacks. 0 keeps it rooted to the spot.")]
    [SerializeField, Min(0f)] private float moveSpeed;
    [Tooltip("How far, in metres, it may move from where it started.")]
    [SerializeField, Min(0f)] private float leash = 6f;
    [Tooltip("Degrees per second it turns to face the player.")]
    [SerializeField, Min(10f)] private float turnSpeed = 150f;
    [Tooltip("Below this share of its health it gets angry and everything goes faster. 0 means never.")]
    [SerializeField, Range(0f, 1f)] private float angryBelow;
    [Tooltip("How much faster it winds up and rests once angry.")]
    [SerializeField, Min(1f)] private float angrySpeedUp = 1.4f;

    [Header("Parts")]
    [Tooltip("What Spit throws.")]
    [SerializeField] private BossProjectile projectilePrefab;
    [Tooltip("See-through red for the warning patches on the ground.")]
    [SerializeField] private Material warningMaterial;
    [Tooltip("Where shots leave from, relative to the boss.")]
    [SerializeField] private Vector3 mouth = new Vector3(0f, 1.6f, 1.2f);
    [Tooltip("How wide the boss is, in metres, for bumping into things and running into the player.")]
    [SerializeField, Min(0.2f)] private float bodyRadius = 1f;

    private const float SpitFlightTime = 0.9f;
    private const float ShockwaveTime = 0.7f;

    private CropBoss boss;
    private Transform player;
    private Health playerHealth;
    private Collider playerBody;
    private Vector3 home;
    private Quaternion homeRotation;
    private bool engaged;
    private bool facingLocked;
    private bool walking;
    private Coroutine fight;
    [SerializeField] private BossWarnings warnings;

    public bool IsFighting => engaged && !boss.IsDead;
    public bool IsAngry => angryBelow > 0f && boss.Health.CurrentHealth <= boss.Health.MaxHealth * angryBelow;
    public IReadOnlyList<Attack> Attacks => attacks;
    public Vector3 HomePosition => home;
    public Quaternion HomeRotation => homeRotation;

    public void RestoreHome(Vector3 position, Quaternion rotation)
    {
        home = position;
        homeRotation = rotation;
    }

    public void SetPlayer(Health target)
    {
        playerHealth = target;
        player = target != null ? target.transform : null;
        playerBody = target != null ? target.GetComponent<Collider>() : null;
    }

    private float Speed => IsAngry ? angrySpeedUp : 1f;

    private void Awake()
    {
        boss = GetComponent<CropBoss>();
        home = transform.position;
        homeRotation = transform.rotation;
        if (warnings != null) warnings.Detach();
    }

    private void Start()
    {
        if (playerHealth == null || attacks.Length == 0 || warnings == null)
        {
            Debug.LogError($"{name} needs a player in the scene and at least one attack to fight.", this);
            enabled = false;
            return;
        }
        if (playerHealth != null)
        {
            playerHealth.Died += OnPlayerDied;
        }
        boss.Health.Died += OnBossDied;
        fight = StartCoroutine(Fight());
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }
        if (boss != null && boss.Health != null)
        {
            boss.Health.Died -= OnBossDied;
        }
        // The warning patches live beside the boss, not under it, so remove them with it.
        if (warnings != null)
        {
            Destroy(warnings.gameObject);
        }
    }

    private void Update()
    {
        if (player == null || boss.IsDead)
        {
            return;
        }
        float distance = FlatDistance(player.position);
        engaged = !playerHealth.IsDead && (engaged ? distance <= aggroRange * 1.5f : distance <= aggroRange);
        if (engaged && !facingLocked)
        {
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnSpeed * Speed * Time.deltaTime);
            }
        }
        // A little hop while it walks.
        if (!facingLocked)
        {
            boss.PoseOffset = walking ? Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.25f : Vector3.zero;
        }
        walking = false;
    }

    // ---------- Choosing what to do ----------

    private IEnumerator Fight()
    {
        while (!boss.IsDead)
        {
            if (!IsFighting)
            {
                // Out of the fight: wander back to where it started.
                if (moveSpeed > 0f && FlatDistance(home) > 0.3f)
                {
                    walking = true;
                    MoveTowards(home, moveSpeed * Time.deltaTime);
                }
                yield return null;
                continue;
            }

            float distance = FlatDistance(player.position);
            Attack next = Pick(distance);
            if (next == null)
            {
                // Nothing reaches from here: walk closer if it can, otherwise wait for the player.
                if (moveSpeed > 0f && distance > bodyRadius + 1.2f)
                {
                    walking = true;
                    MoveTowards(player.position, moveSpeed * Speed * Time.deltaTime);
                }
                yield return null;
                continue;
            }

            switch (next.kind)
            {
                case AttackKind.Slam: yield return Slam(next); break;
                case AttackKind.Spit: yield return Spit(next); break;
                case AttackKind.Charge: yield return Charge(next); break;
                case AttackKind.Shockwave: yield return Shockwave(next); break;
            }
            ResetPose();
            yield return new WaitForSeconds(next.rest / Speed);
        }
    }

    // One of the attacks that fits the distance, at random, so the fight doesn't repeat the same pattern.
    private Attack Pick(float distance)
    {
        var fitting = new List<Attack>();
        foreach (Attack attack in attacks)
        {
            if (distance >= attack.minRange && distance <= attack.maxRange)
            {
                fitting.Add(attack);
            }
        }
        return fitting.Count == 0 ? null : fitting[UnityEngine.Random.Range(0, fitting.Count)];
    }

    // ---------- The attacks ----------

    // Rises up while a red circle fills round it, then slams down and hurts the player if they are still inside.
    private IEnumerator Slam(Attack attack)
    {
        Vector3 centre = Ground(transform.position);
        warnings.ShowEdge(centre, attack.reach);
        GameAudio.Play(Sfx.BossRoar, transform.position + Vector3.up);
        for (float t = 0f; t < 1f; t += Time.deltaTime * Speed / attack.windUp)
        {
            boss.PoseScale = new Vector3(1f - 0.08f * t, 1f + 0.2f * t, 1f - 0.08f * t);
            warnings.ShowFill(centre, attack.reach * t);
            yield return null;
        }
        boss.PoseScale = new Vector3(1.18f, 0.78f, 1.18f);
        GameEffects.Play(Effect.Dust, centre + Vector3.up * 0.2f, 2f + attack.reach * 0.3f);
        GameAudio.Play(Sfx.BossSlam, centre);
        if (FlatDistanceBetween(player.position, centre) <= attack.reach + PlayerRadius)
        {
            playerHealth.TakeDamage(attack.damage, boss.DisplayName);
        }
        yield return new WaitForSeconds(0.2f);
        warnings.HideAll();
    }

    // Puffs up, then throws a fan of shots at where the player stands. Red spots show where each will land.
    private IEnumerator Spit(Attack attack)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime * Speed / attack.windUp)
        {
            float puff = 1f + 0.15f * t;
            boss.PoseScale = new Vector3(puff, puff, puff);
            yield return null;
        }
        boss.PoseScale = new Vector3(0.92f, 1.05f, 0.92f);

        GameAudio.Play(Sfx.BossSpit, transform.position + Vector3.up);
        Vector3 target = Ground(player.position);
        Vector3 across = Vector3.Cross(Vector3.up, target - transform.position).normalized;
        Vector3 from = transform.TransformPoint(mouth);
        for (int i = 0; i < attack.shots; i++)
        {
            float offset = (i - (attack.shots - 1) / 2f) * attack.reach;
            Vector3 landing = Ground(target + across * offset);
            warnings.ShowSpot(i, landing, 0.8f);
            if (projectilePrefab != null)
            {
                BossProjectile shot = Instantiate(projectilePrefab, from, Quaternion.identity, transform.parent);
                shot.Launch(landing, SpitFlightTime, attack.damage, transform);
            }
        }
        yield return new WaitForSeconds(SpitFlightTime);
        warnings.HideAll();
    }

    // Leans back while a red lane fills towards the player, then runs straight down it. The direction is
    // fixed when the wind-up starts, so stepping sideways dodges it.
    private IEnumerator Charge(Attack attack)
    {
        facingLocked = true;
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        direction.Normalize();
        transform.rotation = Quaternion.LookRotation(direction);
        Vector3 start = Ground(transform.position);
        GameAudio.Play(Sfx.BossRoar, transform.position + Vector3.up);
        for (float t = 0f; t < 1f; t += Time.deltaTime * Speed / attack.windUp)
        {
            boss.PoseTilt = Quaternion.Euler(-14f * t, 0f, 0f);
            warnings.ShowLane(start, direction, attack.reach * t, bodyRadius * 2f);
            yield return null;
        }
        boss.PoseTilt = Quaternion.Euler(10f, 0f, 0f);
        GameAudio.Play(Sfx.BossCharge, transform.position + Vector3.up);

        bool hit = false;
        float runSpeed = attack.reach / 0.55f;
        for (float run = 0f; run < attack.reach; run += runSpeed * Time.deltaTime)
        {
            if (!MoveTowards(transform.position + direction * 5f, runSpeed * Time.deltaTime))
            {
                GameEffects.Play(Effect.Dust, transform.position + direction * bodyRadius + Vector3.up * 0.5f, 1.5f);
                break; // ran into something
            }
            if (Time.frameCount % 4 == 0) GameEffects.Play(Effect.Dust, Ground(transform.position) + Vector3.up * 0.1f, 0.25f);
            if (!hit && FlatDistance(player.position) <= bodyRadius + PlayerRadius)
            {
                hit = true;
                playerHealth.TakeDamage(attack.damage, boss.DisplayName);
            }
            yield return null;
        }
        warnings.HideAll();
        yield return new WaitForSeconds(0.3f);
        facingLocked = false;
    }

    // Jumps up, lands, and a ring spreads out along the ground to Reach. Anyone standing on the ground when it
    // passes is hurt; jumping over it is safe.
    private IEnumerator Shockwave(Attack attack)
    {
        facingLocked = true;
        Vector3 centre = Ground(transform.position);
        warnings.ShowEdge(centre, attack.reach);
        GameAudio.Play(Sfx.BossRoar, transform.position + Vector3.up);
        for (float t = 0f; t < 1f; t += Time.deltaTime * Speed / attack.windUp)
        {
            boss.PoseOffset = Vector3.up * Mathf.Sin(t * Mathf.PI * 0.5f) * 1.4f;
            yield return null;
        }
        boss.PoseOffset = Vector3.zero;
        boss.PoseScale = new Vector3(1.2f, 0.75f, 1.2f);
        GameEffects.Play(Effect.Dust, centre + Vector3.up * 0.2f, 3f);
        GameAudio.Play(Sfx.BossSlam, centre);

        bool hit = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime / ShockwaveTime)
        {
            float radius = Mathf.Lerp(bodyRadius, attack.reach, t);
            warnings.ShowFill(centre, radius);
            float gap = Mathf.Abs(FlatDistanceBetween(player.position, centre) - radius);
            if (!hit && gap <= 0.8f && PlayerOnGround())
            {
                hit = true;
                playerHealth.TakeDamage(attack.damage, boss.DisplayName);
            }
            yield return null;
        }
        warnings.HideAll();
        facingLocked = false;
    }

    // ---------- When someone dies ----------

    private void OnPlayerDied()
    {
        if (boss.IsDead)
        {
            return;
        }
        if (fight != null)
        {
            StopCoroutine(fight);
        }
        warnings.HideAll();
        boss.ResetFight();
        ResetPose();
        engaged = false;
        transform.SetPositionAndRotation(home, homeRotation);
        fight = StartCoroutine(Fight());
    }

    private void OnBossDied()
    {
        if (fight != null)
        {
            StopCoroutine(fight);
        }
        warnings.HideAll();
        engaged = false;
    }

    private void ResetPose()
    {
        boss.PoseScale = Vector3.one;
        boss.PoseOffset = Vector3.zero;
        boss.PoseTilt = Quaternion.identity;
        facingLocked = false;
    }

    // ---------- Moving ----------

    // Moves up to maxStep towards target, staying within the leash. Returns false if something is in the way.
    private bool MoveTowards(Vector3 target, float maxStep)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.01f)
        {
            return true;
        }
        Vector3 step = to.normalized * Mathf.Min(maxStep, to.magnitude);
        Vector3 next = transform.position + step;
        Vector3 fromHome = next - home;
        fromHome.y = 0f;
        if (fromHome.magnitude > leash)
        {
            return false;
        }
        if (Blocked(step))
        {
            return false;
        }
        next.y = Ground(next).y;
        transform.position = next;
        return true;
    }

    private bool Blocked(Vector3 step)
    {
        Vector3 from = transform.position + Vector3.up * (bodyRadius + 0.35f);
        foreach (RaycastHit hit in Physics.SphereCastAll(from, bodyRadius, step.normalized, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(player))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    // ---------- Measuring ----------

    private float PlayerRadius => playerBody != null ? Mathf.Max(playerBody.bounds.extents.x, playerBody.bounds.extents.z) : 0.4f;

    private float FlatDistance(Vector3 point) => FlatDistanceBetween(point, transform.position);

    private static float FlatDistanceBetween(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private bool PlayerOnGround()
    {
        float feet = playerBody != null ? playerBody.bounds.min.y : player.position.y - 1f;
        return feet - Ground(player.position).y < 0.3f;
    }

    // The ground under a point, ignoring the boss and the player.
    private Vector3 Ground(Vector3 at)
    {
        foreach (RaycastHit hit in SortedDown(at))
        {
            if (hit.collider.transform.IsChildOf(transform) || (player != null && hit.collider.transform.IsChildOf(player)))
            {
                continue;
            }
            return hit.point;
        }
        return new Vector3(at.x, transform.position.y, at.z);
    }

    private static RaycastHit[] SortedDown(Vector3 at)
    {
        RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * 4f, Vector3.down, 10f, 1 << 6, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

}
