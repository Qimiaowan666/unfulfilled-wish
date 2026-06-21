using UnityEngine;

// 小怪状态基类:公共部分(anim/rb/animBool/timer + Enter/Exit/Update)在 EntityState;这里只加 owner。
// 小怪非攻击态走 Anim.Play(animBool 留空);攻击态的 isAttacking 由运行器设、Enemy_AttackState.Exit 清。
public abstract class EnemyBaseState : EntityState
{
    protected GroundEnemy enemy;

    public EnemyBaseState(GroundEnemy enemy, EnemyStateMachine stateMachine, string animBoolName = "")
        : base(stateMachine, animBoolName)
    {
        this.enemy = enemy;
        rb   = enemy.Rb;
        anim = enemy.Anim;
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = 0f;   // 保留小怪原行为:进状态清计时
    }
}
