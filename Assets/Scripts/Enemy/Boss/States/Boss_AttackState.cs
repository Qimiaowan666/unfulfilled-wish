using UnityEngine;

// boss 攻击态:薄壳。招/连段/驱动(跳劈·挑飞·瞬移)/命中全在 EnemyBase 的统一运行器(AttackRunner)。
// 攻击动画仍由运行器 Anim.Play(clip) 强制进入;打完回 combatState(合并了原 battle/chase/wait),由它继续决策/走位。
public class Boss_AttackState : BossBaseState
{
    // 攻击态的 animator bool 由运行器统一设 isAttacking(和小怪/主角一致)。
    public Boss_AttackState(MinotaurBoss b, StateMachine sm) : base(b, sm, "") { }

    public override void Enter()
    {
        base.Enter();
        boss.Attack.Begin();   // 当前连段已由 combatState(Boss_CombatState)的 TryPickCombo 选好;攻击 clip 由运行器 Anim.Play 强制进入
    }

    public override void Update()
    {
        boss.Attack.Tick();
        if (!boss.Attack.Active)
            stateMachine.ChangeState(boss.combatState);
    }

    public override void Exit()
    {
        base.Exit();
        boss.SetOnlyAnimBool("");   
        if (boss.Attack.Active) boss.Attack.Cancel();   // 被破韧/识破/死亡打断 → 收尾
    }
}
