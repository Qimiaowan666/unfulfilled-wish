using UnityEngine;

public abstract class Player_GroundedState : PlayerBaseState
{
    protected Player_GroundedState(PlayerController player, PlayerStateMachine sm, string animBoolName)
        : base(player, sm, animBoolName) { }

    public override void Update()
    {
        base.Update();
        if (CheckGlobalTransitions()) return;
        if (CheckGroundedTransitions()) return;
    }

    protected bool CheckGroundedTransitions()
    {
        if (IsInputBlocked) return false;

        if (!player.IsGrounded && rb.linearVelocity.y < -0.1f)
        {
            stateMachine.ChangeState(player.fallState);
            return true;
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

        if (input.JumpPressed && player.IsGrounded)
        {
            stateMachine.ChangeState(player.jumpState);
            return true;
        }

        if (input.ExecutePressed)
        {
            if (TryStartExecute()) return true;
        }

        return false;
    }

    bool TryStartExecute()
    {
        var hits = Physics2D.OverlapCircleAll(player.transform.position, player.executeRange, player.enemyLayer);
        foreach (var col in hits)
        {
            var enemy = col.GetComponent<EnemyBase>();
            if (enemy != null && enemy.IsExecutable)
            {
                player.executeState.pendingEnemy = enemy;
                stateMachine.ChangeState(player.executeState);
                return true;
            }
        }
        return false;
    }
}
