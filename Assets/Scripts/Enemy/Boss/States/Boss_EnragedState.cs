using UnityEngine;

public class Boss_EnragedState : BossBaseState
{
    public Boss_EnragedState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isIdle") {}

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
        stateTimer = 1.5f;
        AudioManager.Instance?.PlayBossPhaseChange();
    }

    public override void Update()
    {
        base.Update();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (stateTimer < 0f)
            stateMachine.ChangeState(boss.battleState);
    }
}
