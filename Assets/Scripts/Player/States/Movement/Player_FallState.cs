using UnityEngine;

public class Player_FallState : Player_AiredState
{
    public Player_FallState(PlayerController player, StateMachine sm)
        : base(player, sm, "isFalling") { }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this) return;

        if (player.IsGrounded)
        {
            AudioManager.Instance?.PlayLand();
            float h = rb.linearVelocity.x;
            stateMachine.ChangeState(Mathf.Abs(h) > 0.05f ? (PlayerBaseState)player.moveState : player.idleState);
        }
    }
}
