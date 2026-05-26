using System.Collections;
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
    public Boss_EnragedState       enragedState        { get; private set; }
    public Boss_StunnedState       stunnedState        { get; private set; }
    public Boss_StaggerState       staggerState        { get; private set; }
    public Boss_DeadState          deadState           { get; private set; }

    // ── Inspector Config ─────────────────────────────────────────────
    [Header("Boss Movement")]
    public float moveSpeed        = 2.2f;
    public float phase2SpeedBonus = 1.2f;

    [Header("Phase 2")]
    public float phase2HPThreshold      = 0.5f;
    public float phase2AttackMultiplier = 1.5f;

    [Header("Counter Reaction")]
    public float staggerDuration        = 0.6f;   // 被识破后的停顿时长

    [Header("Attack Weights")]
    public Choice[] attackPool = new Choice[]
    {
        new Choice { id = AttackId.Atk1,    weight = 6f, weightPhase2 = 3f },
        new Choice { id = AttackId.Atk2,    weight = 0f, weightPhase2 = 2f },
        new Choice { id = AttackId.Special, weight = 4f, weightPhase2 = 3f },
    };

    [Header("Attack Hitboxes")]
    public Hitbox[] hitboxes;

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

    DamageFeedback damageFeedback;
    Coroutine      indicatorRoutine;

    public DamageFeedback DamageFeedback => damageFeedback;

    public enum AttackId { Atk1, Atk2, Special }
    public enum HitboxKey { Atk1, Atk2, Special }

    [System.Serializable]
    public class Choice : AttackWeight
    {
        public AttackId id;
        public float    weightPhase2 = 1f;
    }

    [System.Serializable]
    public class Hitbox : AttackHitbox
    {
        public HitboxKey key;
    }

    public override AttackHitbox GetHitbox(string id)
    {
        if (hitboxes == null || !System.Enum.TryParse(id, out HitboxKey key)) return null;
        foreach (var hb in hitboxes)
            if (hb != null && hb.key == key) return hb;
        return null;
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

        stateMachine        = new BossStateMachine();
        idleState           = new Boss_IdleState(this, stateMachine);
        battleState         = new Boss_BattleState(this, stateMachine);
        chaseState          = new Boss_ChaseState(this, stateMachine);
        waitState           = new Boss_WaitState(this, stateMachine);
        normalAttackState   = new Boss_NormalAttackState(this, stateMachine);
        specialAttackState  = new Boss_SpecialAttackState(this, stateMachine);
        enragedState        = new Boss_EnragedState(this, stateMachine);
        stunnedState        = new Boss_StunnedState(this, stateMachine);
        staggerState        = new Boss_StaggerState(this, stateMachine);
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

        CheckPhase2();
        stateMachine.Update();
    }

    void CheckPhase2()
    {
        if (IsPhase2) return;
        if (CurrentHP / maxHP >= phase2HPThreshold) return;
        if (stateMachine.currentState == deadState || stateMachine.currentState == enragedState) return;

        IsPhase2 = true;
        foreach (var c in attackPool) c.weight = c.weightPhase2;   // 整体替换为二阶段权重
        stateMachine.ChangeState(enragedState);
    }

    protected override void OnDeath()   => stateMachine.ChangeState(deadState);
    protected override void OnRespawn()
    {
        IsPhase2     = false;
        specialTimer = specialAttackCooldown;
        stateMachine.ChangeState(idleState);
    }

    protected override void OnPoiseBroken()
    {
        if (CurrentHP <= 0f) return;
        stateMachine.ChangeState(stunnedState);
    }

    public override void OnCountered()
    {
        if (CurrentHP <= 0f) return;
        stateMachine.ChangeState(staggerState);
    }

    // ── Animation Event Entry Points（动画事件直接调这里）─────────────
    public void AnimHitOpen()  => (stateMachine.currentState as BossBaseState)?.OnHitWindowOpen();
    public void AnimHitClose() => (stateMachine.currentState as BossBaseState)?.OnHitWindowClose();
    public void AnimFinish()   => (stateMachine.currentState as BossBaseState)?.OnAnimationFinished();

    // ── Attack Selection ─────────────────────────────────────────────
    public AttackId? PickAttack()
    {
        var picked = PickAttack(attackPool, opt =>
            !IsAttackAvailable(opt.id) ? 0f : opt.weight);

        return picked != null ? picked.id : (AttackId?)null;
    }

    bool IsAttackAvailable(AttackId id)
    {
        switch (id)
        {
            case AttackId.Atk1:    return true;
            case AttackId.Atk2:    return true;   // 阶段差异由 weight 控制（phase1 weight=0 即不出）
            case AttackId.Special: return specialTimer <= 0f;
        }
        return false;
    }

    // 路由：根据选中的 AttackId 切到对应 state
    public void EnterAttack(AttackId id)
    {
        switch (id)
        {
            case AttackId.Atk1:
                normalAttackState.Configure(useAtk2: false);
                stateMachine.ChangeState(normalAttackState);
                break;
            case AttackId.Atk2:
                normalAttackState.Configure(useAtk2: true);
                stateMachine.ChangeState(normalAttackState);
                break;
            case AttackId.Special:
                stateMachine.ChangeState(specialAttackState);
                break;
        }
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
        base.OnDrawGizmos();   // 父类画 detection / attack range

        // 每个攻击的 hitbox（按 showGizmo flag）
        if (hitboxes != null)
            foreach (var hb in hitboxes) DrawHitboxGizmo(hb);

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
