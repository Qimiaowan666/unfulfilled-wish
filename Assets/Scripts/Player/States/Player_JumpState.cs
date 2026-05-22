using UnityEngine;

public class Player_JumpState : Player_AiredState
{
    public Player_JumpState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isJumping") { }

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, player.jumpForce);
        AudioManager.Instance?.PlayJump();
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this) return;

        if (rb.linearVelocity.y < -0.1f)
            stateMachine.ChangeState(player.fallState);
    }
}
