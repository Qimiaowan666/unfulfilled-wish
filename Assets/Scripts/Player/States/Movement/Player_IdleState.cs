using UnityEngine;

public class Player_IdleState : Player_GroundedState
{
    public Player_IdleState(PlayerController player, StateMachine sm)
        : base(player, sm, "isIdle") { }

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (stateMachine.currentState != this) return;

        if (input.MoveInput.x != 0f)
            stateMachine.ChangeState(player.moveState);
    }
}
