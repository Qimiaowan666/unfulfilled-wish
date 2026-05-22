using UnityEngine;

public class Player_DeadState : PlayerBaseState
{
    public Player_DeadState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isDead") { }

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
        rb.simulated      = false;
        stateMachine.Lock();
    }
}
