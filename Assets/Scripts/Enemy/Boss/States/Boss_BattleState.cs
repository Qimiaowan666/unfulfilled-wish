using UnityEngine;

// 路由 hub state：不绑动画，进入即根据距离/冷却分发到 chase/wait/attack
public class Boss_BattleState : BossBaseState
{
    public Boss_BattleState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "") {}

    public override void Update()
    {
        base.Update();

        if (!boss.KeepEngaged())   // 开战后锁定玩家不脱战；只有玩家真不在场才回 Idle
        {
            stateMachine.ChangeState(boss.idleState);
            return;
        }

        float dir = Mathf.Sign(boss.player.position.x - boss.transform.position.x);
        boss.SetFacing(dir);

        float dist = boss.GetHorizontalDistToPlayer();

        if (dist <= boss.attackRange && boss.attackCooldownTimer <= 0f)
        {
            var step = boss.StartNewCombo();
            if (step != null) boss.ExecuteStep(step);
            else stateMachine.ChangeState(boss.waitState);   // 都没选中也站着等
        }
        else if (dist > boss.attackRange)
        {
            // 出攻击范围 + 冷却好 → 直接抽突进技能(跳劈/闪身/瞬移)缩距，不限距离；冷却中才走近
            if (boss.attackCooldownTimer <= 0f)
            {
                var step = boss.StartApproachCombo();
                if (step != null) { boss.ExecuteStep(step); return; }
            }
            stateMachine.ChangeState(boss.chaseState);
        }
        else
        {
            stateMachine.ChangeState(boss.waitState);
        }
    }
}
