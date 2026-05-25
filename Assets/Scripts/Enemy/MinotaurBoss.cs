using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinotaurBoss : EnemyBase
{
    // ── State Machine ────────────────────────────────────────────────
    public BossStateMachine        stateMachine        { get; private set; }
    public Boss_IdleState          idleState           { get; private set; }
    public Boss_BattleState        battleState         { get; private set; }
    public Boss_ChaseState         chaseState          { get; private set; }
    public Boss_WaitState          waitState           { get; private set; }
    public Boss_NormalAttackState  normalAttackState   { get; private set; }
    public Boss_SpecialAttackState specialAttackState  { get; private set; }
    public Boss_RushState          rushAttackState     { get; private set; }
    public Boss_EnragedState       enragedState        { get; private set; }
    public Boss_StunnedState       stunnedState        { get; private set; }
    public Boss_DeadState          deadState           { get; private set; }

    // ── Inspector Config ─────────────────────────────────────────────
    [Header("Boss Movement")]
    public float moveSpeed        = 2.2f;
    public float phase2SpeedBonus = 1.2f;

    [Header("Phase 2")]
    public float phase2HPThreshold      = 0.5f;
    public float phase2AttackMultiplier = 1.5f;
    public float rushAttackCooldown     = 4f;
    public float rushSpeed              = 12f;
    public float rushDuration           = 0.3f;

    [Header("Attack Weights")]
    public AttackWeight[] attackPool = new AttackWeight[]
    {
        new AttackWeight { id = AttackId.Normal,  weightPhase1 = 6f, weightPhase2 = 4f },
        new AttackWeight { id = AttackId.Special, weightPhase1 = 4f, weightPhase2 = 3f },
        new AttackWeight { id = AttackId.Rush,    weightPhase1 = 0f, weightPhase2 = 3f },
    };

    [Header("Special Attack Indicator")]
    public bool           showAttackIndicator    = true;
    public SpriteRenderer indicatorRenderer;
    public Vector2        indicatorSize          = new Vector2(2f, 1.5f);
    public Vector2        indicatorOffset        = new Vector2(2f, 0f);
    public Color          indicatorWarningColor  = new Color(1f, 0.2f, 0.2f, 0.35f);
    public Color          indicatorImpactColor   = new Color(1f, 0.7f, 0.2f, 0.85f);

    // ── Aliases for state classes（语义化别名 → 父类字段）─────────────
    public float normalAttackCooldown   => attackCooldown;
    public float specialWarningDuration => specialAttackWarningDuration;

    // ── Runtime ──────────────────────────────────────────────────────
    public bool  IsPhase2     { get; private set; }
    public float specialTimer { get; set; }
    public float rushTimer    { get; set; }

    DamageFeedback damageFeedback;
    Coroutine      indicatorRoutine;

    public DamageFeedback DamageFeedback => damageFeedback;

    public enum AttackId { Normal, Special, Rush }

    [System.Serializable]
    public class AttackWeight
    {
        public AttackId id;
        public float    weightPhase1;
        public float    weightPhase2;
    }

    // ── SetAnimBool（所有 8 个 bool）──────────────────────────────────
    static readonly string[] boolNames =
    {
        "isIdle", "isMoving", "isAttacking", "isAttacking2",
        "isSpecialAttacking", "isDefending", "isHit", "isDead"
    };

    public override void SetAnimBool(string boolName)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null) return;
        foreach (var name in boolNames)
            Anim.SetBool(name, name == boolName);
    }

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        damageFeedback = GetComponent<DamageFeedback>();
        specialTimer = specialAttackCooldown;
        rushTimer    = rushAttackCooldown;

        stateMachine        = new BossStateMachine();
        idleState           = new Boss_IdleState(this, stateMachine);
        battleState         = new Boss_BattleState(this, stateMachine);
        chaseState          = new Boss_ChaseState(this, stateMachine);
        waitState           = new Boss_WaitState(this, stateMachine);
        normalAttackState   = new Boss_NormalAttackState(this, stateMachine);
        specialAttackState  = new Boss_SpecialAttackState(this, stateMachine);
        rushAttackState     = new Boss_RushState(this, stateMachine);
        enragedState        = new Boss_EnragedState(this, stateMachine);
        stunnedState        = new Boss_StunnedState(this, stateMachine);
        deadState           = new Boss_DeadState(this, stateMachine);

        stateMachine.Initialize(idleState);

        if (indicatorRenderer != null) indicatorRenderer.gameObject.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();
        if (CurrentHP <= 0f) return;

        if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
        if (specialTimer        > 0f) specialTimer        -= Time.deltaTime;
        if (rushTimer           > 0f) rushTimer           -= Time.deltaTime;

        CheckPhase2();
        stateMachine.Update();
    }

    void CheckPhase2()
    {
        if (IsPhase2) return;
        if (CurrentHP / maxHP >= phase2HPThreshold) return;
        if (stateMachine.currentState == deadState || stateMachine.currentState == enragedState) return;

        IsPhase2 = true;
        stateMachine.ChangeState(enragedState);
    }

    protected override void OnDeath()   => stateMachine.ChangeState(deadState);
    protected override void OnRespawn()
    {
        IsPhase2     = false;
        specialTimer = specialAttackCooldown;
        rushTimer    = rushAttackCooldown;
        stateMachine.ChangeState(idleState);
    }

    protected override void OnPoiseBroken()
    {
        if (CurrentHP <= 0f) return;
        stateMachine.ChangeState(stunnedState);
    }

    // ── Animation Event Entry Points（动画事件直接调这里）─────────────
    public void AnimHitOpen()  => (stateMachine.currentState as BossBaseState)?.OnHitWindowOpen();
    public void AnimHitClose() => (stateMachine.currentState as BossBaseState)?.OnHitWindowClose();
    public void AnimFinish()   => (stateMachine.currentState as BossBaseState)?.OnAnimationFinished();

    // ── Attack Selection ─────────────────────────────────────────────
    public AttackId? PickAttack()
    {
        float total = 0f;
        var available = new List<(AttackWeight opt, float weight)>();
        foreach (var opt in attackPool)
        {
            if (!IsAttackAvailable(opt.id)) continue;
            float w = IsPhase2 ? opt.weightPhase2 : opt.weightPhase1;
            if (w <= 0f) continue;
            available.Add((opt, w));
            total += w;
        }

        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        float acc  = 0f;
        foreach (var (opt, w) in available)
        {
            acc += w;
            if (roll <= acc) return opt.id;
        }
        return available[available.Count - 1].opt.id;
    }

    bool IsAttackAvailable(AttackId id)
    {
        switch (id)
        {
            case AttackId.Normal:  return true;
            case AttackId.Special: return specialTimer <= 0f;
            case AttackId.Rush:    return IsPhase2 && rushTimer <= 0f;
        }
        return false;
    }

    // ── Attack Indicator ─────────────────────────────────────────────
    public void ShowIndicator(float duration)
    {
        if (!showAttackIndicator || indicatorRenderer == null) return;
        if (indicatorRoutine != null) StopCoroutine(indicatorRoutine);
        indicatorRoutine = StartCoroutine(IndicatorRoutine(duration));
    }

    IEnumerator IndicatorRoutine(float duration)
    {
        indicatorRenderer.gameObject.SetActive(true);
        indicatorRenderer.transform.localScale = new Vector3(indicatorSize.x, indicatorSize.y, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 worldOffset = new Vector3(
                indicatorOffset.x * FacingDir,
                indicatorOffset.y,
                0f
            );
            indicatorRenderer.transform.position = transform.position + worldOffset;

            float t = elapsed / duration;
            indicatorRenderer.color = Color.Lerp(indicatorWarningColor, indicatorImpactColor, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        indicatorRenderer.gameObject.SetActive(false);
        indicatorRoutine = null;
    }

    // ── Gizmos ───────────────────────────────────────────────────────
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (showAttackIndicator)
        {
            int dir = FacingDir;
            Vector3 center = transform.position + new Vector3(indicatorOffset.x * dir, indicatorOffset.y, 0f);
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawCube(center, new Vector3(indicatorSize.x, indicatorSize.y, 0.1f));
            Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
            Gizmos.DrawWireCube(center, new Vector3(indicatorSize.x, indicatorSize.y, 0.1f));
        }
    }
}
