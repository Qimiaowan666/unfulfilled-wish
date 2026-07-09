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
        if (stateMachine.currentState != this) return;   // base.Update 可能已把状态切走 → 先判断,别再擦掉新态这一帧的速度
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (input.MoveInput.x != 0f)
            stateMachine.ChangeState(player.moveState);
    }
}
