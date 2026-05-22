using System.Collections;
using UnityEngine;
using System;

[RequireComponent(typeof(PoiseMeter))]
public class EnemyBase : Entity
{
    [Header("Save")]
    public string saveID;
    public bool permanentDeath;

    [Header("Stats")]
    public float maxHP    = 50f;
    public float attack   = 8f;
    public int   goldDrop = 10;

    [Header("Detection")]
    public float     detectionRange = 6f;
    public float     attackRange    = 1.2f;
    public LayerMask playerLayer;

    [Header("Movement")]
    public float patrolMoveSpeed         = 2f;
    public float battleMoveSpeed         = 3.5f;
    public float patrolDistance          = 3f;
    public float preferredCombatDistance = 1.05f;
    public float retreatDistance         = 0.75f;

    [Header("Attack")]
    public float   attackCooldown        = 1.5f;
    public float   specialAttackCooldown = 5f;
    public float   poiseDamagePerHit     = 15f;
    public Vector2 attackCheckOffset     = new Vector2(0.5f, 0f);
    public Vector2 attackHitboxSize      = new Vector2(0.8f, 0.6f);

    [Header("Special Attack")]
    public float specialAttackWarningDuration = 0.5f;
    public float dashAttackSpeed             = 8f;
    public float dashAttackDuration          = 0.35f;

    [Header("Idle / Battle")]
    public float idleTime          = 1.5f;
    public float battleTimeDuration = 5f;

    [Header("Stun")]
    public float stunDuration = 3f;

    // ── Runtime State ────────────────────────────────────────────────
    public float     CurrentHP            { get; protected set; }
    public Transform player               { get; protected set; }
    public float     attackCooldownTimer  { get; protected set; }
    public float     specialCooldownTimer { get; protected set; }
    public Vector2   PatrolOrigin         { get; private set; }

    public bool IsDefeated          => CurrentHP <= 0f;
    public bool SavesPermanentDeath => permanentDeath || GetComponent<BossAI>() != null;
    public bool RespawnsAtCheckpoint => !SavesPermanentDeath;
    public bool IsExecutable        => GetComponent<PoiseMeter>().IsBroken && CurrentHP > 0f;
    public string SaveID            => SaveIdUtility.GetSceneObjectID(this, saveID);

    // ── Events ───────────────────────────────────────────────────────
    public event Action<float, float> OnHPChanged;
    public event Action OnDied;
    public event Action OnHit;

    protected PoiseMeter poiseMeter;
    Vector3    initialPosition;
    Quaternion initialRotation;
    Vector3    initialScale;
    Coroutine  deathRoutine;

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale    = transform.localScale;
        PatrolOrigin    = transform.position;
        CurrentHP       = maxHP;
        poiseMeter      = GetComponent<PoiseMeter>();
        poiseMeter.OnPoiseBroken += OnPoiseBroken;

        int playerLayerIndex = LayerMask.NameToLayer("Player");
        if (playerLayerIndex >= 0 && (playerLayer.value & (1 << playerLayerIndex)) == 0)
            playerLayer = 1 << playerLayerIndex;

        specialCooldownTimer = specialAttackCooldown;
    }

    // ── AI Utilities (used by enemy states) ──────────────────────────
    public bool DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
        if (hit != null) player = hit.transform;
        return hit != null;
    }

    public void LoseTarget() => player = null;

    public float GetHorizontalDistToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Mathf.Abs(player.position.x - transform.position.x);
    }

    public void HitPlayer(float damage, float poiseDamage, bool isSpecialAttack = false)
    {
        if (player == null) return;
        ApplyHitToCollider(player.GetComponent<Collider2D>() ?? player.GetComponentInChildren<Collider2D>(), damage, isSpecialAttack);
    }

    public void PerformAttack(float damage, bool isSpecialAttack = false)
    {
        Vector2 origin = (Vector2)transform.position +
                         new Vector2(attackCheckOffset.x * FacingDir, attackCheckOffset.y);
        var hits = Physics2D.OverlapBoxAll(origin, attackHitboxSize, 0f, playerLayer);
        foreach (var hit in hits)
            ApplyHitToCollider(hit, damage, isSpecialAttack);
    }

    void ApplyHitToCollider(Collider2D hit, float damage, bool isSpecialAttack = false)
    {
        if (hit == null) return;
        var ctrl  = hit.GetComponent<PlayerController>()  ?? hit.GetComponentInParent<PlayerController>();
        var stats = hit.GetComponent<PlayerStats>()       ?? hit.GetComponentInParent<PlayerStats>();
        if (stats == null || stats.IsInvulnerable) return;

        bool isBlocking   = ctrl != null && ctrl.IsBlocking;
        bool isCountering = ctrl != null && ctrl.IsCountering;
        var  feedback     = hit.GetComponent<DamageFeedback>() ?? hit.GetComponentInParent<DamageFeedback>();

        if (isSpecialAttack)
        {
            // 特殊攻击：识破成功 → 不受伤，给boss poise伤害
            if (isCountering && ctrl.TryCounter(this)) return;
            // 特殊攻击无视格挡 → 全伤
            stats.TakeDamage(damage);
            feedback?.ApplyKnockback(transform.position, 5f);
            return;
        }

        // 普通攻击：识破状态无法防御 → 全伤
        if (isCountering)
        {
            stats.TakeDamage(damage);
            feedback?.ApplyKnockback(transform.position, 5f);
            return;
        }

        if (feedback != null && !isBlocking) feedback.ApplyKnockback(transform.position, 5f);
        if (isBlocking) ctrl.ReceiveBlockHit(damage);
        else            stats.TakeDamage(damage);
    }

    public bool TryTriggerCounter()
    {
        if (player == null) return false;
        var ctrl = player.GetComponent<PlayerController>();
        return ctrl != null && ctrl.TryCounter(this);
    }

    public void StartAttackCooldown()  => attackCooldownTimer  = attackCooldown;
    public void StartSpecialCooldown() => specialCooldownTimer = specialAttackCooldown;
    public void ResetPoise()           => poiseMeter?.ResetPoise();

    // ── Damage / Death ───────────────────────────────────────────────
    public virtual void TakeDamage(float damage) => TakeDamage(damage, 0f);

    public virtual void TakeDamage(float damage, float poiseDamage)
    {
        if (CurrentHP <= 0f) return;
        CurrentHP = Mathf.Max(CurrentHP - damage, 0f);
        Debug.Log($"{gameObject.name} 受到 {damage} 伤害，剩余 HP：{CurrentHP}/{maxHP}");
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnHit?.Invoke();
        AudioManager.Instance?.PlayEnemyHit();
        if (poiseDamage > 0f) poiseMeter.TakePoiseDamage(poiseDamage);

        var feedback = GetComponent<DamageFeedback>();
        if (feedback != null) feedback.Flash();
        CameraShake.Instance?.Shake(0.06f, 0.04f);

        if (CurrentHP <= 0f) Die();
    }

    public virtual void OnExecuted(float damage)
    {
        CurrentHP = 0f;
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        Die();
    }

    protected virtual void Die()
    {
        if (SavesPermanentDeath)
            SaveSystem.Instance?.MarkEnemyDefeated(SaveID);

        AudioManager.Instance?.PlayEnemyDeath();
        var playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null) playerStats.AddGold(goldDrop);
        OnDied?.Invoke();

        OnDeath();

        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DisableAfterDeath());
    }

    // Override in subclasses to handle death animation via state machine
    protected virtual void OnDeath() => SetAnimBool("isDead");

    // Override in subclasses to reset state machine
    protected virtual void OnRespawn() => SetAnimBool("isIdle");

    protected virtual void OnPoiseBroken() { }

    IEnumerator DisableAfterDeath()
    {
        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);
        deathRoutine = null;
    }

    // ── Save System ──────────────────────────────────────────────────
    public virtual void LoadSaveState(float savedHP, bool defeated)
    {
        if (defeated)
        {
            CurrentHP = 0f;
            OnHPChanged?.Invoke(CurrentHP, maxHP);
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);
        Respawn(savedHP > 0f ? Mathf.Clamp(savedHP, 1f, maxHP) : maxHP);
    }

    public virtual void Respawn(float hp = -1f)
    {
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        transform.position   = initialPosition;
        transform.rotation   = initialRotation;
        transform.localScale = initialScale;
        gameObject.SetActive(true);

        CurrentHP = hp > 0f ? Mathf.Clamp(hp, 1f, maxHP) : maxHP;
        poiseMeter = poiseMeter != null ? poiseMeter : GetComponent<PoiseMeter>();
        poiseMeter?.ResetPoise();

        Rb.linearVelocity    = Vector2.zero;
        attackCooldownTimer  = 0f;
        specialCooldownTimer = specialAttackCooldown;
        player               = null;

        OnHPChanged?.Invoke(CurrentHP, maxHP);
        GetComponent<BossAI>()?.ResetAIState();
        OnRespawn();
    }

    // ── Animation Bool Helper (used by BossAI) ───────────────────────
    static readonly string[] animBoolNames = { "isIdle", "isMoving", "isAttacking", "isStunned", "isDead" };

    public virtual void SetAnimBool(string boolName)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null) return;
        foreach (var name in animBoolNames)
            Anim.SetBool(name, name == boolName);
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Vector2 atkOrigin = (Vector2)transform.position +
                            new Vector2(attackCheckOffset.x * FacingDir, attackCheckOffset.y);
        Gizmos.color = new Color(1f, 0f, 1f, 0.4f);
        Gizmos.DrawCube(atkOrigin, attackHitboxSize);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(atkOrigin, attackHitboxSize);
    }
}
