using UnityEngine;

// 路由 hub state：不绑动画，进入即根据距离/冷却分发到 chase/wait/attack
public class Boss_BattleState : BossBaseState
{
    public Boss_BattleState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "") {}

    public override void Update()
    {
        base.Update();

        if (!boss.DetectPlayer())
        {
            stateMachine.ChangeState(boss.idleState);
            return;
        }

        float dir = Mathf.Sign(boss.player.position.x - boss.transform.position.x);
        boss.SetFacing(dir);

        float dist = boss.GetHorizontalDistToPlayer();

        if (dist <= boss.attackRange && boss.attackCooldownTimer <= 0f)
        {
            var picked = boss.PickAttack();
            if (picked == MinotaurBoss.AttackId.Rush)
                stateMachine.ChangeState(boss.rushAttackState);
            else if (picked == MinotaurBoss.AttackId.Special)
                stateMachine.ChangeState(boss.specialAttackState);
            else if (picked == MinotaurBoss.AttackId.Normal)
                stateMachine.ChangeState(boss.normalAttackState);
            else
                stateMachine.ChangeState(boss.waitState);  // 都没选中也站着等
        }
        else if (dist > boss.attackRange)
        {
            stateMachine.ChangeState(boss.chaseState);
        }
        else
        {
            stateMachine.ChangeState(boss.waitState);
        }
    }
}
