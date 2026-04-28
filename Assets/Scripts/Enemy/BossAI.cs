using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossAI : MonoBehaviour
{
    [Header("Detection")]
    public float attackRange = 1.5f;
    public LayerMask playerLayer;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float phase2SpeedBonus = 1.2f;

    [Header("Phase 1 Attacks")]
    public float normalAttackCooldown = 1.8f;
    public float specialAttackCooldown = 6f;

    [Header("Phase 2")]
    public float phase2HPThreshold = 0.5f;
    public float phase2AttackMultiplier = 1.5f;
    public float rushAttackCooldown = 4f;
    public float rushSpeed = 12f;
    public float rushDuration = 0.3f;

    [Header("Poise Damage on Hit")]
    public float normalPoiseDamage = 20f;
    public float specialPoiseDamage = 50f;

    EnemyBase enemy;
    Rigidbody2D rb;
    Transform player;

    bool isPhase2;
    bool enraged;
    float attackTimer;
    float specialTimer;
    float rushTimer;

    enum BossState { Idle, Chase, Attack, SpecialAttack, Rush, Stunned }
    BossState state = BossState.Idle;

    void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        rb = GetComponent<Rigidbody2D>();
        GetComponent<PoiseMeter>().OnPoiseBroken += OnStunned;
    }

    void Update()
    {
        if (enemy.CurrentHP <= 0f) return;

        CheckPhase2();
        attackTimer -= Time.deltaTime;
        specialTimer -= Time.deltaTime;
        rushTimer -= Time.deltaTime;

        if (enraged) return;
        if (state == BossState.Stunned || state == BossState.Attack ||
            state == BossState.SpecialAttack || state == BossState.Rush) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, 20f, playerLayer);
        if (hit == null)
        {
            state = BossState.Idle;
            enemy.SetAnimationState(0);
            rb.linearVelocity = Vector2.zero;
            return;
        }

        player = hit.transform;
        float dist = Vector2.Distance(transform.position, player.position);

        if (dist <= attackRange && attackTimer <= 0f)
        {
            if (isPhase2 && rushTimer <= 0f && dist > attackRange * 0.5f)
                StartCoroutine(RushRoutine());
            else if (specialTimer <= 0f)
                StartCoroutine(SpecialAttackRoutine());
            else
                StartCoroutine(NormalAttackRoutine());
        }
        else if (dist > attackRange)
        {
            state = BossState.Chase;
            enemy.SetAnimationState(1);
            float speed = moveSpeed * (isPhase2 ? phase2SpeedBonus : 1f);
            float dir = Mathf.Sign(player.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        }
    }

    void CheckPhase2()
    {
        if (isPhase2) return;
        if (enemy.CurrentHP / enemy.maxHP < phase2HPThreshold)
        {
            isPhase2 = true;
            StartCoroutine(EnrageRoutine());
        }
    }

    IEnumerator EnrageRoutine()
    {
        enraged = true;
        rb.linearVelocity = Vector2.zero;
        state = BossState.Idle;
        enemy.SetAnimationState(0);
        yield return new WaitForSeconds(1.5f);
        enraged = false;
    }

    IEnumerator NormalAttackRoutine()
    {
        state = BossState.Attack;
        enemy.SetAnimationState(2);
        attackTimer = normalAttackCooldown;
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(0.25f);
        float mult = isPhase2 ? phase2AttackMultiplier : 1f;
        HitPlayer(enemy.attack * mult, normalPoiseDamage);

        yield return new WaitForSeconds(0.3f);
        state = BossState.Chase;
    }

    IEnumerator SpecialAttackRoutine()
    {
        state = BossState.SpecialAttack;
        enemy.SetAnimationState(3);
        attackTimer = normalAttackCooldown;
        specialTimer = specialAttackCooldown;
        rb.linearVelocity = Vector2.zero;

        // Telegraph window — player can counter here
        yield return new WaitForSeconds(0.8f);

        if (player != null)
        {
            var counter = player.GetComponent<PlayerCounter>();
            if (counter != null && counter.TryCounter(enemy))
            {
                state = BossState.Chase;
                yield break;
            }

            float mult = isPhase2 ? phase2AttackMultiplier : 1f;
            HitPlayer(enemy.attack * 2.5f * mult, specialPoiseDamage);
        }

        yield return new WaitForSeconds(0.5f);
        state = BossState.Chase;
    }

    IEnumerator RushRoutine()
    {
        state = BossState.Rush;
        enemy.SetAnimationState(3);
        rushTimer = rushAttackCooldown;
        attackTimer = normalAttackCooldown;
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(0.4f);

        if (player != null)
        {
            float dir = Mathf.Sign(player.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * rushSpeed, rb.linearVelocity.y);
        }

        yield return new WaitForSeconds(rushDuration);
        HitPlayer(enemy.attack * (isPhase2 ? phase2AttackMultiplier : 1f), normalPoiseDamage);
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(0.3f);
        state = BossState.Chase;
    }

    void HitPlayer(float damage, float poiseDamage)
    {
        if (player == null) return;
        var dodge = player.GetComponent<PlayerDodge>();
        if (dodge != null && dodge.IsInvincible) return;

        var block = player.GetComponent<PlayerBlock>();
        var feedback = player.GetComponent<DamageFeedback>();
        if (feedback != null) feedback.ApplyKnockback(transform.position, block != null && block.IsBlocking ? 3f : 7f);

        if (block != null && block.IsBlocking)
            block.ReceiveAttack(damage);
        else
            player.GetComponent<PlayerStats>()?.TakeDamage(damage);
    }

    void OnStunned()
    {
        StartCoroutine(StunRoutine());
    }

    IEnumerator StunRoutine()
    {
        state = BossState.Stunned;
        enemy.SetAnimationState(4);
        rb.linearVelocity = Vector2.zero;
        float stunTime = isPhase2 ? 2f : 3f;
        yield return new WaitForSeconds(stunTime);
        GetComponent<PoiseMeter>().ResetPoise();
        state = BossState.Chase;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 20f);
    }
}
