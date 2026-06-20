using UnityEngine;

// boss 攻击态:薄壳。招/连段/驱动(跳劈·挑飞·瞬移)/命中全在 EnemyBase 的统一运行器(AttackRunner)。
// 攻击动画仍由运行器 Anim.Play(clip) 强制进入(和老 boss 攻击态一致);打完回 battleState,由 chase/wait 的 SetAnimBool 返回 idle/move。
public class Boss_AttackState : BossBaseState
{
    // 每段的 animator bool 由运行器按 atk.animBool 设(atk1=isAttacking / atk2·跳劈=isAttacking2 / 大招=isSpecialAttacking / atk3=isAttacking3)。
    public Boss_AttackState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "") { }

    public override void Enter()
    {
        base.Enter();
        boss.Attack.Begin();   // 当前连段已由 BattleState 的 TryPickCombo 选好;攻击 clip 由运行器 Anim.Play 强制进入
    }

    public override void Update()
    {
        boss.Attack.Tick();
        if (!boss.Attack.Active)
            stateMachine.ChangeState(boss.battleState);
    }

    public override void Exit()
    {
        base.Exit();
        boss.SetAnimBool("");   // 清掉最后一段的攻击 bool,避免和 chase 的 isMoving 并存(否则攻击完 boss 用 idle 滑行)
        if (boss.Attack.Active) boss.Attack.Cancel();   // 被破韧/识破/死亡打断 → 收尾
    }
}
