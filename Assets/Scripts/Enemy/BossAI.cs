using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossAI : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed       = 2f;
    public float phase2SpeedBonus = 1.2f;

    [Header("Phase 1 Attacks")]
    public float normalAttackCooldown  = 1.8f;
    public float specialAttackCooldown = 6f;
    public float specialWarningDuration = 0.5f;

    [Header("Phase 2")]
    public float phase2HPThreshold      = 0.5f;
    public float phase2AttackMultiplier = 1.5f;
    public float rushAttackCooldown     = 4f;
    public float rushSpeed              = 12f;
    public float rushDuration           = 0.3f;

    [Header("Poise Damage")]
    public float normalPoiseDamage  = 20f;
    public float specialPoiseDamage = 50f;

    // ── Runtime ──────────────────────────────────────────────────────────
    EnemyBase      enemy;
    Rigidbody2D    rb;
    DamageFeedback damageFeedback;

    bool  isPhase2;
    bool  enraged;
    float attackTimer;
    float specialTimer;
    float rushTimer;

    enum BossState { Idle, Chase, Attack, SpecialAttack, Rush, Stunned }
    BossState state = BossState.Idle;

    // ── Lifecycle ────────────────────────────────────────────────────────
    void Awake()
    {
        enemy          = GetComponent<EnemyBase>();
        rb             = GetComponent<Rigidbody2D>();
        damageFeedback = GetComponent<DamageFeedback>();
        specialTimer   = specialAttackCooldown;
        rushTimer      = rushAttackCooldown;

        GetComponent<PoiseMeter>().OnPoiseBroken += OnPoiseBreak;
    }

    public void ResetAIState()
    {
        StopAllCoroutines();
        isPhase2     = false;
        enraged      = false;
        attackTimer  = 0f;
        specialTimer = specialAttackCooldown;
        rushTimer    = rushAttackCooldown;
        state        = BossState.Idle;
        rb.linearVelocity = Vector2.zero;
        enemy?.SetAnimBool("isIdle");
    }

    void Update()
    {
        if (enemy.CurrentHP <= 0f) return;

        CheckPhase2();
        attackTimer  -= Time.deltaTime;
        specialTimer -= Time.deltaTime;
        rushTimer    -= Time.deltaTime;

        if (enraged) return;
        if (state == BossState.Stunned  || state == BossState.Attack ||
            state == BossState.SpecialAttack || state == BossState.Rush) return;

        if (!enemy.DetectPlayer())
        {
            state = BossState.Idle;
            enemy.SetAnimBool("isIdle");
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float dist = enemy.GetHorizontalDistToPlayer();
        float dir  = Mathf.Sign(enemy.player.position.x - transform.position.x);
        enemy.SetFacing(dir);

        if (dist <= enemy.attackRange && attackTimer <= 0f)
        {
            rb.linearVelocity = Vector2.zero;
            if (isPhase2 && rushTimer <= 0f)
                StartCoroutine(RushRoutine());
            else if (specialTimer <= 0f)
                StartCoroutine(SpecialAttackRoutine());
            else
                StartCoroutine(NormalAttackRoutine());
        }
        else if (dist > enemy.attackRange)
        {
            state = BossState.Chase;
            enemy.SetAnimBool("isMoving");
            float speed = moveSpeed * (isPhase2 ? phase2SpeedBonus : 1f);
            rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    // ── Phase 2 ──────────────────────────────────────────────────────────
    void CheckPhase2()
    {
        if (isPhase2) return;
        if (enemy.CurrentHP / enemy.maxHP >= phase2HPThreshold) return;

        isPhase2 = true;
        AudioManager.Instance?.PlayBossPhaseChange();
        StartCoroutine(EnrageRoutine());
    }

    IEnumerator EnrageRoutine()
    {
        enraged = true;
        state   = BossState.Idle;
        rb.linearVelocity = Vector2.zero;
        enemy.SetAnimBool("isIdle");
        yield return new WaitForSeconds(1.5f);
        enraged = false;
    }

    // ── Attack Routines ──────────────────────────────────────────────────
    IEnumerator NormalAttackRoutine()
    {
        state       = BossState.Attack;
        attackTimer = normalAttackCooldown;
        rb.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayBossAttack();

        // 交替 atk1 / atk2
        bool useAtk2 = (Random.value > 0.5f && isPhase2);
        enemy.SetAnimBool(useAtk2 ? "isAttacking2" : "isAttacking");

        yield return new WaitForSeconds(0.3f);

        float mult = isPhase2 ? phase2AttackMultiplier : 1f;
        enemy.PerformAttack(enemy.attack * mult);

        yield return new WaitForSeconds(0.4f);
        state = BossState.Chase;
    }

    IEnumerator SpecialAttackRoutine()
    {
        state        = BossState.SpecialAttack;
        attackTimer  = normalAttackCooldown;
        specialTimer = specialAttackCooldown;
        rb.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayBossAttack();

        // 红光预警 + 前摇动画
        damageFeedback?.FlashWarning(specialWarningDuration);
        enemy.SetAnimBool("isSpecialAttacking");

        yield return new WaitForSeconds(specialWarningDuration);

        // 识破判定由 ApplyHitToCollider 内部处理
        float mult = isPhase2 ? phase2AttackMultiplier : 1f;
        enemy.PerformAttack(enemy.attack * 2.5f * mult, isSpecialAttack: true);

        yield return new WaitForSeconds(0.5f);
        state = BossState.Chase;
    }

    IEnumerator RushRoutine()
    {
        state       = BossState.Rush;
        rushTimer   = rushAttackCooldown;
        attackTimer = normalAttackCooldown;
        rb.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayBossRush();

        damageFeedback?.FlashWarning(0.35f);
        enemy.SetAnimBool("isAttacking2");

        yield return new WaitForSeconds(0.4f);

        if (enemy.player != null)
        {
            float dir = Mathf.Sign(enemy.player.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * rushSpeed, rb.linearVelocity.y);
        }

        yield return new WaitForSeconds(rushDuration);

        float rushMult = isPhase2 ? phase2AttackMultiplier : 1f;
        enemy.PerformAttack(enemy.attack * rushMult);
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(0.3f);
        state = BossState.Chase;
    }

    // ── Poise / Stun ─────────────────────────────────────────────────────
    public void OnPoiseBreak()
    {
        StopAllCoroutines();
        StartCoroutine(StunRoutine());
    }

    IEnumerator StunRoutine()
    {
        state = BossState.Stunned;
        enemy.SetAnimBool("isHit");
        rb.linearVelocity = Vector2.zero;

        float stunTime = isPhase2 ? 2f : 3f;
        while (stunTime > 0f && enemy.CurrentHP > 0f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            stunTime -= Time.deltaTime;
            yield return null;
        }

        if (enemy.CurrentHP <= 0f) yield break;

        GetComponent<PoiseMeter>()?.ResetPoise();
        state = BossState.Chase;
    }

    // ── Gizmos ───────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        float range = enemy != null ? enemy.attackRange : 1.5f;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, range);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 20f);
    }
}
