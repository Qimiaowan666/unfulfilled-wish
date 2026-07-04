using UnityEngine;

public class Player_DashState : PlayerBaseState
{
    float dashDir;
    float originalGravityScale;

    public Player_DashState(PlayerController player, StateMachine sm)
        : base(player, sm, "isDashing") { }

    public override void Enter()
    {
        base.Enter();
        AudioManager.Instance?.PlayDash();

        float h = input.MoveInput.x;
        dashDir = h != 0f ? Mathf.Sign(h) : (player.FacingRight ? 1f : -1f);

        player.SetFacing(dashDir);

        originalGravityScale   = rb.gravityScale;
        rb.gravityScale        = 0f;
        stateTimer             = player.dashDuration;

        player.Stats.SetInvulnerable(true);
    }

    public override void Update()
    {
        base.Update();
        rb.linearVelocity = new Vector2(dashDir * player.dashSpeed, 0f);

        if (stateTimer < 0f)
        {
            stateMachine.ChangeState(player.GroundedOrFall);
        }
    }

    public override void Exit()
    {
        base.Exit();
        rb.gravityScale   = originalGravityScale;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        player.Stats.SetInvulnerable(false);
    }
}
