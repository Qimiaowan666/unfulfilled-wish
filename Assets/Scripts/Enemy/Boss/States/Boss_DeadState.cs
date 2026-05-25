using UnityEngine;

public class Boss_DeadState : BossBaseState
{
    public Boss_DeadState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isDead") {}

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
    }

    public override void Update()
    {
        // 死亡状态不做事，等 EnemyBase.Die 协程 SetActive(false)
    }
}
