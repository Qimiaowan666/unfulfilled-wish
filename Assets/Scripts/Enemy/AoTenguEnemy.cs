using UnityEngine;

public class AoTenguEnemy : EnemyBase
{
    public enum AttackId { Attack, DashAttack }
    public enum HitboxKey { Attack, DashAttack }

    [System.Serializable]
    public class Choice : AttackWeight
    {
        public AttackId id;
    }

    [System.Serializable]
    public class Hitbox : AttackHitbox
    {
        public HitboxKey key;
    }

    [Header("Attack Weights")]
    public Choice[] attackPool = new Choice[]
    {
        new Choice { id = AttackId.Attack,     weight = 3f },
        new Choice { id = AttackId.DashAttack, weight = 1f },
    };

    [Header("Attack Hitboxes")]
    public Hitbox[] hitboxes;

    public EnemyStateMachine     stateMachine    { get; private set; }
    public Enemy_IdleState       idleState       { get; private set; }
    public Enemy_MoveState       moveState       { get; private set; }
    public Enemy_ChaseState      chaseState      { get; private set; }
    public Enemy_AttackState     attackState     { get; private set; }
    public Enemy_DashAttackState dashAttackState { get; private set; }
    public Enemy_StunnedState    stunnedState    { get; private set; }
    public Enemy_DeadState       deadState       { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        stateMachine    = new EnemyStateMachine();
        idleState       = new Enemy_IdleState(this, stateMachine);
        moveState       = new Enemy_MoveState(this, stateMachine);
        chaseState      = new Enemy_ChaseState(this, stateMachine);
        attackState     = new Enemy_AttackState(this, stateMachine);
        dashAttackState = new Enemy_DashAttackState(this, stateMachine);
        stunnedState    = new Enemy_StunnedState(this, stateMachine);
        deadState       = new Enemy_DeadState(this, stateMachine);

        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();
        if (CurrentHP <= 0f) return;
        if (attackCooldownTimer  > 0f) attackCooldownTimer  -= Time.deltaTime;
        if (specialCooldownTimer > 0f) specialCooldownTimer -= Time.deltaTime;
        stateMachine.Update();
    }

    protected override void OnPoiseBroken()
    {
        if (CurrentHP <= 0f) return;
        stunnedState.SetDuration(stunDuration);
        stateMachine.ChangeState(stunnedState);
    }

    protected override void OnDeath()
    {
        stateMachine.ChangeState(deadState);
    }

    protected override void OnRespawn()
    {
        stateMachine.ChangeState(idleState);
    }

    public void AnimationTrigger() =>
        (stateMachine.currentState as EnemyBaseState)?.AnimationTrigger();

    public void AttackTrigger()
    {
        var hb = GetHitbox(HitboxKey.Attack);
        if (hb != null) PerformAttack(attack, hb.offset, hb.size);
    }

    // ── 选招（按权重抽签）────────────────────────────────────────────
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
            case AttackId.Attack:     return true;   // 外层已检查 attackCooldownTimer
            case AttackId.DashAttack: return specialCooldownTimer <= 0f;
        }
        return false;
    }

    public override AttackHitbox GetHitbox(string id)
    {
        if (hitboxes == null || !System.Enum.TryParse(id, out HitboxKey key)) return null;
        foreach (var hb in hitboxes)
            if (hb != null && hb.key == key) return hb;
        return null;
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (hitboxes == null) return;
        foreach (var hb in hitboxes) DrawHitboxGizmo(hb);
    }
}
