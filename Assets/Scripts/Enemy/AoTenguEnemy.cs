using UnityEngine;

public class AoTenguEnemy : EnemyBase
{
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

    public void AttackTrigger() => PerformAttack(attack);
}
