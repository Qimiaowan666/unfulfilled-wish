using UnityEngine;

public class Enemy_AttackState : EnemyBaseState
{
    public Enemy_AttackState(AoTenguEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isAttacking") { }

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
        stateTimer = 1.2f;
        enemy.StartAttackCooldown();
        AudioManager.Instance?.PlayEnemyAttack();
    }

    public override void Update()
    {
        base.Update();

        if (triggerCalled || stateTimer < 0f)
            stateMachine.ChangeState(enemy.player != null
                ? (EnemyBaseState)enemy.chaseState
                : enemy.idleState);
    }
}
