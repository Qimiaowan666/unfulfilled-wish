using UnityEngine;

public class Player_MoveState : Player_GroundedState
{
    public Player_MoveState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isMoving") { }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this) return;
        if (IsInputBlocked) return;

        float h = input.MoveInput.x;
        rb.linearVelocity = new Vector2(h * player.moveSpeed, rb.linearVelocity.y);
        player.SetFacing(h);

        if (h == 0f)
            stateMachine.ChangeState(player.idleState);
    }
}
