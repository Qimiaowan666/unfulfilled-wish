using UnityEngine;

public class Player_StunnedState : PlayerBaseState
{
    public float Duration = 0.5f;

    public Player_StunnedState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isStunned") { }

    public override void Enter()
    {
        base.Enter();
        stateTimer        = Duration;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0f)
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
    }
}
