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
        {
            boss.GetComponent<PoiseMeter>()?.ResetPoise();
            stateMachine.ChangeState(boss.battleState);
        }
    }
}
