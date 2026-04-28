using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 6f;
    public float attackRange = 1.2f;
    public LayerMask playerLayer;

    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float patrolDistance = 3f;
    public float preferredCombatDistance = 1.05f;
    public float retreatDistance = 0.75f;

    [Header("Attack")]
    public float attackCooldown = 1.5f;
    public float specialAttackCooldown = 5f;
    public float poiseDamagePerHit = 15f;

    [Header("Special Attack")]
    public float specialAttackWarningDuration = 0.6f;

    [Header("Stun")]
    public float stunDuration = 3f;

    EnemyBase enemy;
    Rigidbody2D rb;
    Animator anim;
    Transform player;

    Vector2 patrolOrigin;
    float patrolDir = 1f;
    float attackTimer;
    float specialTimer;

    enum AIState { Patrol, Chase, Attack, SpecialAttack, Stunned }
    AIState aiState = AIState.Patrol;

    void SetAIState(AIState s)
    {
        aiState = s;
        if (anim != null && anim.runtimeAnimatorController != null)
            anim.SetInteger("State", (int)s);
    }

    void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        patrolOrigin = transform.position;
        specialTimer = specialAttackCooldown;

        int playerLayerIndex = LayerMask.NameToLayer("Player");
        if (playerLayerIndex >= 0 && (playerLayer.value & (1 << playerLayerIndex)) == 0)
            playerLayer = 1 << playerLayerIndex;

        GetComponent<PoiseMeter>().OnPoiseBroken += OnStunned;
    }

    void Update()
    {
        if (enemy.CurrentHP <= 0f) return;

        attackTimer -= Time.deltaTime;
        specialTimer -= Time.deltaTime;

        DetectPlayer();

        switch (aiState)
        {
            case AIState.Patrol:       Patrol(); break;
            case AIState.Chase:        Chase(); break;
            case AIState.Attack:       break;
            case AIState.SpecialAttack: break;
            case AIState.Stunned:      break;
        }
    }

    void DetectPlayer()
    {
        if (aiState == AIState.Stunned || aiState == AIState.Attack || aiState == AIState.SpecialAttack) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
        if (hit != null)
        {
            player = hit.transform;
            float dist = GetHorizontalDistanceToPlayer();

            if (dist <= attackRange && attackTimer <= 0f)
            {
                if (specialTimer <= 0f)
                    StartCoroutine(SpecialAttackRoutine());
                else
                    StartCoroutine(NormalAttackRoutine());
            }
            else if (dist <= preferredCombatDistance)
            {
                SetAIState(AIState.Chase);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            else if (dist > attackRange)
            {
                SetAIState(AIState.Chase);
            }
        }
        else
        {
            player = null;
            SetAIState(AIState.Patrol);
        }
    }

    void Patrol()
    {
        float targetX = patrolOrigin.x + patrolDir * patrolDistance;
        float dist = Mathf.Abs(transform.position.x - targetX);

        rb.linearVelocity = new Vector2(patrolDir * moveSpeed, rb.linearVelocity.y);

        if (dist < 0.1f) patrolDir *= -1f;
    }

    void Chase()
    {
        if (player == null) return;
        float deltaX = player.position.x - transform.position.x;
        float absX = GetHorizontalDistanceToPlayer();
        float dir = Mathf.Sign(deltaX);

        if (absX < retreatDistance)
        {
            rb.linearVelocity = new Vector2(-dir * moveSpeed * 0.6f, rb.linearVelocity.y);
            return;
        }

        if (absX <= preferredCombatDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
    }

    float GetHorizontalDistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Mathf.Abs(player.position.x - transform.position.x);
    }

    IEnumerator NormalAttackRoutine()
    {
        SetAIState(AIState.Attack);
        attackTimer = attackCooldown;
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(0.2f);
        HitPlayer(enemy.attack, enemy.attack * 0.5f);

        yield return new WaitForSeconds(0.3f);
        SetAIState(player != null ? AIState.Chase : AIState.Patrol);
    }

    IEnumerator SpecialAttackRoutine()
    {
        SetAIState(AIState.SpecialAttack);
        attackTimer = attackCooldown;
        specialTimer = specialAttackCooldown;
        rb.linearVelocity = Vector2.zero;

        // Warning window — player can counter during this delay
        yield return new WaitForSeconds(specialAttackWarningDuration);

        if (player != null)
        {
            var counter = player.GetComponent<PlayerCounter>();
            if (counter != null && counter.TryCounter(enemy))
            {
                SetAIState(AIState.Chase);
                yield break;
            }

            HitPlayer(enemy.attack * 2f, enemy.attack * 1.5f);
        }

        yield return new WaitForSeconds(0.4f);
        SetAIState(player != null ? AIState.Chase : AIState.Patrol);
    }

    void HitPlayer(float damage, float poiseDamage)
    {
        if (player == null) return;

        var block = player.GetComponent<PlayerBlock>();
        var stats = player.GetComponent<PlayerStats>();

        if (stats != null && stats.IsInvulnerable) return;

        var feedback = player.GetComponent<DamageFeedback>();
        bool isBlocking = block != null && block.IsBlocking;
        if (feedback != null && !isBlocking) feedback.ApplyKnockback(transform.position, 5f);

        if (isBlocking)
        {
            block.ReceiveAttack(damage);
        }
        else
        {
            stats?.TakeDamage(damage);
        }
    }

    void OnStunned()
    {
        StopAllCoroutines();
        StartCoroutine(StunRoutine());
    }

    IEnumerator StunRoutine()
    {
        SetAIState(AIState.Stunned);
        rb.linearVelocity = Vector2.zero;
        attackTimer = attackCooldown;
        specialTimer = Mathf.Max(specialTimer, 0.5f);

        float timer = stunDuration;
        while (timer > 0f && enemy.CurrentHP > 0f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            timer -= Time.deltaTime;
            yield return null;
        }

        if (enemy.CurrentHP <= 0f) yield break;

        GetComponent<PoiseMeter>().ResetPoise();
        SetAIState(player != null ? AIState.Chase : AIState.Patrol);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredCombatDistance);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
    }
}
