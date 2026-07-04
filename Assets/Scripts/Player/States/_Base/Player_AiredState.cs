using UnityEngine;

public abstract class Player_AiredState : PlayerBaseState
{
    protected Player_AiredState(PlayerController player, StateMachine sm, string animBoolName)
        : base(player, sm, animBoolName) { }

    public override void Update()
    {
        base.Update();
        if (CheckGlobalTransitions()) return;
        if (CheckAiredTransitions()) return;
    }

    protected bool CheckAiredTransitions()
    {
        float h = input.MoveInput.x;
        if (h != 0f)
        {
            rb.linearVelocity = new Vector2(h * player.moveSpeed * player.airMoveSpeedMultiplier, rb.linearVelocity.y);
            player.SetFacing(h);
        }

        if (CheckCombatTransitions()) return true;   // 攻击 / 格挡(地面空中共用)

        return false;
    }
}
