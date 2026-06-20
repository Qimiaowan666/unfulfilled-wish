using UnityEngine;

public abstract class Player_AiredState : PlayerBaseState
{
    protected Player_AiredState(PlayerController player, PlayerStateMachine sm, string animBoolName)
        : base(player, sm, animBoolName) { }

    public override void Update()
    {
        base.Update();
        if (CheckGlobalTransitions()) return;
        if (CheckAiredTransitions()) return;
    }

    protected bool CheckAiredTransitions()
    {
        if (IsInputBlocked) return false;

        float h = input.MoveInput.x;
        if (h != 0f)
        {
            rb.linearVelocity = new Vector2(h * player.moveSpeed * player.airMoveSpeedMultiplier, rb.linearVelocity.y);
            player.SetFacing(h);
        }

        if (input.AttackPressed)
        {
            stateMachine.ChangeState(player.attackState);
            return true;
        }

        if (input.BlockHeld)
        {
            stateMachine.ChangeState(player.blockState);
            return true;
        }

        return false;
    }
}
