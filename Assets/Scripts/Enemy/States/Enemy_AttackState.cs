using UnityEngine;

// 小怪攻击态:薄壳。招/连段/段前位移/命中/驱动全在 EnemyBase 的统一运行器(AttackRunner)里。
public class Enemy_AttackState : EnemyBaseState
{
    public Enemy_AttackState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public override void Enter()
    {
        base.Enter();
        enemy.Attack.Begin();   // 当前招/连段已由 ChaseState 的 TryPickCombo/TryPickAttack 选好
    }

    public override void Update()
    {
        enemy.Attack.Tick();
        if (!enemy.Attack.Active)   // 连段打完 → 回追击/待命
            stateMachine.ChangeState(enemy.player != null ? (EnemyBaseState)enemy.chaseState : enemy.idleState);
    }

    public override void Exit()
    {
        if (enemy.Attack.Active) enemy.Attack.Cancel();   // 被破韧/死亡打断 → 收尾(恢复驱动/动画/连段)
    }
}
