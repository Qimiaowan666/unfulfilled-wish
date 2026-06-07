using UnityEngine;

public class Boss_StunnedState : BossBaseState
{
    public Boss_StunnedState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isHit") {}

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
        stateTimer = boss.IsPhase2 ? 2f : 3f;
    }

    public override void Update()
    {
        base.Update();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (boss.CurrentHP <= 0f) return; // OnDeath 会切到 deadState

        if (stateTimer < 0f)
            stateMachine.ChangeState(boss.battleState);
    }

    public override void Exit()
    {
        base.Exit();
        // 无论怎么离开硬直（计时结束 / phase 切换 / 其它打断）都恢复韧性，避免韧性条永久卡空
        boss.GetComponent<PoiseMeter>()?.ResetPoise();
    }
}
